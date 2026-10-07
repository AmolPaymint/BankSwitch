using BankSwitch.Domain;

namespace BankSwitch.Application;

// ============================================================
// EFT Rail File Generators
// ============================================================

/// <summary>
/// Generates NPCI NEFT batch files (CRF1 fixed-width ASCII format).
/// Collects pending NEFT transfers, builds the batch file with
/// FILE_HEADER / BATCH_HEADER / TRANSACTION records / BATCH_TRAILER / FILE_TRAILER,
/// and marks transfers as SubmittedToRail.
/// </summary>
public interface INeftBatchGenerator
{
    Task<CmsOperationResult<NeftBatch>> GenerateBatchAsync(string cycleId, string memberId, DateOnly settlementDate, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<NeftBatch>> ProcessReturnFileAsync(string returnFileContent, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NeftBatch>> GetBatchesAsync(DateOnly? date, CancellationToken cancellationToken = default);
}

/// <summary>
/// Generates SWIFT FIN messages for RTGS high-value transfers.
/// MT103 = Single Customer Credit Transfer (direct customer payment).
/// MT202 = General Financial Institution Transfer (Nostro/correspondent settlement).
/// </summary>
public interface ISwiftMessageGenerator
{
    Task<CmsOperationResult<SwiftMessage>> GenerateMt103Async(Guid eftTransferId, string senderBic, string receiverBic, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<SwiftMessage>> GenerateMt202Async(Guid eftTransferId, string senderBic, string receiverBic, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<SwiftMessage>> MarkAcknowledgedAsync(Guid messageId, string ackReference, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SwiftMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Generates ACH batch files in NACHA format (94-char fixed-width).
/// Supports PPD (direct deposit / payroll), CCD (corporate credit/debit),
/// and return processing.
/// </summary>
public interface IAchBatchGenerator
{
    Task<CmsOperationResult<AchFile>> GenerateCreditBatchAsync(string companyId, string companyName, DateOnly effectiveDate, AchEntryType entryType, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<AchFile>> GenerateDebitBatchAsync(string mandateGroupCode, DateOnly effectiveDate, AchEntryType entryType, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<AchFile>> ProcessReturnBatchAsync(string returnFileContent, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AchFile>> GetFilesAsync(DateOnly? effectiveDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages NACH/ACH direct debit mandates (UMRN).
/// Mandates authorise recurring collections from customer accounts.
/// </summary>
public interface IDirectDebitMandateService
{
    Task<CmsOperationResult<DirectDebitMandate>> RegisterMandateAsync(RegisterMandateRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DirectDebitMandate>> ActivateMandateAsync(Guid mandateId, string umrn, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DirectDebitMandate>> SuspendMandateAsync(Guid mandateId, string reason, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DirectDebitMandate>> CancelMandateAsync(Guid mandateId, string reason, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectDebitMandate>> GetMandatesAsync(string customerNumber, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<DirectDebitMandate>> GetMandateAsync(Guid mandateId, CancellationToken cancellationToken = default);
}

// ============================================================
// Chargeback Service
// ============================================================

/// <summary>
/// Manages the full chargeback lifecycle from initial receipt (ISO 0422 / network
/// chargeback message) through representment, pre-arbitration, and arbitration.
/// Enforces Visa/Mastercard/NIBSS deadline calendaring.
/// </summary>
public interface IChargebackService
{
    Task<CmsOperationResult<ChargebackCase>> FileChargebackAsync(FileChargebackRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<ChargebackCase>> SubmitRepresentmentAsync(Guid caseId, string evidenceSummary, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<ChargebackCase>> ReceivePreArbitrationAsync(Guid caseId, string issuerResponse, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<ChargebackCase>> SubmitArbitrationAsync(Guid caseId, string arbitrationNotes, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<ChargebackCase>> ResolveAsync(Guid caseId, ChargebackOutcome outcome, string notes, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<ChargebackCase>> WithdrawAsync(Guid caseId, string reason, string actor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChargebackCase>> GetCasesAsync(ChargebackCaseFilter filter, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<ChargebackCase>> GetCaseAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChargebackCase>> GetOverdueCasesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChargebackReasonCode>> GetReasonCodesAsync(ChargebackNetwork? network, CancellationToken cancellationToken = default);
    Task<ChargebackReasonCode?> GetReasonCodeAsync(ChargebackNetwork network, string code, CancellationToken cancellationToken = default);
}

public interface IChargebackRepository
{
    Task AddCaseAsync(ChargebackCase chargebackCase, CancellationToken cancellationToken = default);
    Task<ChargebackCase?> GetCaseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChargebackCase>> GetCasesAsync(ChargebackCaseFilter filter, CancellationToken cancellationToken = default);
    Task UpdateCaseAsync(ChargebackCase chargebackCase, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChargebackCase>> GetOverdueCasesAsync(DateOnly today, CancellationToken cancellationToken = default);
    Task<ChargebackReasonCode?> GetReasonCodeAsync(ChargebackNetwork network, string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChargebackReasonCode>> GetReasonCodesAsync(ChargebackNetwork? network, CancellationToken cancellationToken = default);
    Task AddReasonCodeAsync(ChargebackReasonCode code, CancellationToken cancellationToken = default);
}

// ============================================================
// Dispute Management
// ============================================================

public interface IDisputeService
{
    Task<CmsOperationResult<CustomerDispute>> IntakeDisputeAsync(IntakeDisputeRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<EvidenceItem>> AddEvidenceAsync(Guid disputeId, AddEvidenceRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CustomerDispute>> UpdateStatusAsync(Guid disputeId, DisputeStatus newStatus, string notes, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CustomerDispute>> EscalateToChargebackAsync(Guid disputeId, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CustomerDispute>> ResolveAsync(Guid disputeId, DisputeStatus outcome, decimal? awardedAmount, string notes, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerDispute>> GetDisputesAsync(DisputeFilter filter, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CustomerDispute>> GetDisputeAsync(Guid disputeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvidenceItem>> GetEvidenceAsync(Guid disputeId, CancellationToken cancellationToken = default);
}

public interface IDisputeRepository
{
    Task AddDisputeAsync(CustomerDispute dispute, CancellationToken cancellationToken = default);
    Task<CustomerDispute?> GetDisputeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerDispute>> GetDisputesAsync(DisputeFilter filter, CancellationToken cancellationToken = default);
    Task UpdateDisputeAsync(CustomerDispute dispute, CancellationToken cancellationToken = default);
    Task AddEvidenceAsync(EvidenceItem evidence, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvidenceItem>> GetEvidenceAsync(Guid disputeId, CancellationToken cancellationToken = default);
}

// ============================================================
// Reconciliation Engine
// ============================================================

public interface IReconciliationEngine
{
    /// <summary>
    /// Runs a four-way reconciliation for the given business date:
    ///   1. Switch TransactionLog (approved transactions)
    ///   2. CMS LedgerEntries (cardholder wallet debits)
    ///   3. GL JournalLines (double-entry posting)
    ///   4. ClearingRecords (transactions included in clearing batches)
    /// Returns a ReconciliationRun summary with all breaks identified.
    /// </summary>
    Task<ReconciliationRun> ReconcileAsync(DateOnly businessDate, string triggeredBy, CancellationToken cancellationToken = default);

    Task<ReconciliationRun?> GetLatestRunAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReconciliationRun>> GetRunsAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReconciliationBreakItem>> GetBreaksAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<ReconciliationBreakItem>> ResolveBreakAsync(Guid breakId, string notes, string actor, CancellationToken cancellationToken = default);
}

public interface IReconciliationRepository
{
    Task AddRunAsync(ReconciliationRun run, IReadOnlyCollection<ReconciliationBreakItem> breaks, CancellationToken cancellationToken = default);
    Task<ReconciliationRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<ReconciliationRun?> GetLatestRunAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReconciliationRun>> GetRunsAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task UpdateRunAsync(ReconciliationRun run, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReconciliationBreakItem>> GetBreaksAsync(Guid runId, CancellationToken cancellationToken = default);
    Task UpdateBreakAsync(ReconciliationBreakItem breakItem, CancellationToken cancellationToken = default);
}

// ============================================================
// EFT Extended Repository
// ============================================================

public interface INeftBatchRepository
{
    Task AddBatchAsync(NeftBatch batch, CancellationToken cancellationToken = default);
    Task<NeftBatch?> GetBatchAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NeftBatch?> GetBatchByCycleIdAsync(string cycleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NeftBatch>> GetBatchesAsync(DateOnly? date, CancellationToken cancellationToken = default);
    Task UpdateBatchAsync(NeftBatch batch, CancellationToken cancellationToken = default);
}

public interface ISwiftMessageRepository
{
    Task AddAsync(SwiftMessage message, CancellationToken cancellationToken = default);
    Task<SwiftMessage?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SwiftMessage>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(SwiftMessage message, CancellationToken cancellationToken = default);
}

public interface IAchFileRepository
{
    Task AddAsync(AchFile file, CancellationToken cancellationToken = default);
    Task<AchFile?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AchFile>> GetAsync(DateOnly? effectiveDate, CancellationToken cancellationToken = default);
    Task UpdateAsync(AchFile file, CancellationToken cancellationToken = default);
}

public interface IDirectDebitMandateRepository
{
    Task AddAsync(DirectDebitMandate mandate, CancellationToken cancellationToken = default);
    Task<DirectDebitMandate?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectDebitMandate>> GetForCustomerAsync(string customerNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectDebitMandate>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(DirectDebitMandate mandate, CancellationToken cancellationToken = default);
}

// ============================================================
// Request / Response DTOs
// ============================================================

public sealed record RegisterMandateRequest(
    string CustomerNumber,
    Guid CustomerId,
    string DebtorAccountNumber,
    string DebtorIfscCode,
    string DebtorBankName,
    string CreditorAccountNumber,
    string CreditorIfscCode,
    string CreditorName,
    decimal MaximumAmount,
    string CurrencyCode,
    MandateFrequency Frequency,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record FileChargebackRequest(
    ChargebackNetwork Network,
    string OriginalTransactionCorrelationId,
    string Rrn,
    string Stan,
    string MaskedPan,
    string PanHash,
    decimal TransactionAmount,
    decimal ChargebackAmount,
    string CurrencyCode,
    DateOnly TransactionDate,
    string ReasonCode,
    string NetworkCaseId,
    string IssuerBin,
    string AcquirerBin,
    string MerchantId,
    string TerminalId,
    string CorrelationId);

public sealed record IntakeDisputeRequest(
    string CustomerNumber,
    Guid CustomerId,
    DisputeType DisputeType,
    string Rrn,
    string Stan,
    string MaskedPan,
    decimal DisputedAmount,
    string CurrencyCode,
    DateOnly TransactionDate,
    string MerchantName,
    string CustomerStatement,
    string Channel,
    string CorrelationId);

public sealed record AddEvidenceRequest(
    EvidenceType EvidenceType,
    string Description,
    string DocumentVaultReference,
    string SubmittedByRole);

public sealed record ChargebackCaseFilter(
    ChargebackNetwork? Network = null,
    ChargebackStage? Stage = null,
    ChargebackOutcome? Outcome = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int MaxRows = 100);

public sealed record DisputeFilter(
    DisputeType? DisputeType = null,
    DisputeStatus? Status = null,
    string? CustomerNumber = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int MaxRows = 100);

public sealed record ReconciliationOptions
{
    /// <summary>Run daily reconciliation at this UTC time (after clearing cut-off).</summary>
    public TimeSpan RunTime { get; init; } = new(23, 0, 0);
    public int WorkerIntervalMinutes { get; init; } = 30;
    /// <summary>Minimum amount difference (in minor units) to treat as a break. Default 0.01.</summary>
    public decimal AmountToleranceUnits { get; init; } = 0.01m;
}
