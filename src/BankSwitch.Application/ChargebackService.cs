using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class ChargebackService : IChargebackService
{
    private readonly IChargebackRepository _repo;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public ChargebackService(IChargebackRepository repo, IAuditLogger audit, IClock clock)
    {
        _repo = repo;
        _audit = audit;
        _clock = clock;
    }

    // ---------------------------------------------------------------
    // Stage 1: File initial chargeback
    // ---------------------------------------------------------------

    public async Task<CmsOperationResult<ChargebackCase>> FileChargebackAsync(
        FileChargebackRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReasonCode)) return Fail("30", "Chargeback reason code is required.");
        if (request.ChargebackAmount <= 0m) return Fail("13", "Chargeback amount must be greater than zero.");
        if (request.ChargebackAmount > request.TransactionAmount) return Fail("13", "Chargeback amount cannot exceed transaction amount.");

        var reasonCode = await _repo.GetReasonCodeAsync(request.Network, request.ReasonCode, cancellationToken).ConfigureAwait(false);

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var representmentDeadline = today.AddDays(reasonCode?.RepresentmentDays ?? 45);

        var caseRef = GenerateCaseReference(request.Network);
        var chargebackCase = new ChargebackCase
        {
            CaseReference = caseRef,
            Network = request.Network,
            Stage = ChargebackStage.Received,
            Outcome = ChargebackOutcome.Pending,
            OriginalTransactionCorrelationId = request.OriginalTransactionCorrelationId,
            Rrn = request.Rrn,
            Stan = request.Stan,
            MaskedPan = request.MaskedPan,
            PanHash = request.PanHash,
            TransactionAmount = request.TransactionAmount,
            ChargebackAmount = request.ChargebackAmount,
            CurrencyCode = request.CurrencyCode,
            TransactionDate = request.TransactionDate,
            ReasonCode = request.ReasonCode,
            ReasonDescription = reasonCode?.Description ?? request.ReasonCode,
            NetworkCaseId = request.NetworkCaseId,
            IssuerBin = request.IssuerBin,
            AcquirerBin = request.AcquirerBin,
            MerchantId = request.MerchantId,
            TerminalId = request.TerminalId,
            ChargebackReceivedDate = today,
            RepresentmentDeadline = representmentDeadline,
            CreatedAt = _clock.UtcNow,
            LastUpdatedBy = actor
        };

        await _repo.AddCaseAsync(chargebackCase, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(caseRef, actor, "ChargebackFiled",
            string.Empty, $"rrn={request.Rrn} amount={request.ChargebackAmount} code={request.ReasonCode} network={request.Network}",
            request.MaskedPan, string.Empty);

        return Ok(chargebackCase, $"Chargeback {caseRef} filed. Representment deadline: {representmentDeadline:yyyy-MM-dd}.");
    }

    // ---------------------------------------------------------------
    // Stage 2: Acquirer submits representment
    // ---------------------------------------------------------------

    public async Task<CmsOperationResult<ChargebackCase>> SubmitRepresentmentAsync(
        Guid caseId, string evidenceSummary, string actor, CancellationToken cancellationToken = default)
    {
        var (c, err) = await LoadCase(caseId, cancellationToken);
        if (err is not null) return Fail(err?.Code, err?.Msg);
        if (c!.Stage != ChargebackStage.Received) return Fail("57", $"Representment can only be submitted in Received stage. Current: {c.Stage}.");

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        if (today > c.RepresentmentDeadline) return Fail("57", $"Representment deadline {c.RepresentmentDeadline:yyyy-MM-dd} has passed.");

        var reasonCode = await _repo.GetReasonCodeAsync(c.Network, c.ReasonCode, cancellationToken).ConfigureAwait(false);
        var preArbitrationDeadline = today.AddDays(reasonCode?.PreArbitrationDays ?? 45);

        var updated = c with
        {
            Stage = ChargebackStage.Represented,
            AcquirerEvidenceSummary = evidenceSummary,
            RepresentmentSubmittedDate = today,
            PreArbitrationDeadline = preArbitrationDeadline,
            UpdatedAt = _clock.UtcNow,
            LastUpdatedBy = actor
        };
        await _repo.UpdateCaseAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(c.CaseReference, actor, "RepresentmentSubmitted",
            ChargebackStage.Received.ToString(), ChargebackStage.Represented.ToString(),
            $"preArbDeadline={preArbitrationDeadline:yyyy-MM-dd}", string.Empty);

        return Ok(updated, $"Representment submitted. Pre-arbitration deadline: {preArbitrationDeadline:yyyy-MM-dd}.");
    }

    // ---------------------------------------------------------------
    // Stage 3: Issuer escalates to pre-arbitration
    // ---------------------------------------------------------------

    public async Task<CmsOperationResult<ChargebackCase>> ReceivePreArbitrationAsync(
        Guid caseId, string issuerResponse, string actor, CancellationToken cancellationToken = default)
    {
        var (c, err) = await LoadCase(caseId, cancellationToken);
        if (err is not null) return Fail(err?.Code, err?.Msg);
        if (c!.Stage != ChargebackStage.Represented) return Fail("57", $"Pre-arbitration can only be escalated from Represented. Current: {c.Stage}.");

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var reasonCode = await _repo.GetReasonCodeAsync(c.Network, c.ReasonCode, cancellationToken).ConfigureAwait(false);
        var arbitrationDeadline = today.AddDays(reasonCode?.ArbitrationDays ?? 10);

        var updated = c with
        {
            Stage = ChargebackStage.PreArbitration,
            IssuerEvidenceSummary = issuerResponse,
            PreArbitrationReceivedDate = today,
            ArbitrationDeadline = arbitrationDeadline,
            UpdatedAt = _clock.UtcNow,
            LastUpdatedBy = actor
        };
        await _repo.UpdateCaseAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(c.CaseReference, actor, "PreArbitrationReceived",
            ChargebackStage.Represented.ToString(), ChargebackStage.PreArbitration.ToString(),
            $"arbDeadline={arbitrationDeadline:yyyy-MM-dd}", string.Empty);

        return Ok(updated, $"Pre-arbitration received. Arbitration deadline: {arbitrationDeadline:yyyy-MM-dd}.");
    }

    // ---------------------------------------------------------------
    // Stage 4: Formal arbitration submission
    // ---------------------------------------------------------------

    public async Task<CmsOperationResult<ChargebackCase>> SubmitArbitrationAsync(
        Guid caseId, string arbitrationNotes, string actor, CancellationToken cancellationToken = default)
    {
        var (c, err) = await LoadCase(caseId, cancellationToken);
        if (err is not null) return Fail(err?.Code, err?.Msg);
        if (c!.Stage != ChargebackStage.PreArbitration) return Fail("57", $"Arbitration can only be submitted from PreArbitration. Current: {c.Stage}.");

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        if (c.ArbitrationDeadline.HasValue && today > c.ArbitrationDeadline.Value)
            return Fail("57", $"Arbitration deadline {c.ArbitrationDeadline:yyyy-MM-dd} has passed.");

        var updated = c with
        {
            Stage = ChargebackStage.Arbitration,
            ResolutionNotes = arbitrationNotes,
            ArbitrationSubmittedDate = today,
            UpdatedAt = _clock.UtcNow,
            LastUpdatedBy = actor
        };
        await _repo.UpdateCaseAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(c.CaseReference, actor, "ArbitrationSubmitted",
            ChargebackStage.PreArbitration.ToString(), ChargebackStage.Arbitration.ToString(), string.Empty, string.Empty);

        return Ok(updated, "Formal arbitration submitted to card network.");
    }

    // ---------------------------------------------------------------
    // Resolution
    // ---------------------------------------------------------------

    public async Task<CmsOperationResult<ChargebackCase>> ResolveAsync(
        Guid caseId, ChargebackOutcome outcome, string notes, string actor, CancellationToken cancellationToken = default)
    {
        var (c, err) = await LoadCase(caseId, cancellationToken);
        if (err is not null) return Fail(err?.Code, err?.Msg);
        if (c!.Stage == ChargebackStage.Resolved) return Ok(c, "Case already resolved.");

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var updated = c with
        {
            Stage = ChargebackStage.Resolved,
            Outcome = outcome,
            ResolvedDate = today,
            ResolutionNotes = notes,
            UpdatedAt = _clock.UtcNow,
            LastUpdatedBy = actor
        };
        await _repo.UpdateCaseAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(c.CaseReference, actor, "ChargebackResolved",
            c.Stage.ToString(), ChargebackStage.Resolved.ToString(),
            $"outcome={outcome} notes={notes}", string.Empty);

        return Ok(updated, $"Chargeback {c.CaseReference} resolved: {outcome}.");
    }

    public async Task<CmsOperationResult<ChargebackCase>> WithdrawAsync(
        Guid caseId, string reason, string actor, CancellationToken cancellationToken = default)
    {
        var (c, err) = await LoadCase(caseId, cancellationToken);
        if (err is not null) return Fail(err?.Code, err?.Msg);
        if (c!.Stage == ChargebackStage.Resolved) return Fail("57", "Cannot withdraw a resolved case.");

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var updated = c with { Stage = ChargebackStage.Resolved, Outcome = ChargebackOutcome.Withdrawn, ResolvedDate = today, ResolutionNotes = reason, UpdatedAt = _clock.UtcNow, LastUpdatedBy = actor };
        await _repo.UpdateCaseAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(c.CaseReference, actor, "ChargebackWithdrawn", c.Stage.ToString(), "Withdrawn", reason, string.Empty);
        return Ok(updated, $"Chargeback {c.CaseReference} withdrawn.");
    }

    public Task<IReadOnlyList<ChargebackCase>> GetCasesAsync(ChargebackCaseFilter filter, CancellationToken cancellationToken = default)
        => _repo.GetCasesAsync(filter, cancellationToken);

    public async Task<CmsOperationResult<ChargebackCase>> GetCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var c = await _repo.GetCaseAsync(caseId, cancellationToken).ConfigureAwait(false);
        return c is null ? Fail("25", "Chargeback case not found.") : Ok(c);
    }

    public Task<IReadOnlyList<ChargebackCase>> GetOverdueCasesAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        return _repo.GetOverdueCasesAsync(today, cancellationToken);
    }

    public Task<IReadOnlyList<ChargebackReasonCode>> GetReasonCodesAsync(ChargebackNetwork? network, CancellationToken cancellationToken = default)
        => _repo.GetReasonCodesAsync(network, cancellationToken);

    public Task<ChargebackReasonCode?> GetReasonCodeAsync(ChargebackNetwork network, string code, CancellationToken cancellationToken = default)
        => _repo.GetReasonCodeAsync(network, code, cancellationToken);

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<(ChargebackCase? Case, (string Code, string Msg)? Error)> LoadCase(Guid caseId, CancellationToken ct)
    {
        var c = await _repo.GetCaseAsync(caseId, ct).ConfigureAwait(false);
        return c is null ? (null, ("25", "Chargeback case not found.")) : (c, null);
    }

    private static string GenerateCaseReference(ChargebackNetwork network) =>
        $"CB-{network.ToString().ToUpperInvariant()[..2]}-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N[..8]}".ToUpperInvariant();

    private static CmsOperationResult<ChargebackCase> Ok(ChargebackCase c, string msg = "") => CmsOperationResult<ChargebackCase>.Success(c, msg);
    private static CmsOperationResult<ChargebackCase> Fail(string code, string msg) => CmsOperationResult<ChargebackCase>.Fail(code, msg);
}
