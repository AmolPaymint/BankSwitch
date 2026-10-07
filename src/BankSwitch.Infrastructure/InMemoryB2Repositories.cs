using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

// ============================================================
// NEFT Batch Repository
// ============================================================

public sealed class InMemoryNeftBatchRepository : INeftBatchRepository
{
    private readonly ConcurrentDictionary<Guid, NeftBatch> _batches = new();

    public Task AddBatchAsync(NeftBatch batch, CancellationToken cancellationToken = default) { _batches[batch.Id] = batch; return Task.CompletedTask; }
    public Task<NeftBatch?> GetBatchAsync(Guid id, CancellationToken cancellationToken = default) { _batches.TryGetValue(id, out var b); return Task.FromResult(b); }
    public Task<NeftBatch?> GetBatchByCycleIdAsync(string cycleId, CancellationToken cancellationToken = default) => Task.FromResult(_batches.Values.FirstOrDefault(b => string.Equals(b.CycleId, cycleId, StringComparison.Ordinal)));
    public Task<IReadOnlyList<NeftBatch>> GetBatchesAsync(DateOnly? date, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NeftBatch>>((!date.HasValue ? _batches.Values : _batches.Values.Where(b => b.SettlementDate == date.Value)).OrderByDescending(b => b.CreatedAt).ToList());
    public Task UpdateBatchAsync(NeftBatch batch, CancellationToken cancellationToken = default) { _batches[batch.Id] = batch; return Task.CompletedTask; }
}

// ============================================================
// SWIFT Message Repository
// ============================================================

public sealed class InMemorySwiftMessageRepository : ISwiftMessageRepository
{
    private readonly ConcurrentDictionary<Guid, SwiftMessage> _messages = new();

    public Task AddAsync(SwiftMessage message, CancellationToken cancellationToken = default) { _messages[message.Id] = message; return Task.CompletedTask; }
    public Task<SwiftMessage?> GetAsync(Guid id, CancellationToken cancellationToken = default) { _messages.TryGetValue(id, out var m); return Task.FromResult(m); }
    public Task<IReadOnlyList<SwiftMessage>> GetPendingAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SwiftMessage>>(_messages.Values.Where(m => m.Status == SwiftMessageStatus.Draft).OrderBy(m => m.CreatedAt).ToList());
    public Task UpdateAsync(SwiftMessage message, CancellationToken cancellationToken = default) { _messages[message.Id] = message; return Task.CompletedTask; }
}

// ============================================================
// ACH File Repository
// ============================================================

public sealed class InMemoryAchFileRepository : IAchFileRepository
{
    private readonly ConcurrentDictionary<Guid, AchFile> _files = new();

    public Task AddAsync(AchFile file, CancellationToken cancellationToken = default) { _files[file.Id] = file; return Task.CompletedTask; }
    public Task<AchFile?> GetAsync(Guid id, CancellationToken cancellationToken = default) { _files.TryGetValue(id, out var f); return Task.FromResult(f); }
    public Task<IReadOnlyList<AchFile>> GetAsync(DateOnly? effectiveDate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AchFile>>((!effectiveDate.HasValue ? _files.Values : _files.Values.Where(f => f.EffectiveDate == effectiveDate.Value)).OrderByDescending(f => f.CreatedAt).ToList());
    public Task UpdateAsync(AchFile file, CancellationToken cancellationToken = default) { _files[file.Id] = file; return Task.CompletedTask; }
}

// ============================================================
// Direct Debit Mandate Repository
// ============================================================

public sealed class InMemoryDirectDebitMandateRepository : IDirectDebitMandateRepository
{
    private readonly ConcurrentDictionary<Guid, DirectDebitMandate> _mandates = new();

