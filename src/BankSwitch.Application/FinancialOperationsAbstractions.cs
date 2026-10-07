using BankSwitch.Domain;

namespace BankSwitch.Application;

public interface IFinancialOperationsService
{
    Task<CmsOperationResult<SettlementBatchResult>> ImportSettlementBatchAsync(ImportSettlementBatchRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<SettlementBatchResult>> ProcessSettlementBatchAsync(ProcessSettlementBatchRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<ReconciliationException>> ResolveReconciliationExceptionAsync(ResolveReconciliationExceptionRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<GlJournalResult>> PostGlJournalAsync(PostGlJournalRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<FinancialOperationResult>> CreateRefundAsync(CreateRefundRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<FinancialOperationResult>> CreateReversalAsync(CreateFinancialReversalRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<FinancialOperationResult>> CreateAdjustmentAsync(CreateFinancialAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<SettlementStatementResult>> GenerateAgencySettlementAsync(GenerateAgencySettlementRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<SettlementStatementResult>> GenerateCorporateSettlementAsync(GenerateCorporateSettlementRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReconciliationException>> GetOpenReconciliationExceptionsAsync(CancellationToken cancellationToken = default);

    // GL posting methods called via domain event handlers (A4)
    Task PostAuthorizationGlAsync(decimal purchaseAmount, decimal feeAmount, string currencyCode, string rrn, string correlationId, CancellationToken cancellationToken = default);
    Task PostTopUpGlAsync(decimal loadAmount, decimal feeAmount, string currencyCode, string reference, string correlationId, CancellationToken cancellationToken = default);

    // Settlement Engine — Outbound (B1 fix)
    /// <summary>
    /// Calculates the net settlement position for each institution/settlement-profile
    /// for the given business date and currency. For each position:
    ///   1. Groups cleared transactions by institution (via sink node settlement profile).
    ///   2. Computes gross purchases, gross refunds, fees, and net position.
    ///   3. Posts a Nostro GL entry (Dr Nostro / Cr Settlement Clearing for net debits,
    ///      or Dr Settlement Clearing / Cr Nostro for net credits).
    ///   4. Generates a settlement instruction file in ISO 20022 camt.054 format.
    /// </summary>
    Task<CmsOperationResult<IReadOnlyList<NetSettlementPosition>>> GenerateNetSettlementPositionsAsync(
        DateOnly businessDate,
        string currencyCode,
        string requestedBy,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NetSettlementPosition>> GetSettlementPositionsAsync(DateOnly? businessDate, CancellationToken cancellationToken = default);
}

public interface IFinancialOperationsRepository
{
    Task AddSettlementBatchAsync(SettlementBatch batch, IReadOnlyCollection<SettlementRecord> records, CancellationToken cancellationToken = default);
    Task<SettlementBatch?> GetSettlementBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<SettlementBatch?> GetSettlementBatchByReferenceAsync(string batchReference, CancellationToken cancellationToken = default);
    Task UpdateSettlementBatchAsync(SettlementBatch batch, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SettlementRecord>> GetSettlementRecordsAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task UpdateSettlementRecordAsync(SettlementRecord record, CancellationToken cancellationToken = default);

    Task AddReconciliationExceptionAsync(ReconciliationException exception, CancellationToken cancellationToken = default);
    Task<ReconciliationException?> GetReconciliationExceptionAsync(Guid exceptionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReconciliationException>> GetOpenReconciliationExceptionsAsync(CancellationToken cancellationToken = default);
    Task UpdateReconciliationExceptionAsync(ReconciliationException exception, CancellationToken cancellationToken = default);

    Task AddGlJournalAsync(GlJournalEntry journal, IReadOnlyCollection<GlJournalLine> lines, CancellationToken cancellationToken = default);
    Task<GlJournalEntry?> GetGlJournalAsync(Guid journalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlJournalLine>> GetGlJournalLinesAsync(Guid journalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlJournalEntry>> GetGlJournalsAsync(CancellationToken cancellationToken = default);

    Task AddFinancialOperationAsync(FinancialOperation operation, CancellationToken cancellationToken = default);
    Task UpdateFinancialOperationAsync(FinancialOperation operation, CancellationToken cancellationToken = default);
    Task<FinancialOperation?> GetFinancialOperationAsync(Guid operationId, CancellationToken cancellationToken = default);
    Task<bool> ExistsPostedFinancialOperationAsync(FinancialOperationType operationType, string originalRrn, string originalStan, string panHash, CancellationToken cancellationToken = default);

    Task AddSettlementStatementAsync(SettlementStatement statement, IReadOnlyCollection<SettlementStatementLine> lines, CancellationToken cancellationToken = default);
    Task<SettlementStatement?> GetSettlementStatementAsync(Guid statementId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SettlementStatementLine>> GetSettlementStatementLinesAsync(Guid statementId, CancellationToken cancellationToken = default);
}

public sealed record SettlementBatchResult(SettlementBatch Batch, IReadOnlyCollection<SettlementRecord> Records, IReadOnlyCollection<ReconciliationException> Exceptions);

public sealed record ImportSettlementBatchRequest(
    string BatchReference,
    string FileName,
    string SourceSystem,
    string CurrencyCode,
    DateOnly SettlementDate,
    string ImportedBy,
    IReadOnlyCollection<ImportSettlementRecordRequest> Records);

public sealed record ImportSettlementRecordRequest(
    string ExternalReference,
    string Rrn,
    string Stan,
    string MaskedPan,
    string PanHash,
    SettlementRecordType RecordType,
    decimal Amount,
    decimal FeeAmount,
    string CurrencyCode,
    DateTimeOffset TransactionDate,
    string Narrative);

public sealed record ProcessSettlementBatchRequest(Guid BatchId, string ProcessedBy, bool AutoPostGl = true);

public sealed record ResolveReconciliationExceptionRequest(
    Guid ExceptionId,
    string AssignedTo,
    string ResolutionNotes,
    ReconciliationExceptionStatus TargetStatus,
    string CorrelationId);

public sealed record GlJournalResult(GlJournalEntry Journal, IReadOnlyCollection<GlJournalLine> Lines);

public sealed record PostGlJournalRequest(
    string SourceModule,
    string Reference,
    string Narrative,
    string CurrencyCode,
    string CorrelationId,
    IReadOnlyCollection<PostGlJournalLineRequest> Lines);

public sealed record PostGlJournalLineRequest(string AccountCode, LedgerEntryDirection Direction, decimal Amount, string Narrative);

public sealed record FinancialOperationResult(FinancialOperation Operation, WalletAccount Wallet, CmsTransactionLog TransactionLog, GlJournalEntry? Journal);

public sealed record CreateRefundRequest(
    string OriginalRrn,
    string OriginalStan,
    string PanHash,
    decimal Amount,
    string CurrencyCode,
    string Reason,
    string TicketReference,
    string Maker,
    string Checker,
    string NewRrn,
    string NewStan,
    string CorrelationId,
    bool PostGl = true);

public sealed record CreateFinancialReversalRequest(
    string OriginalRrn,
    string OriginalStan,
    string PanHash,
    decimal Amount,
    string CurrencyCode,
    string Reason,
    string TicketReference,
    string Maker,
    string Checker,
    string NewRrn,
    string NewStan,
    string CorrelationId,
    bool ReverseFee = true,
    bool PostGl = true);

public sealed record CreateFinancialAdjustmentRequest(
    Guid CardId,
    FinancialAdjustmentDirection Direction,
    decimal Amount,
    string CurrencyCode,
    string Reason,
    string TicketReference,
    string Maker,
    string Checker,
    string Reference,
    string Stan,
    string CorrelationId,
    bool PostGl = true);

public sealed record GenerateAgencySettlementRequest(
    string AgencyCode,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string CurrencyCode,
    decimal CommissionRatePercent,
    string RequestedBy,
    bool PostGl = false);

public sealed record GenerateCorporateSettlementRequest(
    string CorporateCode,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string CurrencyCode,
    string RequestedBy,
    bool PostGl = false);

public sealed record SettlementStatementResult(SettlementStatement Statement, IReadOnlyCollection<SettlementStatementLine> Lines, GlJournalEntry? Journal);

/// <summary>Persistence for outbound net settlement positions.</summary>
public interface ISettlementPositionRepository
{
    Task AddPositionAsync(NetSettlementPosition position, CancellationToken cancellationToken = default);
    Task<NetSettlementPosition?> GetPositionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetSettlementPosition>> GetPositionsAsync(DateOnly? businessDate, CancellationToken cancellationToken = default);
    Task UpdatePositionAsync(NetSettlementPosition position, CancellationToken cancellationToken = default);
    Task<NetSettlementPosition?> GetPositionByProfileAndDateAsync(string settlementProfile, DateOnly businessDate, string currencyCode, CancellationToken cancellationToken = default);
}
