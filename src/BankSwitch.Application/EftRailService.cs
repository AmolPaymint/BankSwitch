using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class EftRailService : IEftRailService
{
    private readonly IEftRepository _repository;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly IEventBus _eventBus;

    public EftRailService(IEftRepository repository, IAuditLogger audit, IClock clock, IEventBus eventBus)
    {
        _repository = repository;
        _audit = audit;
        _clock = clock;
        _eventBus = eventBus;
    }

    public async Task<CmsOperationResult<EftTransfer>> InitiateTransferAsync(InitiateEftTransferRequest request, string actor, CancellationToken cancellationToken = default)
    {
        // --- Validation ---
        if (request.Amount <= 0m)
            return CmsOperationResult<EftTransfer>.Fail("30", "Transfer amount must be greater than zero.");

        var railError = ValidateForRail(request);
        if (!string.IsNullOrEmpty(railError))
            return CmsOperationResult<EftTransfer>.Fail("30", railError);

        if (request.RailType == EftRailType.Rtgs && request.Amount < 200_000m)
            return CmsOperationResult<EftTransfer>.Fail("30", "RTGS minimum transfer amount is ₹2,00,000.");

        var transfer = new EftTransfer
        {
            RailType = request.RailType,
            Status = EftTransferStatus.Initiated,
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId,
            SenderAccountNumber = request.SenderAccountNumber.Trim(),
            SenderIfscCode = NormalizeIfsc(request.SenderIfscCode),
            SenderBankName = request.SenderBankName.Trim(),
            BeneficiaryAccountNumber = request.BeneficiaryAccountNumber.Trim(),
            BeneficiaryIfscCode = NormalizeIfsc(request.BeneficiaryIfscCode),
            BeneficiaryBankName = request.BeneficiaryBankName.Trim(),
            BeneficiaryName = request.BeneficiaryName.Trim(),
            Amount = request.Amount,
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "356" : request.CurrencyCode.Trim(),
            Narration = (request.Narration ?? string.Empty).Trim(),
            CustomerReference = (request.CustomerReference ?? string.Empty).Trim(),
            OriginatingCorrelationId = request.OriginatingCorrelationId ?? string.Empty,
            BatchSequenceNumber = AssignBatchSequence(request.RailType),
            SettlementCycleId = AssignSettlementCycle(request.RailType, _clock.UtcNow),
            CreatedAt = _clock.UtcNow
        };

        await _repository.AddTransferAsync(transfer, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(transfer.CorrelationId, actor, "EftTransferInitiated", string.Empty,
            $"rail={transfer.RailType} amount={transfer.Amount} from={transfer.SenderIfscCode}/{transfer.SenderAccountNumber} to={transfer.BeneficiaryIfscCode}/{transfer.BeneficiaryAccountNumber}",
            transfer.Narration, string.Empty);

        await _eventBus.PublishAsync(new EftTransferInitiatedEvent(
            request.CorrelationId, transfer.Id, transfer.RailType, transfer.Amount,
            transfer.CurrencyCode, transfer.SenderIfscCode, transfer.BeneficiaryIfscCode,
            transfer.SettlementCycleId, actor), cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<EftTransfer>.Success(transfer, $"{transfer.RailType} transfer initiated. CycleId={transfer.SettlementCycleId}");
    }

    public async Task<CmsOperationResult<EftTransfer>> GetTransferStatusAsync(Guid transferId, CancellationToken cancellationToken = default)
    {
        var transfer = await _repository.GetTransferAsync(transferId, cancellationToken).ConfigureAwait(false);
        return transfer is null
            ? CmsOperationResult<EftTransfer>.Fail("25", "EFT transfer not found.")
            : CmsOperationResult<EftTransfer>.Success(transfer);
    }

    public Task<IReadOnlyList<EftTransfer>> GetTransfersAsync(EftTransferFilter filter, CancellationToken cancellationToken = default)
        => _repository.GetTransfersAsync(filter, cancellationToken);

    public async Task<CmsOperationResult<EftTransfer>> RecordRailResponseAsync(Guid transferId, string railTransactionRef, EftTransferStatus status, string reason, string actor, CancellationToken cancellationToken = default)
    {
        var transfer = await _repository.GetTransferAsync(transferId, cancellationToken).ConfigureAwait(false);
        if (transfer is null) return CmsOperationResult<EftTransfer>.Fail("25", "EFT transfer not found.");

        var updated = transfer with
        {
            Status = status,
            RailTransactionRef = railTransactionRef,
            RejectionReason = status == EftTransferStatus.Rejected ? reason : string.Empty,
            SubmittedAt = status == EftTransferStatus.SubmittedToRail ? _clock.UtcNow : transfer.SubmittedAt
        };

        await _repository.UpdateTransferAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(transfer.CorrelationId, actor, "EftRailResponse", transfer.Status.ToString(), status.ToString(), $"ref={railTransactionRef} reason={reason}", string.Empty);
        return CmsOperationResult<EftTransfer>.Success(updated);
    }

    public async Task<CmsOperationResult<EftTransfer>> MarkSettledAsync(Guid transferId, string settlementCycleId, string actor, CancellationToken cancellationToken = default)
    {
        var transfer = await _repository.GetTransferAsync(transferId, cancellationToken).ConfigureAwait(false);
        if (transfer is null) return CmsOperationResult<EftTransfer>.Fail("25", "EFT transfer not found.");

        var updated = transfer with { Status = EftTransferStatus.Settled, SettlementCycleId = settlementCycleId, SettledAt = _clock.UtcNow };
        await _repository.UpdateTransferAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(transfer.CorrelationId, actor, "EftTransferSettled", transfer.Status.ToString(), "Settled", $"cycleId={settlementCycleId}", string.Empty);

        await _eventBus.PublishAsync(new EftTransferSettledEvent(
            Guid.NewGuid().ToString("N"), transfer.Id, transfer.RailType, transfer.Amount,
            transfer.RailTransactionRef, settlementCycleId), cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<EftTransfer>.Success(updated);
    }

    // ---------------------------------------------------------------
    // Rail-specific validation rules
    // ---------------------------------------------------------------

    private static string? ValidateForRail(InitiateEftTransferRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.BeneficiaryAccountNumber)) return "Beneficiary account number is required.";
        if (string.IsNullOrWhiteSpace(r.SenderAccountNumber)) return "Sender account number is required.";

        switch (r.RailType)
        {
            case EftRailType.Neft:
            case EftRailType.Rtgs:
            case EftRailType.Imps:
                if (string.IsNullOrWhiteSpace(r.BeneficiaryIfscCode) || r.BeneficiaryIfscCode.Length != 11)
                    return $"{r.RailType} requires a valid 11-character IFSC code for the beneficiary bank.";
                if (string.IsNullOrWhiteSpace(r.SenderIfscCode) || r.SenderIfscCode.Length != 11)
                    return $"{r.RailType} requires a valid 11-character IFSC code for the sender bank.";
                break;
            case EftRailType.Ach:
                if (string.IsNullOrWhiteSpace(r.BeneficiaryBankName)) return "ACH requires beneficiary bank name.";
                break;
        }
        return null;
    }

    private static string NormalizeIfsc(string ifsc) =>
        (ifsc ?? string.Empty).Trim().ToUpperInvariant();

    private static string AssignBatchSequence(EftRailType rail) =>
        rail == EftRailType.Neft ? $"N{DateTimeOffset.UtcNow:yyyyMMddHHmm}" :
        rail == EftRailType.Ach ? $"ACH{DateTimeOffset.UtcNow:yyyyMMdd}" :
        string.Empty;

    /// <summary>
    /// NEFT settles in hourly cycles; RTGS settles individually (gross); IMPS is real-time.
    /// Returns a human-readable settlement cycle identifier used for batch grouping.
    /// </summary>
    private static string AssignSettlementCycle(EftRailType rail, DateTimeOffset now) => rail switch
    {
        EftRailType.Neft => $"NEFT-{now:yyyyMMdd}-C{(now.Hour + 1):D2}",
        EftRailType.Rtgs => $"RTGS-{now:yyyyMMddHHmmss}-IND",
        EftRailType.Imps => $"IMPS-{now:yyyyMMddHHmmss}",
        EftRailType.Ach => $"ACH-{now:yyyyMMdd}",
        EftRailType.InternalBookTransfer => $"INT-{now:yyyyMMddHHmmss}",
        _ => $"UNK-{now:yyyyMMddHHmmss}"
    };
}
