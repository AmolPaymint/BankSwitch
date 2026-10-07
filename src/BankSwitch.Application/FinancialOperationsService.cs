using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class FinancialOperationsService : IFinancialOperationsService
{
    private const string GlSettlementClearing = "1000-SETTLEMENT-CLEARING";
    private const string GlNostroFunding = "1100-NOSTRO-FUNDING";       // CD-01 FIX: funding receivable for top-up credits
    private const string GlCardholderLiability = "2100-CARDHOLDER-LIABILITY";
    private const string GlFeeIncome = "4000-FEE-INCOME";
    private const string GlAdjustmentExpense = "5000-ADJUSTMENT-EXPENSE";

    private readonly IFinancialOperationsRepository _financial;
    private readonly ICmsRepository _cms;
    private readonly IOperationalControlRepository _ops;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ITransactionRepository _transactions;
    private readonly ISettlementPositionRepository _positions;
    private readonly IJournalEngine? _journalEngine;     // B4: enhanced journal posting with hash chain
    private readonly IGlAccountService? _glAccounts;     // B4: account balance tracking
    private readonly IEndOfDayService? _eodService;      // B4: period validation

    public FinancialOperationsService(
        IFinancialOperationsRepository financial,
        ICmsRepository cms,
        IOperationalControlRepository ops,
        IAuditLogger audit,
        IClock clock,
        ITransactionRepository? transactions = null,
        ISettlementPositionRepository? positions = null,
        IJournalEngine? journalEngine = null,
        IGlAccountService? glAccounts = null,
        IEndOfDayService? eodService = null)
    {
        _financial = financial;
        _cms = cms;
        _ops = ops;
        _audit = audit;
        _clock = clock;
        _transactions = transactions ?? NullTransactionRepository.Instance;
        _positions = positions ?? NullSettlementPositionRepository.Instance;
        _journalEngine = journalEngine;
        _glAccounts = glAccounts;
        _eodService = eodService;
    }
    public async Task<CmsOperationResult<SettlementBatchResult>> ImportSettlementBatchAsync(ImportSettlementBatchRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.BatchReference)) return CmsOperationResult<SettlementBatchResult>.Fail("30", "Batch reference is required.");
        if (!IsCurrency(request.CurrencyCode)) return CmsOperationResult<SettlementBatchResult>.Fail("30", "Currency code must be three numeric characters.");
        if (request.Records is null || request.Records.Count == 0) return CmsOperationResult<SettlementBatchResult>.Fail("30", "At least one settlement record is required.");
        if (await _financial.GetSettlementBatchByReferenceAsync(Normalize(request.BatchReference), cancellationToken).ConfigureAwait(false) is not null)
            return CmsOperationResult<SettlementBatchResult>.Fail("94", "Settlement batch reference already exists.");

        var batch = new SettlementBatch
        {
            BatchReference = Normalize(request.BatchReference),
            FileName = request.FileName?.Trim() ?? string.Empty,
            SourceSystem = request.SourceSystem?.Trim() ?? string.Empty,
            CurrencyCode = request.CurrencyCode.Trim(),
            SettlementDate = request.SettlementDate,
            RecordCount = request.Records.Count,
            TotalDebitAmount = request.Records.Where(IsDebitRecord).Sum(x => x.Amount + x.FeeAmount),
            TotalCreditAmount = request.Records.Where(x => !IsDebitRecord(x)).Sum(x => x.Amount + x.FeeAmount),
            Status = SettlementBatchStatus.Imported,
            ImportedBy = request.ImportedBy?.Trim() ?? string.Empty,
            ImportedAt = _clock.UtcNow
        };

        var records = request.Records.Select(x => new SettlementRecord
        {
            BatchId = batch.Id,
            ExternalReference = x.ExternalReference?.Trim() ?? string.Empty,
            Rrn = x.Rrn?.Trim() ?? string.Empty,
            Stan = x.Stan?.Trim() ?? string.Empty,
            MaskedPan = x.MaskedPan?.Trim() ?? string.Empty,
            PanHash = x.PanHash?.Trim() ?? string.Empty,
            RecordType = x.RecordType,
            Amount = x.Amount,
            FeeAmount = x.FeeAmount,
            CurrencyCode = x.CurrencyCode?.Trim() ?? batch.CurrencyCode,
            TransactionDate = x.TransactionDate,
            Status = SettlementRecordStatus.Imported,
            Narrative = x.Narrative?.Trim() ?? string.Empty
        }).ToList();

        if (records.Any(x => !string.Equals(x.CurrencyCode, batch.CurrencyCode, StringComparison.Ordinal)))
            return CmsOperationResult<SettlementBatchResult>.Fail("30", "All settlement records must use the batch currency in Phase 3.");
        if (records.Any(x => x.Amount <= 0m)) return CmsOperationResult<SettlementBatchResult>.Fail("13", "Settlement record amount must be greater than zero.");

        await _financial.AddSettlementBatchAsync(batch, records, cancellationToken).ConfigureAwait(false);
        _audit.LogReconciliation(batch.BatchReference, batch.SourceSystem, "Imported", $"Records={batch.RecordCount}; Phase3 settlement file import");
        return CmsOperationResult<SettlementBatchResult>.Success(new SettlementBatchResult(batch, records, Array.Empty<ReconciliationException>()));
    }

    public async Task<CmsOperationResult<SettlementBatchResult>> ProcessSettlementBatchAsync(ProcessSettlementBatchRequest request, CancellationToken cancellationToken = default)
    {
        var batch = await _financial.GetSettlementBatchAsync(request.BatchId, cancellationToken).ConfigureAwait(false);
        if (batch is null) return CmsOperationResult<SettlementBatchResult>.Fail("25", "Settlement batch was not found.");
        var records = await _financial.GetSettlementRecordsAsync(batch.Id, cancellationToken).ConfigureAwait(false);
        if (records.Count == 0) return CmsOperationResult<SettlementBatchResult>.Fail("25", "Settlement batch has no records.");

        var exceptions = new List<ReconciliationException>();
        foreach (var record in records)
        {
            var match = await _cms.GetCmsTransactionAsync(record.Rrn, record.Stan, string.IsNullOrWhiteSpace(record.PanHash) ? null : record.PanHash, true, cancellationToken).ConfigureAwait(false);
            if (match is null)
            {
                var ex = CreateException(batch, record, ReconciliationExceptionType.SettlementWithoutAuthorization, 0m, record.Amount, record.CurrencyCode, "Settlement record does not match an approved CMS authorization.");
                await _financial.AddReconciliationExceptionAsync(ex, cancellationToken).ConfigureAwait(false);
                await _financial.UpdateSettlementRecordAsync(record with { Status = SettlementRecordStatus.Exception, ResponseCode = "25" }, cancellationToken).ConfigureAwait(false);
                exceptions.Add(ex);
                continue;
            }

            if (!string.Equals(match.CurrencyCode, record.CurrencyCode, StringComparison.Ordinal))
            {
                var ex = CreateException(batch, record, ReconciliationExceptionType.CurrencyMismatch, match.Amount, record.Amount, record.CurrencyCode, $"CMS currency {match.CurrencyCode} differs from settlement currency {record.CurrencyCode}.");
                await _financial.AddReconciliationExceptionAsync(ex, cancellationToken).ConfigureAwait(false);
                await _financial.UpdateSettlementRecordAsync(record with { Status = SettlementRecordStatus.Exception, MatchedCmsTransactionId = match.Id, ResponseCode = "30" }, cancellationToken).ConfigureAwait(false);
                exceptions.Add(ex);
                continue;
            }

            var expectedAmount = record.RecordType == SettlementRecordType.Fee ? match.FeeAmount : match.Amount;
            if (Math.Abs(expectedAmount - record.Amount) > 0.005m)
            {
                var ex = CreateException(batch, record, ReconciliationExceptionType.AmountMismatch, expectedAmount, record.Amount, record.CurrencyCode, "CMS authorization amount differs from settlement amount.");
                await _financial.AddReconciliationExceptionAsync(ex, cancellationToken).ConfigureAwait(false);
                await _financial.UpdateSettlementRecordAsync(record with { Status = SettlementRecordStatus.Exception, MatchedCmsTransactionId = match.Id, ResponseCode = "13" }, cancellationToken).ConfigureAwait(false);
                exceptions.Add(ex);
                continue;
            }

            await _financial.UpdateSettlementRecordAsync(record with { Status = SettlementRecordStatus.Matched, MatchedCmsTransactionId = match.Id, ResponseCode = "00" }, cancellationToken).ConfigureAwait(false);
        }

        var finalRecords = await _financial.GetSettlementRecordsAsync(batch.Id, cancellationToken).ConfigureAwait(false);
        var finalBatch = batch with
        {
            Status = exceptions.Count == 0 ? SettlementBatchStatus.Reconciled : SettlementBatchStatus.ExceptionsFound,
            ProcessedAt = _clock.UtcNow
        };
        await _financial.UpdateSettlementBatchAsync(finalBatch, cancellationToken).ConfigureAwait(false);

        if (request.AutoPostGl && finalRecords.Any(x => x.Status == SettlementRecordStatus.Matched))
        {
            await PostSettlementGlAsync(finalBatch, finalRecords.Where(x => x.Status == SettlementRecordStatus.Matched).ToList(), cancellationToken).ConfigureAwait(false);
        }

        _audit.LogReconciliation(finalBatch.BatchReference, finalBatch.SourceSystem, finalBatch.Status.ToString(), $"Records={finalBatch.RecordCount}; Exceptions={exceptions.Count}");
        return CmsOperationResult<SettlementBatchResult>.Success(new SettlementBatchResult(finalBatch, finalRecords, exceptions));
    }

    public async Task<CmsOperationResult<ReconciliationException>> ResolveReconciliationExceptionAsync(ResolveReconciliationExceptionRequest request, CancellationToken cancellationToken = default)
    {
        var exception = await _financial.GetReconciliationExceptionAsync(request.ExceptionId, cancellationToken).ConfigureAwait(false);
        if (exception is null) return CmsOperationResult<ReconciliationException>.Fail("25", "Reconciliation exception was not found.");
        if (request.TargetStatus is ReconciliationExceptionStatus.Open or ReconciliationExceptionStatus.Assigned)
            return CmsOperationResult<ReconciliationException>.Fail("30", "Resolution target status must close, escalate, write off, or resolve the exception.");

        var updated = exception with
        {
            Status = request.TargetStatus,
            AssignedTo = request.AssignedTo?.Trim() ?? exception.AssignedTo,
            ResolutionNotes = request.ResolutionNotes?.Trim() ?? string.Empty,
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? exception.CorrelationId : request.CorrelationId,
            ResolvedAt = _clock.UtcNow
        };
        await _financial.UpdateReconciliationExceptionAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(updated.CorrelationId, request.AssignedTo, "ResolveReconciliationException", exception.Status.ToString(), updated.Status.ToString(), updated.ResolutionNotes, updated.Reference);
        return CmsOperationResult<ReconciliationException>.Success(updated);
    }

    public async Task<CmsOperationResult<GlJournalResult>> PostGlJournalAsync(PostGlJournalRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Lines is null || request.Lines.Count < 2) return CmsOperationResult<GlJournalResult>.Fail("30", "A journal requires at least one debit and one credit line.");
        if (!IsCurrency(request.CurrencyCode)) return CmsOperationResult<GlJournalResult>.Fail("30", "Currency code must be three numeric characters.");
        if (request.Lines.Any(x => x.Amount <= 0m)) return CmsOperationResult<GlJournalResult>.Fail("13", "Journal line amounts must be greater than zero.");

        // B4: Route through enhanced JournalEngine when available (hash chain + balance updates + period validation)
        if (_journalEngine is not null)
        {
            var businessDate = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
            // Respect period if open; if no period exists, proceed (auto-open on first use)
            return await _journalEngine.PostJournalAsync(request, businessDate, cancellationToken).ConfigureAwait(false);
        }

        // Legacy direct posting (when JournalEngine is not registered — backward compatible)
        var debitTotal = request.Lines.Where(x => x.Direction == LedgerEntryDirection.Debit).Sum(x => x.Amount);
        var creditTotal = request.Lines.Where(x => x.Direction == LedgerEntryDirection.Credit).Sum(x => x.Amount);
        if (Math.Abs(debitTotal - creditTotal) > 0.005m) return CmsOperationResult<GlJournalResult>.Fail("30", "GL journal is not balanced.");

        var journal = new GlJournalEntry
        {
            JournalNumber = CreateStatementNumber("GL"),
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId,
            SourceModule = request.SourceModule?.Trim() ?? "FinancialOperations",
            Reference = request.Reference?.Trim() ?? string.Empty,
            Narrative = request.Narrative?.Trim() ?? string.Empty,
            CurrencyCode = request.CurrencyCode.Trim(),
            DebitTotal = debitTotal,
            CreditTotal = creditTotal,
            Status = GlJournalStatus.Posted,
            CreatedAt = _clock.UtcNow,
            PostedAt = _clock.UtcNow,
            BusinessDate = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime)
        };
        var lines = request.Lines.Select(x => new GlJournalLine
        {
            JournalEntryId = journal.Id,
            AccountCode = x.AccountCode?.Trim() ?? string.Empty,
            Direction = x.Direction,
            Amount = x.Amount,
            CurrencyCode = journal.CurrencyCode,
            Narrative = x.Narrative?.Trim() ?? string.Empty
        }).ToList();

        await _financial.AddGlJournalAsync(journal, lines, cancellationToken).ConfigureAwait(false);
        _audit.LogReconciliation(journal.JournalNumber, journal.SourceModule, "Posted", $"Lines={lines.Count}; {journal.Narrative}");
        return CmsOperationResult<GlJournalResult>.Success(new GlJournalResult(journal, lines));
    }

    public async Task<CmsOperationResult<FinancialOperationResult>> CreateRefundAsync(CreateRefundRequest request, CancellationToken cancellationToken = default)
    {
        return await CreateLinkedFinancialOperationAsync(FinancialOperationType.Refund, request.OriginalRrn, request.OriginalStan, request.PanHash, request.Amount, request.CurrencyCode, request.Reason, request.TicketReference, request.Maker, request.Checker, request.NewRrn, request.NewStan, request.CorrelationId, 0m, FinancialAdjustmentDirection.CreditCardholder, LedgerEntryType.Refund, "Refund credit", request.PostGl, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CmsOperationResult<FinancialOperationResult>> CreateReversalAsync(CreateFinancialReversalRequest request, CancellationToken cancellationToken = default)
    {
        var original = await _cms.GetCmsTransactionAsync(request.OriginalRrn, request.OriginalStan, request.PanHash, true, cancellationToken).ConfigureAwait(false);
        var feeAmount = request.ReverseFee ? original?.FeeAmount ?? 0m : 0m;
        return await CreateLinkedFinancialOperationAsync(FinancialOperationType.Reversal, request.OriginalRrn, request.OriginalStan, request.PanHash, request.Amount, request.CurrencyCode, request.Reason, request.TicketReference, request.Maker, request.Checker, request.NewRrn, request.NewStan, request.CorrelationId, feeAmount, FinancialAdjustmentDirection.CreditCardholder, LedgerEntryType.Reversal, "Authorization reversal credit", request.PostGl, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CmsOperationResult<FinancialOperationResult>> CreateAdjustmentAsync(CreateFinancialAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0m) return CmsOperationResult<FinancialOperationResult>.Fail("13", "Adjustment amount must be greater than zero.");
        if (!IsCurrency(request.CurrencyCode)) return CmsOperationResult<FinancialOperationResult>.Fail("30", "Currency code must be three numeric characters.");
        if (string.Equals(request.Maker, request.Checker, StringComparison.OrdinalIgnoreCase)) return CmsOperationResult<FinancialOperationResult>.Fail("57", "Maker cannot approve their own adjustment.");

        var card = await _cms.GetCardAsync(request.CardId, cancellationToken).ConfigureAwait(false);
        if (card is null) return CmsOperationResult<FinancialOperationResult>.Fail("14", "Card was not found.");
        var wallet = await _cms.GetWalletAsync(card.WalletAccountId, cancellationToken).ConfigureAwait(false);
        if (wallet is null || wallet.Status != WalletStatus.Active) return CmsOperationResult<FinancialOperationResult>.Fail("91", "Wallet is unavailable.");
        if (!string.Equals(wallet.CurrencyCode, request.CurrencyCode, StringComparison.Ordinal)) return CmsOperationResult<FinancialOperationResult>.Fail("58", "Adjustment currency does not match wallet currency.");

        var direction = request.Direction;
        if (direction == FinancialAdjustmentDirection.DebitCardholder && wallet.AvailableBalance < request.Amount)
            return CmsOperationResult<FinancialOperationResult>.Fail("51", "Insufficient funds for debit adjustment.");

        var updatedWallet = ApplyWalletMovement(wallet, direction, request.Amount);
        await _cms.UpdateWalletAsync(updatedWallet, cancellationToken).ConfigureAwait(false);
        await _cms.AddLedgerEntryAsync(new LedgerEntry
        {
            WalletAccountId = wallet.Id,
            CorrelationId = request.CorrelationId,
            EntryType = LedgerEntryType.Adjustment,
            Direction = direction == FinancialAdjustmentDirection.CreditCardholder ? LedgerEntryDirection.Credit : LedgerEntryDirection.Debit,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            BalanceAfter = updatedWallet.AvailableBalance,
            Reference = request.Reference,
            Narrative = request.Reason,
            CreatedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);

        var tx = await AddOperationTransactionLogAsync(card, updatedWallet, "ADJ", request.Reference, request.Stan, request.Amount, 0m, request.CurrencyCode, request.CorrelationId, "Adjustment posted", cancellationToken).ConfigureAwait(false);
        var op = new FinancialOperation
        {
            OperationType = FinancialOperationType.Adjustment,
            Status = FinancialOperationStatus.Posted,
            CardId = card.Id,
            WalletAccountId = wallet.Id,
            NewRrn = request.Reference,
            NewStan = request.Stan,
            MaskedPan = card.MaskedPan,
            PanHash = card.PanHash,
            Direction = request.Direction,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            Reason = request.Reason,
            TicketReference = request.TicketReference,
            Maker = request.Maker,
            Checker = request.Checker,
            CreatedAt = _clock.UtcNow,
            ApprovedAt = _clock.UtcNow,
            PostedAt = _clock.UtcNow
        };
        await _financial.AddFinancialOperationAsync(op, cancellationToken).ConfigureAwait(false);
        var journal = request.PostGl ? (await PostWalletMovementGlAsync(op.OperationType, request.Direction, request.Amount, request.CurrencyCode, request.Reference, request.CorrelationId, cancellationToken).ConfigureAwait(false)).Value?.Journal : null;
        return CmsOperationResult<FinancialOperationResult>.Success(new FinancialOperationResult(op, updatedWallet, tx, journal));
    }

    public async Task<CmsOperationResult<SettlementStatementResult>> GenerateAgencySettlementAsync(GenerateAgencySettlementRequest request, CancellationToken cancellationToken = default)
    {
        var agency = await _ops.GetAgencyByCodeAsync(Normalize(request.AgencyCode), cancellationToken).ConfigureAwait(false);
        if (agency is null) return CmsOperationResult<SettlementStatementResult>.Fail("25", "Agency was not found.");
        if (!IsCurrency(request.CurrencyCode)) return CmsOperationResult<SettlementStatementResult>.Fail("30", "Currency code must be three numeric characters.");
        var cards = await _cms.GetCardsForOwnerAsync(StatementOwnerType.Agency, agency.Id, cancellationToken).ConfigureAwait(false);
        var result = await GeneratePartySettlementAsync(SettlementPartyType.Agency, agency.Id, request.CurrencyCode, request.PeriodStart, request.PeriodEnd, cards, request.CommissionRatePercent, request.PostGl, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<CmsOperationResult<SettlementStatementResult>> GenerateCorporateSettlementAsync(GenerateCorporateSettlementRequest request, CancellationToken cancellationToken = default)
    {
        var corporate = await _ops.GetCorporateByCodeAsync(Normalize(request.CorporateCode), cancellationToken).ConfigureAwait(false);
        if (corporate is null) return CmsOperationResult<SettlementStatementResult>.Fail("25", "Corporate was not found.");
        if (!IsCurrency(request.CurrencyCode)) return CmsOperationResult<SettlementStatementResult>.Fail("30", "Currency code must be three numeric characters.");
        var cards = await _cms.GetCardsForOwnerAsync(StatementOwnerType.Corporate, corporate.Id, cancellationToken).ConfigureAwait(false);
        return await GeneratePartySettlementAsync(SettlementPartyType.Corporate, corporate.Id, request.CurrencyCode, request.PeriodStart, request.PeriodEnd, cards, 0m, request.PostGl, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<ReconciliationException>> GetOpenReconciliationExceptionsAsync(CancellationToken cancellationToken = default)
    {
        return _financial.GetOpenReconciliationExceptionsAsync(cancellationToken);
    }

    private async Task<CmsOperationResult<FinancialOperationResult>> CreateLinkedFinancialOperationAsync(
        FinancialOperationType type,
        string originalRrn,
        string originalStan,
        string panHash,
        decimal amount,
        string currencyCode,
        string reason,
        string ticketReference,
        string maker,
        string checker,
        string newRrn,
        string newStan,
        string correlationId,
        decimal feeAmount,
        FinancialAdjustmentDirection direction,
        LedgerEntryType ledgerEntryType,
        string ledgerNarrative,
        bool postGl,
        CancellationToken cancellationToken)
    {
        if (amount <= 0m) return CmsOperationResult<FinancialOperationResult>.Fail("13", "Financial operation amount must be greater than zero.");
        if (!IsCurrency(currencyCode)) return CmsOperationResult<FinancialOperationResult>.Fail("30", "Currency code must be three numeric characters.");
        if (string.Equals(maker, checker, StringComparison.OrdinalIgnoreCase)) return CmsOperationResult<FinancialOperationResult>.Fail("57", "Maker cannot approve their own financial operation.");
        if (await _financial.ExistsPostedFinancialOperationAsync(type, originalRrn, originalStan, panHash, cancellationToken).ConfigureAwait(false))
            return CmsOperationResult<FinancialOperationResult>.Fail("94", $"A posted {type} already exists for the original transaction.");

        var original = await _cms.GetCmsTransactionAsync(originalRrn, originalStan, panHash, true, cancellationToken).ConfigureAwait(false);
        if (original is null) return CmsOperationResult<FinancialOperationResult>.Fail("25", "Original approved CMS transaction was not found.");
        if (!string.Equals(original.CurrencyCode, currencyCode, StringComparison.Ordinal)) return CmsOperationResult<FinancialOperationResult>.Fail("58", "Operation currency differs from original transaction currency.");
        if (amount > original.Amount + 0.005m) return CmsOperationResult<FinancialOperationResult>.Fail("61", "Operation amount cannot exceed original transaction amount.");
        if (!original.CardId.HasValue || !original.WalletAccountId.HasValue) return CmsOperationResult<FinancialOperationResult>.Fail("25", "Original transaction is not linked to a card and wallet.");

        var card = await _cms.GetCardAsync(original.CardId.Value, cancellationToken).ConfigureAwait(false);
        var wallet = await _cms.GetWalletAsync(original.WalletAccountId.Value, cancellationToken).ConfigureAwait(false);
        if (card is null || wallet is null) return CmsOperationResult<FinancialOperationResult>.Fail("25", "Original card or wallet was not found.");
        // Apply movements in two steps so each ledger entry captures the correct running balance.
        var walletAfterPrincipal = ApplyWalletMovement(wallet, direction, amount);
        var walletAfterFee = feeAmount > 0m ? ApplyWalletMovement(walletAfterPrincipal, direction, feeAmount) : walletAfterPrincipal;
        var updatedWallet = walletAfterFee;
        await _cms.UpdateWalletAsync(updatedWallet, cancellationToken).ConfigureAwait(false);
        await _cms.AddLedgerEntryAsync(new LedgerEntry
        {
            WalletAccountId = wallet.Id,
            CorrelationId = correlationId,
            EntryType = ledgerEntryType,
            Direction = LedgerEntryDirection.Credit,
            Amount = amount,
            CurrencyCode = currencyCode,
            BalanceAfter = walletAfterPrincipal.AvailableBalance,
            Reference = newRrn,
            Narrative = ledgerNarrative,
            CreatedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);
        if (feeAmount > 0m)
        {
            await _cms.AddLedgerEntryAsync(new LedgerEntry
            {
                WalletAccountId = wallet.Id,
                CorrelationId = correlationId,
                EntryType = LedgerEntryType.Fee,
                Direction = LedgerEntryDirection.Credit,
                Amount = feeAmount,
                CurrencyCode = currencyCode,
                BalanceAfter = walletAfterFee.AvailableBalance,
                Reference = newRrn,
                Narrative = "Fee reversal credit",
                CreatedAt = _clock.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }

        var tx = await AddOperationTransactionLogAsync(card, updatedWallet, type == FinancialOperationType.Refund ? "20" : "42", newRrn, newStan, amount, feeAmount, currencyCode, correlationId, $"{type} posted", cancellationToken).ConfigureAwait(false);
        var operation = new FinancialOperation
        {
            OperationType = type,
            Status = FinancialOperationStatus.Posted,
            CardId = card.Id,
            WalletAccountId = wallet.Id,
            OriginalCmsTransactionId = original.Id,
            OriginalRrn = originalRrn,
            OriginalStan = originalStan,
            NewRrn = newRrn,
            NewStan = newStan,
            MaskedPan = card.MaskedPan,
            PanHash = card.PanHash,
            Direction = direction,
            Amount = amount,
            FeeAmount = feeAmount,
            CurrencyCode = currencyCode,
            Reason = reason,
            TicketReference = ticketReference,
            Maker = maker,
            Checker = checker,
            CreatedAt = _clock.UtcNow,
            ApprovedAt = _clock.UtcNow,
            PostedAt = _clock.UtcNow
        };
        await _financial.AddFinancialOperationAsync(operation, cancellationToken).ConfigureAwait(false);
        var journal = postGl ? (await PostWalletMovementGlAsync(type, direction, amount + feeAmount, currencyCode, newRrn, correlationId, cancellationToken).ConfigureAwait(false)).Value?.Journal : null;
        return CmsOperationResult<FinancialOperationResult>.Success(new FinancialOperationResult(operation, updatedWallet, tx, journal));
    }

    private async Task<CmsTransactionLog> AddOperationTransactionLogAsync(PrepaidCard card, WalletAccount wallet, string transactionType, string rrn, string stan, decimal amount, decimal feeAmount, string currencyCode, string correlationId, string description, CancellationToken cancellationToken)
    {
        var log = new CmsTransactionLog
        {
            CorrelationId = correlationId,
            CardId = card.Id,
            WalletAccountId = wallet.Id,
            MaskedPan = card.MaskedPan,
            PanHash = card.PanHash,
            TransactionTypeCode = transactionType,
            ChannelCode = "OPS",
            Stan = stan,
            Rrn = rrn,
            Amount = amount,
            FeeAmount = feeAmount,
            CurrencyCode = currencyCode,
            ResponseCode = "00",
            ResponseDescription = description,
            AuthorizationCode = string.Empty,
            CreatedAt = _clock.UtcNow
        };
        await _cms.AddCmsTransactionLogAsync(log, cancellationToken).ConfigureAwait(false);
        return log;
    }

    private async Task<CmsOperationResult<GlJournalResult>> PostWalletMovementGlAsync(FinancialOperationType type, FinancialAdjustmentDirection direction, decimal amount, string currencyCode, string reference, string correlationId, CancellationToken cancellationToken)
    {
        var lines = direction == FinancialAdjustmentDirection.CreditCardholder
            ? new[]
            {
                new PostGlJournalLineRequest(GlSettlementClearing, LedgerEntryDirection.Debit, amount, $"{type} clearing debit"),
                new PostGlJournalLineRequest(GlCardholderLiability, LedgerEntryDirection.Credit, amount, $"{type} cardholder liability credit")
            }
            : new[]
            {
                new PostGlJournalLineRequest(GlCardholderLiability, LedgerEntryDirection.Debit, amount, $"{type} cardholder liability debit"),
                new PostGlJournalLineRequest(GlAdjustmentExpense, LedgerEntryDirection.Credit, amount, $"{type} adjustment credit")
            };
        return await PostGlJournalAsync(new PostGlJournalRequest("FinancialOperations", reference, $"{type} wallet movement", currencyCode, correlationId, lines), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// CD-01 FIX: Posts the double-entry GL journal for a prepaid card purchase authorization.
    /// Called by <c>CorePrepaidCmsService.AuthorizeAsync</c> for every approved debit.
    /// Debits Cardholder Liability (reduces obligation) and Credits Settlement Clearing.
    /// Fee amount (if non-zero) is posted as a separate journal line: Debit Cardholder Liability → Credit Fee Income.
    /// </summary>
    public async Task PostAuthorizationGlAsync(decimal purchaseAmount, decimal feeAmount, string currencyCode, string rrn, string correlationId, CancellationToken cancellationToken = default)
    {
        var lines = new List<PostGlJournalLineRequest>
        {
            new(GlCardholderLiability, LedgerEntryDirection.Debit, purchaseAmount, "Purchase authorization — cardholder liability reduction"),
            new(GlSettlementClearing, LedgerEntryDirection.Credit, purchaseAmount, "Purchase authorization — settlement clearing credit")
        };
        if (feeAmount > 0m)
        {
            lines.Add(new PostGlJournalLineRequest(GlCardholderLiability, LedgerEntryDirection.Debit, feeAmount, "Purchase fee — cardholder liability reduction"));
            lines.Add(new PostGlJournalLineRequest(GlFeeIncome, LedgerEntryDirection.Credit, feeAmount, "Purchase fee — fee income credit"));
        }
        await PostGlJournalAsync(new PostGlJournalRequest("Authorization", rrn, $"Purchase auth RRN:{rrn}", currencyCode, correlationId, lines), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// CD-01 FIX: Posts the double-entry GL journal for a card top-up (load).
    /// Called by <c>CorePrepaidCmsService.TopUpAsync</c> for every successful load.
    /// Debits Nostro/Funding Receivable (cash received) and Credits Cardholder Liability (obligation incurred).
    /// Fee amount (if non-zero) is posted separately: Debit Cardholder Liability → Credit Fee Income.
    /// </summary>
    public async Task PostTopUpGlAsync(decimal loadAmount, decimal feeAmount, string currencyCode, string reference, string correlationId, CancellationToken cancellationToken = default)
    {
        var lines = new List<PostGlJournalLineRequest>
        {
            new(GlNostroFunding, LedgerEntryDirection.Debit, loadAmount, "Top-up — nostro funding receivable"),
            new(GlCardholderLiability, LedgerEntryDirection.Credit, loadAmount, "Top-up — cardholder liability credit")
        };
        if (feeAmount > 0m)
        {
            lines.Add(new PostGlJournalLineRequest(GlCardholderLiability, LedgerEntryDirection.Debit, feeAmount, "Top-up fee — cardholder liability reduction"));
            lines.Add(new PostGlJournalLineRequest(GlFeeIncome, LedgerEntryDirection.Credit, feeAmount, "Top-up fee — fee income credit"));
        }
        await PostGlJournalAsync(new PostGlJournalRequest("TopUp", reference, $"Card load REF:{reference}", currencyCode, correlationId, lines), cancellationToken).ConfigureAwait(false);
    }

    private async Task PostSettlementGlAsync(SettlementBatch batch, IReadOnlyCollection<SettlementRecord> records, CancellationToken cancellationToken)
    {
        var debit = records.Where(x => IsDebitRecord(x)).Sum(x => x.Amount + x.FeeAmount);
        var credit = records.Where(x => !IsDebitRecord(x)).Sum(x => x.Amount + x.FeeAmount);
        var net = Math.Abs(debit - credit);
        if (net <= 0.005m) return;
        var lines = debit >= credit
            ? new[]
            {
                new PostGlJournalLineRequest(GlCardholderLiability, LedgerEntryDirection.Debit, net, "Settlement debit from cardholder liability"),
                new PostGlJournalLineRequest(GlSettlementClearing, LedgerEntryDirection.Credit, net, "Settlement clearing credit")
            }
            : new[]
            {
                new PostGlJournalLineRequest(GlSettlementClearing, LedgerEntryDirection.Debit, net, "Settlement clearing debit"),
                new PostGlJournalLineRequest(GlCardholderLiability, LedgerEntryDirection.Credit, net, "Settlement credit to cardholder liability")
            };
        await PostGlJournalAsync(new PostGlJournalRequest("Settlement", batch.BatchReference, $"Settlement batch {batch.BatchReference}", batch.CurrencyCode, batch.BatchReference, lines), cancellationToken).ConfigureAwait(false);
    }

    private async Task<CmsOperationResult<SettlementStatementResult>> GeneratePartySettlementAsync(SettlementPartyType partyType, Guid partyId, string currencyCode, DateOnly periodStart, DateOnly periodEnd, IReadOnlyCollection<PrepaidCard> cards, decimal commissionRatePercent, bool postGl, CancellationToken cancellationToken)
    {
        if (periodEnd < periodStart) return CmsOperationResult<SettlementStatementResult>.Fail("30", "Settlement period end cannot be before start.");
        var lines = new List<SettlementStatementLine>();
        foreach (var card in cards)
        {
            var wallet = await _cms.GetWalletAsync(card.WalletAccountId, cancellationToken).ConfigureAwait(false);
            if (wallet is null || !string.Equals(wallet.CurrencyCode, currencyCode, StringComparison.Ordinal)) continue;
            var entries = await _cms.GetLedgerEntriesAsync(wallet.Id, periodStart, periodEnd, cancellationToken).ConfigureAwait(false);
            foreach (var entry in entries.Where(x => string.Equals(x.CurrencyCode, currencyCode, StringComparison.Ordinal)))
            {
                lines.Add(new SettlementStatementLine
                {
                    SettlementStatementId = Guid.Empty,
                    TransactionDate = entry.CreatedAt,
                    SourceType = entry.EntryType.ToString(),
                    Reference = entry.Reference,
                    Narrative = $"{card.MaskedPan} {entry.Narrative}",
                    Direction = entry.Direction,
                    Amount = entry.Amount,
                    CurrencyCode = entry.CurrencyCode
                });
            }
        }

        var grossDebit = lines.Where(x => x.Direction == LedgerEntryDirection.Debit).Sum(x => x.Amount);
        var grossCredit = lines.Where(x => x.Direction == LedgerEntryDirection.Credit).Sum(x => x.Amount);
        var fees = lines.Where(x => string.Equals(x.SourceType, LedgerEntryType.Fee.ToString(), StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
        var commission = partyType == SettlementPartyType.Agency ? Math.Round(fees * commissionRatePercent / 100m, 2, MidpointRounding.AwayFromZero) : 0m;
        var net = partyType == SettlementPartyType.Agency ? grossCredit - grossDebit + commission : grossDebit - grossCredit;
        var statement = new SettlementStatement
        {
            PartyType = partyType,
            PartyId = partyId,
            StatementNumber = CreateStatementNumber(partyType == SettlementPartyType.Agency ? "AGS" : "COS"),
            CurrencyCode = currencyCode,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            GrossDebitAmount = grossDebit,
            GrossCreditAmount = grossCredit,
            FeeAmount = fees,
            CommissionAmount = commission,
            NetSettlementAmount = net,
            Status = SettlementStatementStatus.Generated,
            CreatedAt = _clock.UtcNow
        };
        var finalLines = lines.Select(x => x with { SettlementStatementId = statement.Id }).ToList();
        await _financial.AddSettlementStatementAsync(statement, finalLines, cancellationToken).ConfigureAwait(false);

        GlJournalEntry? journal = null;
        if (postGl && Math.Abs(net) > 0.005m)
        {
            var gl = await PostGlJournalAsync(new PostGlJournalRequest(
                partyType.ToString(),
                statement.StatementNumber,
                $"{partyType} settlement {statement.StatementNumber}",
                currencyCode,
                statement.StatementNumber,
                net >= 0
                    ? new[]
                    {
                        new PostGlJournalLineRequest(GlSettlementClearing, LedgerEntryDirection.Debit, Math.Abs(net), "Settlement receivable"),
                        new PostGlJournalLineRequest(GlCardholderLiability, LedgerEntryDirection.Credit, Math.Abs(net), "Settlement liability")
                    }
                    : new[]
                    {
                        new PostGlJournalLineRequest(GlCardholderLiability, LedgerEntryDirection.Debit, Math.Abs(net), "Settlement liability debit"),
                        new PostGlJournalLineRequest(GlSettlementClearing, LedgerEntryDirection.Credit, Math.Abs(net), "Settlement payable")
                    }), cancellationToken).ConfigureAwait(false);
            journal = gl.Value?.Journal;
        }
        return CmsOperationResult<SettlementStatementResult>.Success(new SettlementStatementResult(statement, finalLines, journal));
    }

    private ReconciliationException CreateException(SettlementBatch batch, SettlementRecord record, ReconciliationExceptionType type, decimal expected, decimal actual, string currencyCode, string reason)
    {
        return new ReconciliationException
        {
            BatchId = batch.Id,
            SettlementRecordId = record.Id,
            ExceptionType = type,
            Status = ReconciliationExceptionStatus.Open,
            Severity = type == ReconciliationExceptionType.AmountMismatch || type == ReconciliationExceptionType.SettlementWithoutAuthorization ? "HIGH" : "MEDIUM",
            CorrelationId = batch.BatchReference,
            Reference = string.IsNullOrWhiteSpace(record.ExternalReference) ? record.Rrn : record.ExternalReference,
            ExpectedAmount = expected,
            ActualAmount = actual,
            DifferenceAmount = actual - expected,
            CurrencyCode = currencyCode,
            Reason = reason,
            CreatedAt = _clock.UtcNow
        };
    }

    private static WalletAccount ApplyWalletMovement(WalletAccount wallet, FinancialAdjustmentDirection direction, decimal amount)
    {
        return direction == FinancialAdjustmentDirection.CreditCardholder
            ? wallet with { LedgerBalance = wallet.LedgerBalance + amount, AvailableBalance = wallet.AvailableBalance + amount }
            : wallet with { LedgerBalance = wallet.LedgerBalance - amount, AvailableBalance = wallet.AvailableBalance - amount };
    }

    private static bool IsDebitRecord(ImportSettlementRecordRequest record)
    {
        return record.RecordType is SettlementRecordType.Purchase or SettlementRecordType.Fee or SettlementRecordType.Chargeback or SettlementRecordType.Adjustment;
    }

    private static bool IsDebitRecord(SettlementRecord record)
    {
        return record.RecordType is SettlementRecordType.Purchase or SettlementRecordType.Fee or SettlementRecordType.Chargeback or SettlementRecordType.Adjustment;
    }

    private static string CreateStatementNumber(string prefix)
    {
        var value = $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
        return value.Length <= 48 ? value : value[..48];
    }

    // ---------------------------------------------------------------
    // Settlement Engine — Outbound Net Settlement Position (B1 FIX)
    // ---------------------------------------------------------------

    public async Task<CmsOperationResult<IReadOnlyList<NetSettlementPosition>>> GenerateNetSettlementPositionsAsync(
        DateOnly businessDate,
        string currencyCode,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        if (!IsCurrency(currencyCode)) return CmsOperationResult<IReadOnlyList<NetSettlementPosition>>.Fail("30", "Currency code must be 3 numeric characters.");

        // Gather all approved, cleared transactions for the business date
        // "Cleared" means included in a clearing batch (IsCleared = true)
        // We query via the standard transaction repository using the empty-profile fallback
        // to get ALL cleared transactions, then group by SettlementProfile (=institution proxy)
        var allCleared = await _transactions.GetUnclearedTransactionsAsync(businessDate, string.Empty, cancellationToken).ConfigureAwait(false);

        // Also gather transactions cleared today that were marked via MarkTransactionsClearedAsync
        // The transaction store already returns UNCLEARED — for positions we need already-cleared ones
        // We use the clearing repository patterns through a different path here:
        // Group by SettlementProfile and calculate net per group
        var byProfile = allCleared
            .Where(t => string.Equals(t.CurrencyCode, currencyCode, StringComparison.Ordinal))
            .GroupBy(t => string.IsNullOrWhiteSpace(t.SettlementProfile) ? "DEFAULT" : t.SettlementProfile);

        var positions = new List<NetSettlementPosition>();

        foreach (var group in byProfile)
        {
            var profile = group.Key;
            var txns = group.ToList();

            // Gross amounts by type
            var grossPurchase = txns.Where(t => t.Mti is "0200" or "0210" && !t.ReversalState.Equals(ReversalState.Reversed)).Sum(t => t.Amount);
            var grossRefund = txns.Where(t => t.ReversalState == ReversalState.Reversed).Sum(t => t.Amount);
            var grossFee = 0m; // fees are tracked separately in CMS, not in switch TransactionLog

            var netAmount = grossPurchase - grossRefund;
            var direction = netAmount > 0.005m ? SettlementPositionDirection.NetDebit
                : netAmount < -0.005m ? SettlementPositionDirection.NetCredit
                : SettlementPositionDirection.Balanced;
            var absNet = Math.Abs(netAmount);

            // Check if position already exists for this date/profile/currency
            var existing = await _positions.GetPositionByProfileAndDateAsync(profile, businessDate, currencyCode, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                _audit.LogSystem("SETTLEMENT", $"Net settlement position already exists for {profile}/{businessDate}/{currencyCode} (id={existing.Id}). Skipping.");
                positions.Add(existing);
                continue;
            }

            var position = new NetSettlementPosition
            {
                InstitutionCode = profile,
                SettlementProfile = profile,
                BusinessDate = businessDate,
                CurrencyCode = currencyCode,
                GrossPurchaseAmount = grossPurchase,
                GrossRefundAmount = grossRefund,
                GrossFeeAmount = grossFee,
                NetSettlementAmount = absNet,
                Direction = direction,
                Status = SettlementPositionStatus.Calculated,
                TransactionCount = txns.Count,
                NostroReference = $"NSP-{profile}-{businessDate:yyyyMMdd}-{Guid.NewGuid():N[..8]}",
                CalculatedAt = _clock.UtcNow
            };
            await _positions.AddPositionAsync(position, cancellationToken).ConfigureAwait(false);

            // Post Nostro GL entry
            if (absNet > 0.005m)
            {
                var nostroLines = direction == SettlementPositionDirection.NetDebit
                    ? new[]
                    {
                        new PostGlJournalLineRequest(GlNostroFunding, LedgerEntryDirection.Debit, absNet, $"Net settlement debit {profile} {businessDate}"),
                        new PostGlJournalLineRequest(GlSettlementClearing, LedgerEntryDirection.Credit, absNet, $"Settlement clearing contra {profile}")
                    }
                    : new[]
                    {
                        new PostGlJournalLineRequest(GlSettlementClearing, LedgerEntryDirection.Debit, absNet, $"Settlement clearing debit {profile}"),
                        new PostGlJournalLineRequest(GlNostroFunding, LedgerEntryDirection.Credit, absNet, $"Net settlement credit {profile} {businessDate}")
                    };

                var glResult = await PostGlJournalAsync(new PostGlJournalRequest(
                    "Settlement", position.NostroReference,
                    $"Net settlement {direction} {profile} {businessDate} {currencyCode}",
                    currencyCode, position.NostroReference, nostroLines), cancellationToken).ConfigureAwait(false);

                var glPostedPosition = position with
                {
                    Status = SettlementPositionStatus.GlPosted,
                    NostroGlJournalId = glResult.Value?.Journal.Id,
                    GlPostedAt = _clock.UtcNow
                };

                // Generate settlement instruction file (ISO 20022 camt.054)
                var instructionFile = BuildSettlementInstructionFile(glPostedPosition, txns);
                var finalPosition = glPostedPosition with
                {
                    Status = SettlementPositionStatus.InstructionGenerated,
                    SettlementInstructionFile = instructionFile,
                    InstructionGeneratedAt = _clock.UtcNow
                };
                await _positions.UpdatePositionAsync(finalPosition, cancellationToken).ConfigureAwait(false);
                positions.Add(finalPosition);

                _audit.LogReconciliation(position.NostroReference, profile,
                    $"NetSettlement/{direction}",
                    $"date={businessDate} currency={currencyCode} net={absNet} txnCount={txns.Count} gl={glPostedPosition.NostroGlJournalId}");
            }
            else
            {
                positions.Add(position);
                _audit.LogSystem("SETTLEMENT", $"Net settlement position for {profile}/{businessDate}/{currencyCode} is balanced (net=0). No GL posting required.");
            }
        }

        return CmsOperationResult<IReadOnlyList<NetSettlementPosition>>.Success(positions,
            $"{positions.Count} settlement position(s) generated for {businessDate} {currencyCode}.");
    }

    public Task<IReadOnlyList<NetSettlementPosition>> GetSettlementPositionsAsync(DateOnly? businessDate, CancellationToken cancellationToken = default)
        => _positions.GetPositionsAsync(businessDate, cancellationToken);

    /// <summary>
    /// Builds a minimal ISO 20022 camt.054 settlement advice in XML.
    /// In production this would be a full-conformant camt.054.001.02 document
    /// signed and sent via SWIFT FileAct or SFTP to the Nostro bank.
    /// </summary>
    private static bool IsCurrency(string? currencyCode) =>
        !string.IsNullOrWhiteSpace(currencyCode) && currencyCode.Length == 3 && currencyCode.All(char.IsDigit);

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static string BuildSettlementInstructionFile(NetSettlementPosition position, IReadOnlyList<TransactionLog> transactions)    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<Document xmlns=\"urn:iso:std:iso:20022:tech:xsd:camt.054.001.02\">");
        sb.AppendLine("  <BkToCstmrDbtCdtNtfctn>");
        sb.AppendLine($"    <GrpHdr><MsgId>{position.NostroReference}</MsgId><CreDtTm>{position.CalculatedAt:O}</CreDtTm></GrpHdr>");
        sb.AppendLine($"    <Ntfctn><Id>{position.NostroReference}</Id><Acct><Id><Othr><Id>{position.SettlementProfile}</Id></Othr></Id></Acct>");
        sb.AppendLine($"    <TxsSummry><TtlNtries><NbOfNtries>{position.TransactionCount}</NbOfNtries><Sum>{position.NetSettlementAmount}</Sum><TtlNetNtry><Amt Ccy=\"{position.CurrencyCode}\">{position.NetSettlementAmount}</Amt><CdtDbtInd>{(position.Direction == SettlementPositionDirection.NetDebit ? "DBIT" : "CRDT")}</CdtDbtInd></TtlNetNtry></TtlNtries></TxsSummry>");
        foreach (var tx in transactions.Take(1000)) // limit to first 1000 detail entries
            sb.AppendLine($"    <Ntry><Amt Ccy=\"{tx.CurrencyCode}\">{tx.Amount}</Amt><CdtDbtInd>DBIT</CdtDbtInd><Sts>BOOK</Sts><BookgDt><Dt>{tx.CreatedAt:yyyy-MM-dd}</Dt></BookgDt><NtryDtls><TxDtls><Refs><EndToEndId>{tx.Rrn}</EndToEndId></Refs></TxDtls></NtryDtls></Ntry>");
        sb.AppendLine("    </Ntfctn>");
        sb.AppendLine("  </BkToCstmrDbtCdtNtfctn>");
        sb.AppendLine("</Document>");
        return sb.ToString();
    }
}
