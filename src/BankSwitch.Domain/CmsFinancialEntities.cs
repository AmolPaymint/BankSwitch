namespace BankSwitch.Domain;

public enum SettlementBatchStatus { Imported, Processing, Reconciled, ExceptionsFound, Failed, Archived }

public enum SettlementRecordStatus { Imported, Matched, Exception, Posted, Ignored }

public enum SettlementRecordType { Purchase, Refund, Reversal, Fee, Chargeback, Adjustment }

public enum ReconciliationExceptionType { AuthorizationNotSettled, SettlementWithoutAuthorization, AmountMismatch, CurrencyMismatch, DuplicateSettlement, FeeMismatch, ReversalMismatch, RefundMismatch, AgencySettlementMismatch, CorporateFundingMismatch, Unknown }

public enum ReconciliationExceptionStatus { Open, Assigned, Resolved, WrittenOff, Escalated, Closed }

public enum FinancialOperationType { Refund, Reversal, Adjustment }

public enum FinancialOperationStatus { Pending, Approved, Posted, Rejected, Failed, Reversed }

public enum FinancialAdjustmentDirection { DebitCardholder, CreditCardholder }

public enum GlAccountType { Asset, Liability, Income, Expense, Clearing, Suspense }

//public enum GlJournalStatus{ Draft, Balanced, Posted, Reversed, Failed }
public enum GlJournalStatus{ Draft, Balanced, Posted, Reversed, Failed, Voided }

public enum SettlementPartyType { Agency, Corporate }

public enum SettlementStatementStatus { Generated, Approved, Posted, Paid, Exception, Archived }