    public Task AddAsync(DirectDebitMandate mandate, CancellationToken cancellationToken = default) { _mandates[mandate.Id] = mandate; return Task.CompletedTask; }
    public Task<DirectDebitMandate?> GetAsync(Guid id, CancellationToken cancellationToken = default) { _mandates.TryGetValue(id, out var m); return Task.FromResult(m); }
    public Task<IReadOnlyList<DirectDebitMandate>> GetForCustomerAsync(string customerNumber, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DirectDebitMandate>>(_mandates.Values.Where(m => string.Equals(m.CustomerNumber, customerNumber, StringComparison.OrdinalIgnoreCase)).OrderByDescending(m => m.CreatedAt).ToList());
    public Task<IReadOnlyList<DirectDebitMandate>> GetActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DirectDebitMandate>>(_mandates.Values.Where(m => m.Status == MandateStatus.Active).ToList());
    public Task UpdateAsync(DirectDebitMandate mandate, CancellationToken cancellationToken = default) { _mandates[mandate.Id] = mandate; return Task.CompletedTask; }
}

// ============================================================
// Chargeback Repository (with seeded reason codes)
// ============================================================

public sealed class InMemoryChargebackRepository : IChargebackRepository
{
    private readonly ConcurrentDictionary<Guid, ChargebackCase> _cases = new();
    private readonly ConcurrentDictionary<string, ChargebackReasonCode> _reasonCodes = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryChargebackRepository() { SeedReasonCodes(); }

