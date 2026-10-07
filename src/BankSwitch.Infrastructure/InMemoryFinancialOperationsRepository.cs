using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryFinancialOperationsRepository : IFinancialOperationsRepository
{
    private readonly ConcurrentDictionary<Guid, SettlementBatch> _batches = new();
    private readonly ConcurrentDictionary<string, Guid> _batchByReference = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, SettlementRecord> _records = new();
    private readonly ConcurrentDictionary<Guid, ReconciliationException> _exceptions = new();
    private readonly ConcurrentDictionary<Guid, GlJournalEntry> _journals = new();
    private readonly ConcurrentDictionary<Guid, List<GlJournalLine>> _journalLines = new();
    private readonly ConcurrentDictionary<Guid, FinancialOperation> _operations = new();
    private readonly ConcurrentDictionary<Guid, SettlementStatement> _statements = new();
    private readonly ConcurrentDictionary<Guid, List<SettlementStatementLine>> _statementLines = new();

    public Task AddSettlementBatchAsync(SettlementBatch batch, IReadOnlyCollection<SettlementRecord> records, CancellationToken cancellationToken = default)
    {
        _batches[batch.Id] = batch;
        _batchByReference[batch.BatchReference] = batch.Id;
        foreach (var record in records) _records[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task<SettlementBatch?> GetSettlementBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        _batches.TryGetValue(batchId, out var batch);
        return Task.FromResult(batch);
    }

    public Task<SettlementBatch?> GetSettlementBatchByReferenceAsync(string batchReference, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_batchByReference.TryGetValue(batchReference ?? string.Empty, out var id) && _batches.TryGetValue(id, out var batch) ? batch : null);
    }

    public Task UpdateSettlementBatchAsync(SettlementBatch batch, CancellationToken cancellationToken = default)
    {
        _batches[batch.Id] = batch;
        _batchByReference[batch.BatchReference] = batch.Id;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SettlementRecord>> GetSettlementRecordsAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SettlementRecord>>(_records.Values.Where(x => x.BatchId == batchId).OrderBy(x => x.TransactionDate).ThenBy(x => x.ExternalReference).ToList());
    }

    public Task UpdateSettlementRecordAsync(SettlementRecord record, CancellationToken cancellationToken = default)
    {
        _records[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task AddReconciliationExceptionAsync(ReconciliationException exception, CancellationToken cancellationToken = default)
    {
        _exceptions[exception.Id] = exception;
        return Task.CompletedTask;
    }

    public Task<ReconciliationException?> GetReconciliationExceptionAsync(Guid exceptionId, CancellationToken cancellationToken = default)
    {
        _exceptions.TryGetValue(exceptionId, out var exception);
        return Task.FromResult(exception);
    }

    public Task<IReadOnlyList<ReconciliationException>> GetOpenReconciliationExceptionsAsync(CancellationToken cancellationToken = default)
    {
        var open = _exceptions.Values
            .Where(x => x.Status is ReconciliationExceptionStatus.Open or ReconciliationExceptionStatus.Assigned or ReconciliationExceptionStatus.Escalated)
            .OrderBy(x => x.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<ReconciliationException>>(open);
    }

    public Task UpdateReconciliationExceptionAsync(ReconciliationException exception, CancellationToken cancellationToken = default)
    {
        _exceptions[exception.Id] = exception;
        return Task.CompletedTask;
    }

    public Task AddGlJournalAsync(GlJournalEntry journal, IReadOnlyCollection<GlJournalLine> lines, CancellationToken cancellationToken = default)
    {
        _journals[journal.Id] = journal;
        _journalLines[journal.Id] = lines.ToList();
        return Task.CompletedTask;
    }

    public Task<GlJournalEntry?> GetGlJournalAsync(Guid journalId, CancellationToken cancellationToken = default)
    {
        _journals.TryGetValue(journalId, out var journal);
        return Task.FromResult(journal);
    }

    public Task<IReadOnlyList<GlJournalLine>> GetGlJournalLinesAsync(Guid journalId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<GlJournalLine>>(_journalLines.TryGetValue(journalId, out var lines) ? lines : Array.Empty<GlJournalLine>());
    }

    public Task<IReadOnlyList<GlJournalEntry>> GetGlJournalsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GlJournalEntry>>(_journals.Values.OrderBy(j => j.CreatedAt).ToList());

    public Task AddFinancialOperationAsync(FinancialOperation operation, CancellationToken cancellationToken = default)
    {
        _operations[operation.Id] = operation;
        return Task.CompletedTask;
    }

    public Task UpdateFinancialOperationAsync(FinancialOperation operation, CancellationToken cancellationToken = default)
    {
        _operations[operation.Id] = operation;
        return Task.CompletedTask;
    }

    public Task<FinancialOperation?> GetFinancialOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        _operations.TryGetValue(operationId, out var operation);
        return Task.FromResult(operation);
    }

    public Task<bool> ExistsPostedFinancialOperationAsync(FinancialOperationType operationType, string originalRrn, string originalStan, string panHash, CancellationToken cancellationToken = default)
    {
        var exists = _operations.Values.Any(x => x.OperationType == operationType
            && x.Status == FinancialOperationStatus.Posted
            && string.Equals(x.OriginalRrn, originalRrn ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.OriginalStan, originalStan ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.PanHash, panHash ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task AddSettlementStatementAsync(SettlementStatement statement, IReadOnlyCollection<SettlementStatementLine> lines, CancellationToken cancellationToken = default)
    {
        _statements[statement.Id] = statement;
        _statementLines[statement.Id] = lines.ToList();
        return Task.CompletedTask;
    }

    public Task<SettlementStatement?> GetSettlementStatementAsync(Guid statementId, CancellationToken cancellationToken = default)
    {
        _statements.TryGetValue(statementId, out var statement);
        return Task.FromResult(statement);
    }

    public Task<IReadOnlyList<SettlementStatementLine>> GetSettlementStatementLinesAsync(Guid statementId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SettlementStatementLine>>(_statementLines.TryGetValue(statementId, out var lines) ? lines : Array.Empty<SettlementStatementLine>());
    }
}
