using System.Security.Cryptography;
using System.Text;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class DebitCardProductionService : IDebitCardProductionService
{
    private readonly IDebitCardProductionRepository _repo;
    private readonly ICmsRepository _cmsRepo;
    private readonly ICorePrepaidCmsService _cms;
    private readonly ISensitiveDataProtector _protector;
    private readonly IPersonalizationBureauClient _bureauClient;
    private readonly IEnumerable<ICardNetworkHotlistClient> _networkClients;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public DebitCardProductionService(
        IDebitCardProductionRepository repo,
        ICmsRepository cmsRepo,
        ICorePrepaidCmsService cms,
        ISensitiveDataProtector protector,
        IPersonalizationBureauClient bureauClient,
        IEnumerable<ICardNetworkHotlistClient> networkClients,
        IAuditLogger audit,
        IClock clock)
    {
        _repo = repo;
        _cmsRepo = cmsRepo;
        _cms = cms;
        _protector = protector;
        _bureauClient = bureauClient;
        _networkClients = networkClients;
        _audit = audit;
        _clock = clock;
    }

    public async Task<CmsOperationResult<DebitCardProductionOrder>> CreateProductionOrderAsync(CreateDebitCardProductionOrderRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var customer = await _cmsRepo.GetCustomerByNumberAsync(request.CustomerNumber, cancellationToken).ConfigureAwait(false);
        if (customer is null) return CmsOperationResult<DebitCardProductionOrder>.Fail("14", "Customer was not found.");
        if (customer.Status != CustomerLifecycleStatus.Active || customer.KycStatus != KycStatus.Verified)
            return CmsOperationResult<DebitCardProductionOrder>.Fail("57", "Customer is not active or KYC verified.");

        var product = await _cmsRepo.GetProductByCodeAsync(request.ProductCode, cancellationToken).ConfigureAwait(false);
        if (product is null || product.Status != CardProductStatus.Active)
            return CmsOperationResult<DebitCardProductionOrder>.Fail("58", "Active product was not found.");

        Guid? cardId = null;
        string maskedPan = string.Empty;
        if (request.ExistingCardId.HasValue)
        {
            var card = await _cmsRepo.GetCardAsync(request.ExistingCardId.Value, cancellationToken).ConfigureAwait(false);
            if (card is null) return CmsOperationResult<DebitCardProductionOrder>.Fail("14", "Existing card was not found.");
            cardId = card.Id;
            maskedPan = card.MaskedPan;
        }

        var order = new DebitCardProductionOrder
        {
            OrderNumber = $"DCP-{_clock.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            ProductionType = request.ProductionType,
            Status = DebitCardProductionStatus.Requested,
            CustomerId = customer.Id,
            CustomerNumber = customer.CustomerNumber,
            ProductId = product.Id,
            ProductCode = product.ProductCode,
            CardId = cardId,
            OldCardId = request.ExistingCardId,
            MaskedPan = maskedPan,
            EmbossName = NormalizeEmbossName(string.IsNullOrWhiteSpace(request.EmbossName) ? customer.FullName : request.EmbossName),
            BranchCode = request.BranchCode,
            DeliveryAddress = request.DeliveryAddress,
            BureauCode = string.IsNullOrWhiteSpace(request.BureauCode) ? _bureauClient.BureauCode : request.BureauCode,
            CorrelationId = SafeCorrelation(request.CorrelationId),
            Notes = request.Notes,
            CreatedAt = _clock.UtcNow
        };

        await _repo.AddProductionOrderAsync(order, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(order.CorrelationId, actor, "CreateDebitCardProductionOrder", string.Empty, order.OrderNumber, $"Type={order.ProductionType} Customer={order.CustomerNumber}", "CARD-PRODUCTION");
        return CmsOperationResult<DebitCardProductionOrder>.Success(order, "Debit card production order created.");
    }

    public Task<IReadOnlyList<DebitCardProductionOrder>> GetProductionOrdersAsync(DebitCardProductionStatus? status = null, CancellationToken cancellationToken = default)
        => _repo.GetProductionOrdersAsync(status, cancellationToken);

    public Task<CmsOperationResult<DebitCardBureauFile>> GenerateEmbossingFileAsync(GenerateBureauFileRequest request, string actor, CancellationToken cancellationToken = default)
        => GenerateBureauFileAsync(request, actor, BureauFileType.Embossing, BuildEmbossingRecordAsync, DebitCardProductionStatus.EmbossingFileGenerated, cancellationToken);

    public Task<CmsOperationResult<DebitCardBureauFile>> GeneratePinMailerFileAsync(GenerateBureauFileRequest request, string actor, CancellationToken cancellationToken = default)
        => GenerateBureauFileAsync(request, actor, BureauFileType.PinMailer, BuildPinMailerRecordAsync, DebitCardProductionStatus.PinMailerFileGenerated, cancellationToken);

    public async Task<CmsOperationResult<DebitCardBureauFile>> SubmitPersonalizationBureauFileAsync(SubmitBureauFileRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var file = await _repo.GetBureauFileAsync(request.BureauFileId, cancellationToken).ConfigureAwait(false);
        if (file is null) return CmsOperationResult<DebitCardBureauFile>.Fail("14", "Bureau file was not found.");
        if (file.Status is BureauFileStatus.Acknowledged or BureauFileStatus.Processed)
            return CmsOperationResult<DebitCardBureauFile>.Fail("94", "Bureau file was already acknowledged or processed.");

        var result = await _bureauClient.SendFileAsync(file, file.EncryptedPayloadReference, cancellationToken).ConfigureAwait(false);
        var updated = file with
        {
            Status = result.Accepted ? BureauFileStatus.Acknowledged : BureauFileStatus.Rejected,
            SentAt = _clock.UtcNow,
            AcknowledgedAt = result.Accepted ? _clock.UtcNow : null,
            AckReference = result.Reference,
            RejectionReason = result.Accepted ? string.Empty : result.Message
        };
        await _repo.UpdateBureauFileAsync(updated, cancellationToken).ConfigureAwait(false);

        foreach (var orderId in updated.ProductionOrderIds)
        {
            var order = await _repo.GetProductionOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
            if (order is null) continue;
            await _repo.UpdateProductionOrderAsync(order with
            {
                Status = result.Accepted ? DebitCardProductionStatus.BureauAcknowledged : DebitCardProductionStatus.Failed,
                UpdatedAt = _clock.UtcNow,
                Notes = AppendNote(order.Notes, $"Bureau response {updated.AckReference}: {result.Message}")
            }, cancellationToken).ConfigureAwait(false);
        }

        _audit.LogAdminAudit(SafeCorrelation(request.CorrelationId), actor, "SubmitPersonalizationBureauFile", file.FileReference, updated.AckReference, result.Message, "CARD-PRODUCTION");
        return CmsOperationResult<DebitCardBureauFile>.Success(updated, result.Message);
    }

    public async Task<CmsOperationResult<DebitCardProductionOrder>> AssignInstantBranchCardAsync(AssignInstantBranchCardRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var customer = await _cmsRepo.GetCustomerByNumberAsync(request.CustomerNumber, cancellationToken).ConfigureAwait(false);
        if (customer is null) return CmsOperationResult<DebitCardProductionOrder>.Fail("14", "Customer was not found.");
        if (customer.Status != CustomerLifecycleStatus.Active || customer.KycStatus != KycStatus.Verified)
            return CmsOperationResult<DebitCardProductionOrder>.Fail("57", "Customer is not active or KYC verified.");

        var product = await _cmsRepo.GetProductByCodeAsync(request.ProductCode, cancellationToken).ConfigureAwait(false);
        if (product is null || product.Status != CardProductStatus.Active)
            return CmsOperationResult<DebitCardProductionOrder>.Fail("58", "Active product was not found.");

        var stock = await _repo.GetAvailableBranchStockItemAsync(request.BranchCode, request.ProductCode, cancellationToken).ConfigureAwait(false);
        if (stock is null) return CmsOperationResult<DebitCardProductionOrder>.Fail("57", "No available instant card stock for branch and product.");
        if (!string.IsNullOrWhiteSpace(request.StockReference) && !string.Equals(stock.StockReference, request.StockReference, StringComparison.OrdinalIgnoreCase))
            return CmsOperationResult<DebitCardProductionOrder>.Fail("57", "Requested stock reference is not the next available stock item.");

        var issue = await _cms.IssueCardAsync(new IssueCardRequest(request.CustomerNumber, request.ProductCode, product.CardKind)
        {
            BatchReference = stock.StockReference
        }, cancellationToken).ConfigureAwait(false);
        if (!issue.IsSuccess || issue.Value is null) return CmsOperationResult<DebitCardProductionOrder>.Fail(issue.ResponseCode, issue.Message);

        var assigned = stock with
        {
            Status = DebitCardStockStatus.Assigned,
            AssignedCustomerId = customer.Id,
            AssignedCardId = issue.Value.Card.Id,
            AssignedBy = actor,
            AssignedAt = _clock.UtcNow
        };
        await _repo.UpdateBranchStockItemAsync(assigned, cancellationToken).ConfigureAwait(false);

        var order = new DebitCardProductionOrder
        {
            OrderNumber = $"DCP-INSTANT-{_clock.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            ProductionType = DebitCardProductionType.InstantBranchIssue,
            Status = DebitCardProductionStatus.Activated,
            CustomerId = customer.Id,
            CustomerNumber = customer.CustomerNumber,
            ProductId = product.Id,
            ProductCode = product.ProductCode,
            CardId = issue.Value.Card.Id,
            BranchStockItemId = assigned.Id,
            MaskedPan = issue.Value.Card.MaskedPan,
            EmbossName = NormalizeEmbossName(customer.FullName),
            BranchCode = request.BranchCode,
            BureauCode = "BRANCH-STOCK",
            CorrelationId = SafeCorrelation(request.CorrelationId),
            Notes = $"Instant card assigned from stock {assigned.StockReference}.",
            CreatedAt = _clock.UtcNow,
            UpdatedAt = _clock.UtcNow
        };
        await _repo.AddProductionOrderAsync(order, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(order.CorrelationId, actor, "AssignInstantBranchCard", assigned.StockReference, order.MaskedPan, $"Branch={request.BranchCode}", "CARD-PRODUCTION");
        return CmsOperationResult<DebitCardProductionOrder>.Success(order, "Instant branch card assigned.");
    }

    public async Task<CmsOperationResult<IssueCardResult>> IssueVirtualDebitCardAsync(IssueVirtualDebitCardRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var product = await _cmsRepo.GetProductByCodeAsync(request.ProductCode, cancellationToken).ConfigureAwait(false);
        if (product is null || product.Status != CardProductStatus.Active)
            return CmsOperationResult<IssueCardResult>.Fail("58", "Active product was not found.");
        if (product.CardKind != PrepaidCardKind.Virtual)
            return CmsOperationResult<IssueCardResult>.Fail("58", "Product is not configured for virtual cards.");

        var result = await _cms.IssueCardAsync(new IssueCardRequest(request.CustomerNumber, request.ProductCode, PrepaidCardKind.Virtual), cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess && result.Value is not null)
        {
            _audit.LogAdminAudit(SafeCorrelation(request.CorrelationId), actor, "IssueVirtualDebitCard", string.Empty, result.Value.Card.MaskedPan, "Virtual debit card issued.", "CARD-PRODUCTION");
        }
        return result;
    }

    public async Task<CmsOperationResult<IReadOnlyList<HotlistPropagationEvent>>> PropagateHotlistAsync(PropagateHotlistRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var card = await _cmsRepo.GetCardAsync(request.CardId, cancellationToken).ConfigureAwait(false);
        if (card is null) return CmsOperationResult<IReadOnlyList<HotlistPropagationEvent>>.Fail("14", "Card was not found.");

        var networks = request.Networks.Count == 0
            ? Enum.GetValues<DebitCardNetwork>().Where(n => n != DebitCardNetwork.Proprietary).ToList()
            : request.Networks.Distinct().ToList();

        var clients = _networkClients.ToDictionary(x => x.Network, x => x);
        var events = new List<HotlistPropagationEvent>();
        foreach (var network in networks)
        {
            var evt = new HotlistPropagationEvent
            {
                CardId = card.Id,
                MaskedPan = card.MaskedPan,
                PanHash = card.PanHash,
                Network = network,
                Reason = request.Reason,
                Status = NetworkPropagationStatus.Pending,
                CreatedAt = _clock.UtcNow
            };

            if (clients.TryGetValue(network, out var client))
            {
                var response = await client.PropagateHotlistAsync(evt, cancellationToken).ConfigureAwait(false);
                evt = evt with
                {
                    Status = response.Accepted ? NetworkPropagationStatus.Acknowledged : NetworkPropagationStatus.Failed,
                    AttemptCount = 1,
                    LastAttemptAt = _clock.UtcNow,
                    AcknowledgedAt = response.Accepted ? _clock.UtcNow : null,
                    NetworkReference = response.NetworkReference,
                    ErrorMessage = response.Accepted ? string.Empty : response.Message
                };
            }
            else
            {
                evt = evt with
                {
                    Status = NetworkPropagationStatus.Failed,
                    AttemptCount = 1,
                    LastAttemptAt = _clock.UtcNow,
                    ErrorMessage = "No network hotlist adapter registered."
                };
            }

            await _repo.AddHotlistEventAsync(evt, cancellationToken).ConfigureAwait(false);
            events.Add(evt);
        }

        var hotlisted = card with
        {
            Status = request.Reason == CardBlockReason.Stolen ? PrepaidCardStatus.Stolen : request.Reason == CardBlockReason.Lost ? PrepaidCardStatus.Lost : PrepaidCardStatus.TemporarilyBlocked,
            BlockReason = request.Reason.ToString(),
            BlockedAt = _clock.UtcNow,
            UpdatedAt = _clock.UtcNow
        };
        await _cmsRepo.UpdateCardAsync(hotlisted, cancellationToken).ConfigureAwait(false);

        _audit.LogAdminAudit(SafeCorrelation(request.CorrelationId), actor, "PropagateCardHotlist", card.MaskedPan, string.Join(',', events.Select(e => $"{e.Network}:{e.Status}")), $"Reason={request.Reason}", "CARD-PRODUCTION");
        return CmsOperationResult<IReadOnlyList<HotlistPropagationEvent>>.Success(events, "Hotlist propagation completed.");
    }

    private async Task<CmsOperationResult<DebitCardBureauFile>> GenerateBureauFileAsync(
        GenerateBureauFileRequest request,
        string actor,
        BureauFileType fileType,
        Func<DebitCardProductionOrder, CancellationToken, Task<string>> recordBuilder,
        DebitCardProductionStatus nextOrderStatus,
        CancellationToken cancellationToken)
    {
        if (request.ProductionOrderIds.Count == 0)
            return CmsOperationResult<DebitCardBureauFile>.Fail("13", "At least one production order is required.");

        var orders = new List<DebitCardProductionOrder>();
        foreach (var id in request.ProductionOrderIds.Distinct())
        {
            var order = await _repo.GetProductionOrderAsync(id, cancellationToken).ConfigureAwait(false);
            if (order is null) return CmsOperationResult<DebitCardBureauFile>.Fail("14", $"Production order {id} was not found.");
            orders.Add(order);
        }

        var lines = new List<string> { $"HDR|{fileType}|{request.BureauCode}|{_clock.UtcNow:yyyyMMddHHmmss}|{orders.Count}" };
        foreach (var order in orders)
        {
            lines.Add(await recordBuilder(order, cancellationToken).ConfigureAwait(false));
        }
        lines.Add($"TRL|{orders.Count}");
        var clearPayload = string.Join(Environment.NewLine, lines);
        var protectedPayload = _protector.Protect(clearPayload, $"BUREAU-{fileType}");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clearPayload)));
        var reference = $"{fileType.ToString().ToUpperInvariant()}-{_clock.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var file = new DebitCardBureauFile
        {
            FileReference = reference,
            FileType = fileType,
            Status = BureauFileStatus.Generated,
            BureauCode = request.BureauCode,
            FileName = $"{request.FileNamePrefix}_{reference}.dat",
            ContentHash = hash,
            EncryptedPayloadReference = protectedPayload,
            ProductionOrderIds = orders.Select(x => x.Id).ToList(),
            RecordCount = orders.Count,
            GeneratedAt = _clock.UtcNow
        };
        await _repo.AddBureauFileAsync(file, cancellationToken).ConfigureAwait(false);

        foreach (var order in orders)
        {
            await _repo.UpdateProductionOrderAsync(order with { Status = nextOrderStatus, UpdatedAt = _clock.UtcNow }, cancellationToken).ConfigureAwait(false);
        }

        _audit.LogAdminAudit(SafeCorrelation(request.CorrelationId), actor, $"Generate{fileType}File", string.Empty, file.FileReference, $"Records={file.RecordCount}", "CARD-PRODUCTION");
        return CmsOperationResult<DebitCardBureauFile>.Success(file, $"{fileType} file generated.");
    }

    private async Task<string> BuildEmbossingRecordAsync(DebitCardProductionOrder order, CancellationToken cancellationToken)
    {
        var card = order.CardId.HasValue ? await _cmsRepo.GetCardAsync(order.CardId.Value, cancellationToken).ConfigureAwait(false) : null;
        var pan = card is null ? string.Empty : _protector.Unprotect(card.CardNumberToken, "PAN");
        var expiry = card is null ? string.Empty : $"{card.ExpiryMonth:00}{card.ExpiryYear % 100:00}";
        return $"EMB|{order.OrderNumber}|{order.CustomerNumber}|{order.EmbossName}|{pan}|{expiry}|{order.BranchCode}|{order.DeliveryAddress}";
    }

    private async Task<string> BuildPinMailerRecordAsync(DebitCardProductionOrder order, CancellationToken cancellationToken)
    {
        var card = order.CardId.HasValue ? await _cmsRepo.GetCardAsync(order.CardId.Value, cancellationToken).ConfigureAwait(false) : null;
        var pinRef = card is null || string.IsNullOrWhiteSpace(card.PinToken) ? "PIN-NOT-SET" : $"PINREF-{card.Id.ToString("N")[..12]}";
        return $"PIN|{order.OrderNumber}|{order.CustomerNumber}|{order.MaskedPan}|{pinRef}|{order.BranchCode}|{order.DeliveryAddress}";
    }

    private static string SafeCorrelation(string correlationId) => string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId;
    private static string NormalizeEmbossName(string name) => new string((name ?? string.Empty).ToUpperInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '.' || c == '-').Take(26).ToArray()).Trim();
    private static string AppendNote(string existing, string note) => string.IsNullOrWhiteSpace(existing) ? note : existing + " | " + note;
}