    public Task AddCaseAsync(ChargebackCase c, CancellationToken ct = default) { _cases[c.Id] = c; return Task.CompletedTask; }
    public Task<ChargebackCase?> GetCaseAsync(Guid id, CancellationToken ct = default) { _cases.TryGetValue(id, out var c); return Task.FromResult(c); }
    public Task<IReadOnlyList<ChargebackCase>> GetCasesAsync(ChargebackCaseFilter filter, CancellationToken ct = default)
    {
        IEnumerable<ChargebackCase> q = _cases.Values;
        if (filter.Network.HasValue) q = q.Where(c => c.Network == filter.Network.Value);
        if (filter.Stage.HasValue) q = q.Where(c => c.Stage == filter.Stage.Value);
        if (filter.Outcome.HasValue) q = q.Where(c => c.Outcome == filter.Outcome.Value);
        if (filter.From.HasValue) q = q.Where(c => c.ChargebackReceivedDate >= filter.From.Value);
        if (filter.To.HasValue) q = q.Where(c => c.ChargebackReceivedDate <= filter.To.Value);
        return Task.FromResult<IReadOnlyList<ChargebackCase>>(q.OrderByDescending(c => c.CreatedAt).Take(filter.MaxRows).ToList());
    }
    public Task UpdateCaseAsync(ChargebackCase c, CancellationToken ct = default) { _cases[c.Id] = c; return Task.CompletedTask; }
    public Task<IReadOnlyList<ChargebackCase>> GetOverdueCasesAsync(DateOnly today, CancellationToken ct = default)
    {
        var overdue = _cases.Values.Where(c =>
            c.Stage != ChargebackStage.Resolved &&
            (c.Stage == ChargebackStage.Received && today > c.RepresentmentDeadline ||
             c.Stage == ChargebackStage.Represented && c.PreArbitrationDeadline.HasValue && today > c.PreArbitrationDeadline.Value ||
             c.Stage == ChargebackStage.PreArbitration && c.ArbitrationDeadline.HasValue && today > c.ArbitrationDeadline.Value)).ToList();
        return Task.FromResult<IReadOnlyList<ChargebackCase>>(overdue);
    }
    public Task<ChargebackReasonCode?> GetReasonCodeAsync(ChargebackNetwork network, string code, CancellationToken ct = default) => Task.FromResult(_reasonCodes.TryGetValue($"{network}:{code}", out var rc) ? rc : null);
    public Task<IReadOnlyList<ChargebackReasonCode>> GetReasonCodesAsync(ChargebackNetwork? network, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ChargebackReasonCode>>((!network.HasValue ? _reasonCodes.Values : _reasonCodes.Values.Where(r => r.Network == network.Value)).OrderBy(r => r.Code).ToList());
    public Task AddReasonCodeAsync(ChargebackReasonCode code, CancellationToken ct = default) { _reasonCodes[$"{code.Network}:{code.Code}"] = code; return Task.CompletedTask; }

    private void SeedReasonCodes()
    {
        void Add(ChargebackNetwork net, string code, string cat, string desc, int reprDays = 45, int preArbDays = 45, int arbDays = 10) =>
            _reasonCodes[$"{net}:{code}"] = new ChargebackReasonCode { Network = net, Code = code, Category = cat, Description = desc, RepresentmentDays = reprDays, PreArbitrationDays = preArbDays, ArbitrationDays = arbDays };

        // Visa reason codes
        Add(ChargebackNetwork.Visa, "10.4", "Fraud", "Other Fraud—Card Absent Environment", 120, 30, 10);
        Add(ChargebackNetwork.Visa, "10.5", "Fraud", "Visa Fraud Monitoring Program", 120, 30, 10);
        Add(ChargebackNetwork.Visa, "10.7", "Fraud", "Card Present Fraud—Other", 120, 30, 10);
        Add(ChargebackNetwork.Visa, "12.6", "Processing", "Duplicate Processing", 120, 30, 10);
        Add(ChargebackNetwork.Visa, "13.1", "Consumer", "Merchandise/Services Not Received", 120, 30, 10);
        Add(ChargebackNetwork.Visa, "13.3", "Consumer", "Not as Described", 120, 30, 10);
        Add(ChargebackNetwork.Visa, "13.7", "Consumer", "Cancelled Recurring Transaction", 120, 30, 10);

        // Mastercard reason codes
        Add(ChargebackNetwork.Mastercard, "4834", "Processing", "Point of Interaction Error", 120, 45, 10);
        Add(ChargebackNetwork.Mastercard, "4853", "Consumer", "Cardholder Dispute", 120, 45, 10);
        Add(ChargebackNetwork.Mastercard, "4855", "Consumer", "Goods or Services Not Provided", 120, 45, 10);
        Add(ChargebackNetwork.Mastercard, "4863", "Fraud", "Cardholder Does Not Recognize", 120, 45, 10);
        Add(ChargebackNetwork.Mastercard, "4871", "Fraud", "Chip/PIN Liability Shift", 120, 45, 10);

        // NIBSS / Verve
        Add(ChargebackNetwork.Nibss, "CBR-01", "Fraud", "Fraudulent Transaction", 90, 30, 10);
        Add(ChargebackNetwork.Nibss, "CBR-02", "Consumer", "Service/Goods Not Received", 90, 30, 10);
        Add(ChargebackNetwork.Nibss, "CBR-03", "Processing", "Wrong Amount Debited", 90, 30, 10);
        Add(ChargebackNetwork.Nibss, "CBR-04", "Processing", "Double Debit", 90, 30, 10);
        Add(ChargebackNetwork.Nibss, "CBR-05", "Consumer", "ATM Cash Not Dispensed", 90, 30, 10);
        Add(ChargebackNetwork.Verve, "CBR-01", "Fraud", "Fraudulent Transaction", 90, 30, 10);
        Add(ChargebackNetwork.Verve, "CBR-99", "Other", "Other Dispute", 90, 30, 10);
    }
}

// ============================================================
// Dispute Repository
// ============================================================

public sealed class InMemoryDisputeRepository : IDisputeRepository
{
    private readonly ConcurrentDictionary<Guid, CustomerDispute> _disputes = new();
    private readonly ConcurrentBag<EvidenceItem> _evidence = new();

