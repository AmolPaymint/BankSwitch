using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class DirectDebitMandateService : IDirectDebitMandateService
{
    private readonly IDirectDebitMandateRepository _repo;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public DirectDebitMandateService(IDirectDebitMandateRepository repo, IAuditLogger audit, IClock clock)
    {
        _repo = repo;
        _audit = audit;
        _clock = clock;
    }

    public async Task<CmsOperationResult<DirectDebitMandate>> RegisterMandateAsync(
        RegisterMandateRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DebtorAccountNumber)) return Fail("30", "Debtor account number is required.");
        if (string.IsNullOrWhiteSpace(request.DebtorIfscCode) || request.DebtorIfscCode.Length != 11) return Fail("30", "Valid 11-character IFSC is required.");
        if (request.MaximumAmount <= 0) return Fail("30", "Maximum debit amount must be greater than zero.");
        if (request.StartDate < DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime)) return Fail("30", "Mandate start date cannot be in the past.");
        if (request.EndDate.HasValue && request.EndDate < request.StartDate) return Fail("30", "End date cannot be before start date.");

        var umrn = GenerateUmrn();
        var mandate = new DirectDebitMandate
        {
            MandateReference = umrn,
            CustomerNumber = request.CustomerNumber,
            CustomerId = request.CustomerId,
            DebtorAccountNumber = request.DebtorAccountNumber.Trim(),
            DebtorIfscCode = request.DebtorIfscCode.Trim().ToUpperInvariant(),
            DebtorBankName = request.DebtorBankName.Trim(),
            CreditorAccountNumber = request.CreditorAccountNumber.Trim(),
            CreditorIfscCode = request.CreditorIfscCode.Trim().ToUpperInvariant(),
            CreditorName = request.CreditorName.Trim(),
            MaximumAmount = request.MaximumAmount,
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "356" : request.CurrencyCode.Trim(),
            Frequency = request.Frequency,
            Status = MandateStatus.Pending,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedAt = _clock.UtcNow
        };
        await _repo.AddAsync(mandate, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(umrn, actor, "MandateRegistered", string.Empty, $"customer={request.CustomerNumber} maxAmt={request.MaximumAmount} freq={request.Frequency}", mandate.CreditorName, string.Empty);
        return Ok(mandate, $"Mandate {umrn} registered. Awaiting bank activation.");
    }

    public async Task<CmsOperationResult<DirectDebitMandate>> ActivateMandateAsync(
        Guid mandateId, string umrn, string actor, CancellationToken cancellationToken = default)
    {
        var mandate = await _repo.GetAsync(mandateId, cancellationToken).ConfigureAwait(false);
        if (mandate is null) return Fail("25", "Mandate not found.");
        if (mandate.Status != MandateStatus.Pending) return Fail("57", $"Mandate cannot be activated from status {mandate.Status}.");

        var activated = mandate with { Status = MandateStatus.Active, MandateReference = umrn, ActivatedAt = _clock.UtcNow };
        await _repo.UpdateAsync(activated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(umrn, actor, "MandateActivated", MandateStatus.Pending.ToString(), MandateStatus.Active.ToString(), mandate.CustomerNumber, string.Empty);
        return Ok(activated, $"Mandate {umrn} activated.");
    }

    public async Task<CmsOperationResult<DirectDebitMandate>> SuspendMandateAsync(
        Guid mandateId, string reason, string actor, CancellationToken cancellationToken = default)
    {
        var mandate = await _repo.GetAsync(mandateId, cancellationToken).ConfigureAwait(false);
        if (mandate is null) return Fail("25", "Mandate not found.");
        if (mandate.Status != MandateStatus.Active) return Fail("57", "Only active mandates can be suspended.");

        var suspended = mandate with { Status = MandateStatus.Suspended, CancellationReason = reason };
        await _repo.UpdateAsync(suspended, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(mandate.MandateReference, actor, "MandateSuspended", MandateStatus.Active.ToString(), MandateStatus.Suspended.ToString(), reason, string.Empty);
        return Ok(suspended);
    }

    public async Task<CmsOperationResult<DirectDebitMandate>> CancelMandateAsync(
        Guid mandateId, string reason, string actor, CancellationToken cancellationToken = default)
    {
        var mandate = await _repo.GetAsync(mandateId, cancellationToken).ConfigureAwait(false);
        if (mandate is null) return Fail("25", "Mandate not found.");
        if (mandate.Status == MandateStatus.Cancelled) return Ok(mandate, "Mandate already cancelled.");

        var cancelled = mandate with { Status = MandateStatus.Cancelled, CancelledAt = _clock.UtcNow, CancellationReason = reason };
        await _repo.UpdateAsync(cancelled, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(mandate.MandateReference, actor, "MandateCancelled", mandate.Status.ToString(), MandateStatus.Cancelled.ToString(), reason, string.Empty);
        return Ok(cancelled, $"Mandate {mandate.MandateReference} cancelled.");
    }

    public Task<IReadOnlyList<DirectDebitMandate>> GetMandatesAsync(string customerNumber, CancellationToken cancellationToken = default)
        => _repo.GetForCustomerAsync(customerNumber, cancellationToken);

    public async Task<CmsOperationResult<DirectDebitMandate>> GetMandateAsync(Guid mandateId, CancellationToken cancellationToken = default)
    {
        var m = await _repo.GetAsync(mandateId, cancellationToken).ConfigureAwait(false);
        return m is null ? Fail("25", "Mandate not found.") : Ok(m);
    }

    private static string GenerateUmrn() =>
        $"NACH{DateTimeOffset.UtcNow:yyyyMMdd}{Guid.NewGuid():N[..12]}".ToUpperInvariant()[..28];

    private static CmsOperationResult<DirectDebitMandate> Ok(DirectDebitMandate m, string msg = "") => CmsOperationResult<DirectDebitMandate>.Success(m, msg);
    private static CmsOperationResult<DirectDebitMandate> Fail(string code, string msg) => CmsOperationResult<DirectDebitMandate>.Fail(code, msg);
}