public sealed record SettlementBatch : Entity
{
    public string BatchReference { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string SourceSystem { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public DateOnly SettlementDate { get; init; }
    public int RecordCount { get; init; }
    public decimal TotalDebitAmount { get; init; }
    public decimal TotalCreditAmount { get; init; }
    public SettlementBatchStatus Status { get; init; } = SettlementBatchStatus.Imported;
    public string ImportedBy { get; init; } = string.Empty;
    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; init; }
}

public sealed record SettlementRecord : Entity
{
    public Guid BatchId { get; init; }
    public string ExternalReference { get; init; } = string.Empty;
    public string Rrn { get; init; } = string.Empty;
    public string Stan { get; init; } = string.Empty;
    public string MaskedPan { get; init; } = string.Empty;
    public string PanHash { get; init; } = string.Empty;
    public SettlementRecordType RecordType { get; init; } = SettlementRecordType.Purchase;
    public decimal Amount { get; init; }
    public decimal FeeAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTimeOffset TransactionDate { get; init; } = DateTimeOffset.UtcNow;
    public SettlementRecordStatus Status { get; init; } = SettlementRecordStatus.Imported;
    public Guid? MatchedCmsTransactionId { get; init; }
    public string ResponseCode { get; init; } = string.Empty;
    public string Narrative { get; init; } = string.Empty;
}

public sealed record ReconciliationException : Entity
{
    public Guid? BatchId { get; init; }
    public Guid? SettlementRecordId { get; init; }
    public ReconciliationExceptionType ExceptionType { get; init; } = ReconciliationExceptionType.Unknown;
    public ReconciliationExceptionStatus Status { get; init; } = ReconciliationExceptionStatus.Open;
    public string Severity { get; init; } = "MEDIUM";
    public string CorrelationId { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public decimal ExpectedAmount { get; init; }
    public decimal ActualAmount { get; init; }
    public decimal DifferenceAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string AssignedTo { get; init; } = string.Empty;
    public string ResolutionNotes { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; init; }
}

public sealed record GlAccount : Entity
{
    public string AccountCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public GlAccountType AccountType { get; init; } = GlAccountType.Clearing;
    public string CurrencyCode { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}

public sealed record GlJournalEntry : Entity
{
    public string JournalNumber { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string SourceModule { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string Narrative { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal DebitTotal { get; init; }
    public decimal CreditTotal { get; init; }
    public GlJournalStatus Status { get; init; } = GlJournalStatus.Draft;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PostedAt { get; init; }

    // B4: Immutable ledger hash chain (ANSI X9.9 / PCIDSS audit trail)
    /// <summary>Business date this journal entry was posted to (YYYY-MM-DD).</summary>
    public DateOnly BusinessDate { get; init; }
    /// <summary>Sequential position in the hash chain for this journal (1-based, ever-increasing).</summary>
    public long ChainSequence { get; init; }
    /// <summary>SHA-256 hash of the previous journal entry in the chain. "GENESIS" for the first entry.</summary>
    public string PreviousHash { get; init; } = "GENESIS";
    /// <summary>
    /// SHA-256 hash of this entry's immutable content:
    /// SHA256(PreviousHash + JournalNumber + ChainSequence + DebitTotal + CreditTotal + PostedAt).
    /// A mismatch between stored and recomputed hash indicates tampering.
    /// </summary>
    public string EntryHash { get; init; } = string.Empty;
    /// <summary>Optional reversal link — set when this journal reverses a prior journal.</summary>
    public Guid? ReversesJournalId { get; init; }
    /// <summary>True when this entry has been voided and a reversal journal has been posted.</summary>
    public bool IsVoided { get; init; } = false;
}

// ============================================================
// GL Account Running Balances
// ============================================================

/// <summary>
/// Running balance for a GL account on a specific business date.
/// Maintained by the journal posting engine: every time a journal line
/// hits an account, this record is updated atomically.
/// Used for trial balance computation without scanning all journal lines.
/// </summary>
public sealed record GlAccountBalance : Entity
{
    public string AccountCode { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public DateOnly BalanceDate { get; init; }
    public decimal OpeningBalance { get; init; }   // Balance at start of day
    public decimal TotalDebits { get; init; }      // Sum of all debit postings today
    public decimal TotalCredits { get; init; }     // Sum of all credit postings today
    /// <summary>Closing balance = OpeningBalance + net movement (considering normal balance side).</summary>
    public decimal ClosingBalance { get; init; }
    public int JournalLineCount { get; init; }
    public DateTimeOffset LastUpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

// ============================================================
// GL Accounting Period (Business Day)
// ============================================================

public enum GlPeriodStatus { Open, Closing, Closed, Locked }

/// <summary>
/// An accounting period — in a payment switch context, this is typically one business day.
/// Journals may only be posted to Open periods. Once Closed, the trial balance is captured
/// and opening balances for the next period are set.
/// </summary>
public sealed record GlPeriod : Entity
{
    public DateOnly BusinessDate { get; init; }
    public GlPeriodStatus Status { get; init; } = GlPeriodStatus.Open;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal OpeningDebitTotal { get; init; }
    public decimal OpeningCreditTotal { get; init; }
    public decimal ClosingDebitTotal { get; init; }
    public decimal ClosingCreditTotal { get; init; }
    public int JournalCount { get; init; }
    public string OpenedBy { get; init; } = string.Empty;
    public string ClosedBy { get; init; } = string.Empty;
    public DateTimeOffset OpenedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; init; }
    /// <summary>SHA-256 hash of the trial balance snapshot (all account balances at close).</summary>
    public string PeriodCloseHash { get; init; } = string.Empty;
}

public sealed record GlJournalLine : Entity
{
    public Guid JournalEntryId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public LedgerEntryDirection Direction { get; init; }
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string Narrative { get; init; } = string.Empty;
}

public sealed record FinancialOperation : Entity
{
    public FinancialOperationType OperationType { get; init; }
    public FinancialOperationStatus Status { get; init; } = FinancialOperationStatus.Pending;
    public Guid? CardId { get; init; }
    public Guid? WalletAccountId { get; init; }
    public Guid? OriginalCmsTransactionId { get; init; }
    public string OriginalRrn { get; init; } = string.Empty;
    public string OriginalStan { get; init; } = string.Empty;
    public string NewRrn { get; init; } = string.Empty;
    public string NewStan { get; init; } = string.Empty;
    public string MaskedPan { get; init; } = string.Empty;
    public string PanHash { get; init; } = string.Empty;
    public FinancialAdjustmentDirection Direction { get; init; } = FinancialAdjustmentDirection.CreditCardholder;
    public decimal Amount { get; init; }
    public decimal FeeAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string TicketReference { get; init; } = string.Empty;
    public string Maker { get; init; } = string.Empty;
    public string Checker { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ApprovedAt { get; init; }
    public DateTimeOffset? PostedAt { get; init; }
}

public sealed record SettlementStatement : Entity
{
    public SettlementPartyType PartyType { get; init; }
    public Guid PartyId { get; init; }
    public string StatementNumber { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal GrossDebitAmount { get; init; }
    public decimal GrossCreditAmount { get; init; }
    public decimal FeeAmount { get; init; }
    public decimal CommissionAmount { get; init; }
    public decimal NetSettlementAmount { get; init; }
    public SettlementStatementStatus Status { get; init; } = SettlementStatementStatus.Generated;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PostedAt { get; init; }
}

public sealed record SettlementStatementLine : Entity
{
    public Guid SettlementStatementId { get; init; }
    public DateTimeOffset TransactionDate { get; init; }
    public string SourceType { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string Narrative { get; init; } = string.Empty;
    public LedgerEntryDirection Direction { get; init; }
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
}

// ============================================================
// Net Settlement Position (outbound settlement generation)
// ============================================================

public enum SettlementPositionDirection { NetDebit, NetCredit, Balanced }
public enum SettlementPositionStatus { Calculated, GlPosted, InstructionGenerated, Settled }

/// <summary>
/// Net settlement position calculated by the settlement engine for a specific
/// institution, currency, and business date. This is the switch's own outbound
/// settlement calculation — the counterpart to the imported SettlementBatch.
/// After calculation the engine posts a Nostro GL entry and generates a
/// settlement instruction file (ISO 20022 camt.054 or SWIFT MT940).
/// </summary>
public sealed record NetSettlementPosition : Entity
{
    public string InstitutionCode { get; init; } = string.Empty;
    public string SettlementProfile { get; init; } = string.Empty;
    public DateOnly BusinessDate { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal GrossPurchaseAmount { get; init; }
    public decimal GrossRefundAmount { get; init; }
    public decimal GrossFeeAmount { get; init; }
    public decimal NetSettlementAmount { get; init; }
    public SettlementPositionDirection Direction { get; init; }
    public SettlementPositionStatus Status { get; init; } = SettlementPositionStatus.Calculated;
    public int TransactionCount { get; init; }
    public Guid? NostroGlJournalId { get; init; }
    public string SettlementInstructionFile { get; init; } = string.Empty;
    public string NostroReference { get; init; } = string.Empty;
    public DateTimeOffset CalculatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? GlPostedAt { get; init; }
    public DateTimeOffset? InstructionGeneratedAt { get; init; }
}