    public Task AddDisputeAsync(CustomerDispute d, CancellationToken ct = default) { _disputes[d.Id] = d; return Task.CompletedTask; }
    public Task<CustomerDispute?> GetDisputeAsync(Guid id, CancellationToken ct = default) { _disputes.TryGetValue(id, out var d); return Task.FromResult(d); }
    public Task<IReadOnlyList<CustomerDispute>> GetDisputesAsync(DisputeFilter filter, CancellationToken ct = default)
    {
        IEnumerable<CustomerDispute> q = _disputes.Values;
        if (filter.DisputeType.HasValue) q = q.Where(d => d.DisputeType == filter.DisputeType.Value);
        if (filter.Status.HasValue) q = q.Where(d => d.Status == filter.Status.Value);
        if (!string.IsNullOrWhiteSpace(filter.CustomerNumber)) q = q.Where(d => string.Equals(d.CustomerNumber, filter.CustomerNumber, StringComparison.OrdinalIgnoreCase));
        if (filter.From.HasValue) q = q.Where(d => d.TransactionDate >= filter.From.Value);
        if (filter.To.HasValue) q = q.Where(d => d.TransactionDate <= filter.To.Value);
        return Task.FromResult<IReadOnlyList<CustomerDispute>>(q.OrderByDescending(d => d.ReceivedAt).Take(filter.MaxRows).ToList());
    }
    public Task UpdateDisputeAsync(CustomerDispute d, CancellationToken ct = default) { _disputes[d.Id] = d; return Task.CompletedTask; }
    public Task AddEvidenceAsync(EvidenceItem evidence, CancellationToken ct = default) { _evidence.Add(evidence); return Task.CompletedTask; }
    public Task<IReadOnlyList<EvidenceItem>> GetEvidenceAsync(Guid disputeId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<EvidenceItem>>(_evidence.Where(e => e.DisputeId == disputeId).OrderBy(e => e.SubmittedAt).ToList());
}

// ============================================================
// Reconciliation Repository
// ============================================================

public sealed class InMemoryReconciliationRepository : IReconciliationRepository
{
    private readonly ConcurrentDictionary<Guid, ReconciliationRun> _runs = new();
    private readonly ConcurrentDictionary<Guid, List<ReconciliationBreakItem>> _breaks = new();

    public Task AddRunAsync(ReconciliationRun run, IReadOnlyCollection<ReconciliationBreakItem> breaks, CancellationToken ct = default)
    {
        _runs[run.Id] = run;
        _breaks[run.Id] = breaks.ToList();
        return Task.CompletedTask;
    }
    public Task<ReconciliationRun?> GetRunAsync(Guid runId, CancellationToken ct = default) { _runs.TryGetValue(runId, out var r); return Task.FromResult(r); }
    public Task<ReconciliationRun?> GetLatestRunAsync(DateOnly businessDate, CancellationToken ct = default) => Task.FromResult(_runs.Values.Where(r => r.BusinessDate == businessDate).MaxBy(r => r.StartedAt));
    public Task<IReadOnlyList<ReconciliationRun>> GetRunsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        IEnumerable<ReconciliationRun> q = _runs.Values;
        if (from.HasValue) q = q.Where(r => r.BusinessDate >= from.Value);
        if (to.HasValue) q = q.Where(r => r.BusinessDate <= to.Value);
        return Task.FromResult<IReadOnlyList<ReconciliationRun>>(q.OrderByDescending(r => r.StartedAt).ToList());
    }
    public Task UpdateRunAsync(ReconciliationRun run, CancellationToken ct = default) { _runs[run.Id] = run; return Task.CompletedTask; }
    public Task<IReadOnlyList<ReconciliationBreakItem>> GetBreaksAsync(Guid runId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ReconciliationBreakItem>>(_breaks.TryGetValue(runId, out var b) ? b : new List<ReconciliationBreakItem>());
    public Task UpdateBreakAsync(ReconciliationBreakItem breakItem, CancellationToken ct = default)
    {
        if (_breaks.TryGetValue(breakItem.ReconciliationRunId, out var list))
        {
            var idx = list.FindIndex(b => b.Id == breakItem.Id);
            if (idx >= 0) list[idx] = breakItem;
        }
        return Task.CompletedTask;
    }
}
