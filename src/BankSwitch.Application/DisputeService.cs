using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class DisputeService : IDisputeService
{
    private readonly IDisputeRepository _repo;
    private readonly IChargebackService _chargebacks;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    // Visa/Mastercard dispute resolution time limits (business days)
    // 30 days for customer to file, 45 days for bank to investigate, 60 days to escalate
    private static readonly TimeSpan EvidenceRequestWindow = TimeSpan.FromDays(10);
    private static readonly TimeSpan InvestigationWindow = TimeSpan.FromDays(45);

    public DisputeService(IDisputeRepository repo, IChargebackService chargebacks, IAuditLogger audit, IClock clock)
    {
        _repo = repo;
        _chargebacks = chargebacks;
        _audit = audit;
        _clock = clock;
    }

    public async Task<CmsOperationResult<CustomerDispute>> IntakeDisputeAsync(
        IntakeDisputeRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Rrn) && string.IsNullOrWhiteSpace(request.Stan))
            return Fail("30", "Either RRN or STAN is required to identify the disputed transaction.");
        if (request.DisputedAmount <= 0m) return Fail("13", "Disputed amount must be greater than zero.");

        var disputeRef = $"DSP-{_clock.UtcNow:yyyyMMdd}-{Guid.NewGuid():N[..8]}".ToUpperInvariant();
        var evidenceDeadline = _clock.UtcNow.Add(EvidenceRequestWindow);

        var dispute = new CustomerDispute
        {
            DisputeReference = disputeRef,
            CustomerNumber = request.CustomerNumber,
            CustomerId = request.CustomerId,
            DisputeType = request.DisputeType,
            Status = DisputeStatus.Received,
            Rrn = request.Rrn,
            Stan = request.Stan,
            MaskedPan = request.MaskedPan,
            DisputedAmount = request.DisputedAmount,
            CurrencyCode = request.CurrencyCode,
            TransactionDate = request.TransactionDate,
            MerchantName = request.MerchantName,
            CustomerStatement = request.CustomerStatement,
            Channel = request.Channel,
            EvidenceDeadline = evidenceDeadline,
            ReceivedAt = _clock.UtcNow
        };

        await _repo.AddDisputeAsync(dispute, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(disputeRef, actor, "DisputeIntake",
            string.Empty, $"type={request.DisputeType} amount={request.DisputedAmount} rrn={request.Rrn}",
            request.CustomerNumber, string.Empty);

        return Ok(dispute, $"Dispute {disputeRef} received. Evidence deadline: {evidenceDeadline:yyyy-MM-dd}.");
    }

    public async Task<CmsOperationResult<EvidenceItem>> AddEvidenceAsync(
        Guid disputeId, AddEvidenceRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var dispute = await _repo.GetDisputeAsync(disputeId, cancellationToken).ConfigureAwait(false);
        if (dispute is null) return CmsOperationResult<EvidenceItem>.Fail("25", "Dispute not found.");
        if (dispute.Status is DisputeStatus.ResolvedInFavourOfCustomer or DisputeStatus.ResolvedInFavourOfMerchant or DisputeStatus.Withdrawn)
            return CmsOperationResult<EvidenceItem>.Fail("57", $"Dispute is already resolved ({dispute.Status}). No further evidence can be added.");
        if (string.IsNullOrWhiteSpace(request.DocumentVaultReference))
            return CmsOperationResult<EvidenceItem>.Fail("30", "Document vault reference is required.");

        var evidence = new EvidenceItem
        {
            DisputeId = disputeId,
            EvidenceType = (BankSwitch.Domain.EvidenceType) request.EvidenceType,
            Description = request.Description,
            DocumentVaultReference = request.DocumentVaultReference,
            SubmittedBy = actor,
            SubmittedByRole = request.SubmittedByRole,
            SubmittedAt = _clock.UtcNow
        };
        await _repo.AddEvidenceAsync(evidence, cancellationToken).ConfigureAwait(false);

        // Advance status to EvidenceReceived if we were waiting
        if (dispute.Status == DisputeStatus.EvidenceRequested)
        {
            var updated = dispute with { Status = DisputeStatus.EvidenceReceived, UpdatedAt = _clock.UtcNow };
            await _repo.UpdateDisputeAsync(updated, cancellationToken).ConfigureAwait(false);
        }

        _audit.LogAdminAudit(dispute.DisputeReference, actor, "EvidenceAdded",
            string.Empty, $"type={request.EvidenceType} vault={request.DocumentVaultReference}", request.SubmittedByRole, string.Empty);

        return CmsOperationResult<EvidenceItem>.Success(evidence, "Evidence added to dispute.");
    }

    public async Task<CmsOperationResult<CustomerDispute>> UpdateStatusAsync(
        Guid disputeId, DisputeStatus newStatus, string notes, string actor, CancellationToken cancellationToken = default)
    {
        var dispute = await _repo.GetDisputeAsync(disputeId, cancellationToken).ConfigureAwait(false);
        if (dispute is null) return Fail("25", "Dispute not found.");

        var updated = dispute with { Status = newStatus, InternalNotes = notes, AssignedTo = actor, UpdatedAt = _clock.UtcNow };
        await _repo.UpdateDisputeAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(dispute.DisputeReference, actor, "DisputeStatusUpdated",
            dispute.Status.ToString(), newStatus.ToString(), notes, string.Empty);

        return Ok(updated);
    }

    /// <summary>
    /// Network rules-based escalation: evaluates the dispute type and transaction
    /// characteristics to determine whether the dispute qualifies for a chargeback
    /// under Visa/Mastercard/NIBSS rules, and files the chargeback if so.
    /// </summary>
    public async Task<CmsOperationResult<CustomerDispute>> EscalateToChargebackAsync(
        Guid disputeId, string actor, CancellationToken cancellationToken = default)
    {
        var dispute = await _repo.GetDisputeAsync(disputeId, cancellationToken).ConfigureAwait(false);
        if (dispute is null) return Fail("25", "Dispute not found.");
        if (dispute.LinkedChargebackId.HasValue) return Fail("57", "Dispute already has a linked chargeback case.");

        // Determine the applicable card network from BIN (simplified: use the MaskedPan prefix)
        var network = DetermineNetwork(dispute.MaskedPan);

        // Map dispute type to chargeback reason code (Visa/Mastercard rulebook mapping)
        var (reasonCode, reasonDesc) = MapToReasonCode(network, dispute.DisputeType);

        var chargebackRequest = new FileChargebackRequest(
            network,
            OriginalTransactionCorrelationId: dispute.Rrn, // best proxy for correlation ID
            Rrn: dispute.Rrn,
            Stan: dispute.Stan,
            MaskedPan: dispute.MaskedPan,
            PanHash: string.Empty,
            TransactionAmount: dispute.DisputedAmount,
            ChargebackAmount: dispute.DisputedAmount,
            CurrencyCode: dispute.CurrencyCode,
            TransactionDate: dispute.TransactionDate,
            ReasonCode: reasonCode,
            NetworkCaseId: string.Empty,
            IssuerBin: string.Empty,
            AcquirerBin: string.Empty,
            MerchantId: string.Empty,
            TerminalId: string.Empty,
            CorrelationId: Guid.NewGuid().ToString("N"));

        var cbResult = await _chargebacks.FileChargebackAsync(chargebackRequest, actor, cancellationToken).ConfigureAwait(false);
        if (!cbResult.IsSuccess) return Fail(cbResult.ResponseCode, $"Chargeback filing failed: {cbResult.Message}");

        var escalated = dispute with
        {
            Status = DisputeStatus.Escalated,
            LinkedChargebackId = cbResult.Value!.Id,
            EscalatedAt = _clock.UtcNow,
            UpdatedAt = _clock.UtcNow
        };
        await _repo.UpdateDisputeAsync(escalated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(dispute.DisputeReference, actor, "DisputeEscalatedToChargeback",
            dispute.Status.ToString(), "Escalated",
            $"chargebackId={cbResult.Value.Id} code={reasonCode}", string.Empty);

        return Ok(escalated, $"Dispute escalated to chargeback {cbResult.Value.CaseReference} (code {reasonCode}: {reasonDesc}).");
    }

    public async Task<CmsOperationResult<CustomerDispute>> ResolveAsync(
        Guid disputeId, DisputeStatus outcome, decimal? awardedAmount, string notes, string actor, CancellationToken cancellationToken = default)
    {
        var dispute = await _repo.GetDisputeAsync(disputeId, cancellationToken).ConfigureAwait(false);
        if (dispute is null) return Fail("25", "Dispute not found.");

        if (outcome is not (DisputeStatus.ResolvedInFavourOfCustomer or DisputeStatus.ResolvedInFavourOfMerchant
            or DisputeStatus.Withdrawn or DisputeStatus.Expired))
            return Fail("30", $"Invalid resolution outcome: {outcome}.");

        var resolved = dispute with
        {
            Status = outcome,
            AwardedAmount = awardedAmount,
            ResolutionNotes = notes,
            ResolvedAt = _clock.UtcNow,
            UpdatedAt = _clock.UtcNow
        };
        await _repo.UpdateDisputeAsync(resolved, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(dispute.DisputeReference, actor, "DisputeResolved",
            dispute.Status.ToString(), outcome.ToString(),
            $"awarded={awardedAmount} notes={notes}", string.Empty);

        return Ok(resolved, $"Dispute resolved: {outcome}." + (awardedAmount.HasValue ? $" Amount awarded: {awardedAmount}" : ""));
    }

    public Task<IReadOnlyList<CustomerDispute>> GetDisputesAsync(DisputeFilter filter, CancellationToken cancellationToken = default)
        => _repo.GetDisputesAsync(filter, cancellationToken);

    public async Task<CmsOperationResult<CustomerDispute>> GetDisputeAsync(Guid disputeId, CancellationToken cancellationToken = default)
    {
        var d = await _repo.GetDisputeAsync(disputeId, cancellationToken).ConfigureAwait(false);
        return d is null ? Fail("25", "Dispute not found.") : Ok(d);
    }

    public Task<IReadOnlyList<EvidenceItem>> GetEvidenceAsync(Guid disputeId, CancellationToken cancellationToken = default)
        => _repo.GetEvidenceAsync(disputeId, cancellationToken);

    // ---------------------------------------------------------------
    // Network rules engine (simplified mapping)
    // ---------------------------------------------------------------

    private static ChargebackNetwork DetermineNetwork(string maskedPan)
    {
        if (string.IsNullOrWhiteSpace(maskedPan) || maskedPan.Length < 1) return ChargebackNetwork.Nibss;
        var prefix = maskedPan[0];
        return prefix switch
        {
            '4' => ChargebackNetwork.Visa,
            '5' => ChargebackNetwork.Mastercard,
            '3' => ChargebackNetwork.AmericanExpress,
            '6' => ChargebackNetwork.Verve,
            _ => ChargebackNetwork.Nibss
        };
    }

    private static (string Code, string Description) MapToReasonCode(ChargebackNetwork network, DisputeType disputeType)
    {
        // Visa / Mastercard reason code mapping for common dispute types
        // Full rulebook: Visa Dispute Resolution Rules v2.1, Mastercard Chargeback Guide v12
        if (network == ChargebackNetwork.Visa)
        {
            return disputeType switch
            {
                DisputeType.UnauthorisedTransaction => ("10.4", "Other Fraud — Card Absent Environment"),
                DisputeType.TransactionNotReceived => ("13.1", "Merchandise/Services Not Received"),
                DisputeType.TransactionAmountMismatch => ("12.6", "Duplicate Processing"),
                DisputeType.DuplicateTransaction => ("12.6", "Duplicate Processing"),
                DisputeType.QualityOfGoods => ("13.3", "Not as Described"),
                DisputeType.ATMCashNotDispensed => ("10.7", "Card Present Fraud — Other"),
                _ => ("13.7", "Cancelled Recurring or Digital Goods Merchandise")
            };
        }
        else if (network == ChargebackNetwork.Mastercard)
        {
            return disputeType switch
            {
                DisputeType.UnauthorisedTransaction => ("4853", "Cardholder Dispute"),
                DisputeType.TransactionNotReceived => ("4855", "Goods or Services Not Provided"),
                DisputeType.TransactionAmountMismatch => ("4834", "Point of Interaction Error"),
                DisputeType.DuplicateTransaction => ("4834", "Point of Interaction Error"),
                DisputeType.QualityOfGoods => ("4853", "Cardholder Dispute—Not as Described"),
                DisputeType.ATMCashNotDispensed => ("4834", "Point of Interaction Error"),
                _ => ("4853", "Cardholder Dispute")
            };
        }
        else  // Verve/NIBSS
        {
            return disputeType switch
            {
                DisputeType.UnauthorisedTransaction => ("CBR-01", "Fraudulent Transaction"),
                DisputeType.TransactionNotReceived => ("CBR-02", "Service/Goods Not Received"),
                DisputeType.TransactionAmountMismatch => ("CBR-03", "Wrong Amount Debited"),
                DisputeType.DuplicateTransaction => ("CBR-04", "Double Debit"),
                DisputeType.ATMCashNotDispensed => ("CBR-05", "ATM Cash Not Dispensed"),
                _ => ("CBR-99", "Other Dispute")
            };
        }
    }

    private static CmsOperationResult<CustomerDispute> Ok(CustomerDispute d, string msg = "") => CmsOperationResult<CustomerDispute>.Success(d, msg);
    private static CmsOperationResult<CustomerDispute> Fail(string code, string msg) => CmsOperationResult<CustomerDispute>.Fail(code, msg);
}
