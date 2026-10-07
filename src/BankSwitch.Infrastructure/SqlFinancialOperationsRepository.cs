using BankSwitch.Application;
using BankSwitch.Domain;
using Npgsql;

namespace BankSwitch.Infrastructure;

public sealed class SqlFinancialOperationsRepository : IFinancialOperationsRepository
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;

    public SqlFinancialOperationsRepository(SecurePostgresConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
public async Task AddSettlementBatchAsync(SettlementBatch batch, IReadOnlyCollection<SettlementRecord> records, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        await using (var command = new NpgsqlCommand("""
            INSERT INTO dbo.settlementbatches
            (id, batchreference, filename, sourcesystem, currencycode, settlementdate, recordcount, totaldebitamount, totalcreditamount, status, importedby, importedat, processedat)
            VALUES (@Id, @BatchReference, @FileName, @SourceSystem, @CurrencyCode, @SettlementDate, @RecordCount, @TotalDebitAmount, @TotalCreditAmount, @Status, @ImportedBy, @ImportedAt, @ProcessedAt)
            """, connection, tx))
        {
            BindSettlementBatch(command, batch);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var record in records)
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO dbo.settlementrecords
                (id, batchid, externalreference, rrn, stan, maskedpan, panhash, recordtype, amount, feeamount, currencycode, transactiondate, status, matchedcmstransactionid, responsecode, narrative)
                VALUES (@Id, @BatchId, @ExternalReference, @Rrn, @Stan, @MaskedPan, @PanHash, @RecordType, @Amount, @FeeAmount, @CurrencyCode, @TransactionDate, @Status, @MatchedCmsTransactionId, @ResponseCode, @Narrative)
                """, connection, tx);
            BindSettlementRecord(command, record);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
    catch
    {
        await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
        throw;
    }
}
    public async Task<SettlementBatch?> GetSettlementBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.settlementbatches WHERE id = @Id LIMIT 1", connection);
        command.Parameters.AddWithValue("@Id", batchId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadSettlementBatch(reader) : null;
    }

    public async Task<SettlementBatch?> GetSettlementBatchByReferenceAsync(string batchReference, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.settlementbatches WHERE batchreference = @BatchReference LIMIT 1", connection);
        command.Parameters.AddWithValue("@BatchReference", batchReference ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadSettlementBatch(reader) : null;
    }

    public async Task UpdateSettlementBatchAsync(SettlementBatch batch, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            UPDATE dbo.settlementbatches SET status = @Status, recordcount = @RecordCount, totaldebitamount = @TotalDebitAmount, totalcreditamount = @TotalCreditAmount, processedat = @ProcessedAt WHERE id = @Id
            """, connection);
        command.Parameters.AddWithValue("@Id", batch.Id);
        command.Parameters.AddWithValue("@Status", batch.Status.ToString());
        command.Parameters.AddWithValue("@RecordCount", batch.RecordCount);
        command.Parameters.AddWithValue("@TotalDebitAmount", batch.TotalDebitAmount);
        command.Parameters.AddWithValue("@TotalCreditAmount", batch.TotalCreditAmount);
        command.Parameters.AddWithValue("@ProcessedAt", (object?)batch.ProcessedAt ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SettlementRecord>> GetSettlementRecordsAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var records = new List<SettlementRecord>();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.settlementrecords WHERE batchid = @BatchId ORDER BY transactiondate, externalreference", connection);
        command.Parameters.AddWithValue("@BatchId", batchId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) records.Add(ReadSettlementRecord(reader));
        return records;
    }

    public async Task UpdateSettlementRecordAsync(SettlementRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            UPDATE dbo.settlementrecords SET status = @Status, matchedCmsTransactionId = @MatchedCmsTransactionId, responsecode = @ResponseCode, narrative = @Narrative WHERE id = @Id
            """, connection);
        command.Parameters.AddWithValue("@Id", record.Id);
        command.Parameters.AddWithValue("@Status", record.Status.ToString());
        command.Parameters.AddWithValue("@MatchedCmsTransactionId", (object?)record.MatchedCmsTransactionId ?? DBNull.Value);
        command.Parameters.AddWithValue("@ResponseCode", record.ResponseCode);
        command.Parameters.AddWithValue("@Narrative", record.Narrative);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

public async Task AddReconciliationExceptionAsync(ReconciliationException exception, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.reconciliationexceptions
        (id, batchid, settlementrecordid, exceptiontype, status, severity, correlationid, reference, expectedamount, actualamount, differenceamount, currencycode, reason, assignedto, resolutionnotes, createdat, resolvedat)
        VALUES (@Id, @BatchId, @SettlementRecordId, @ExceptionType, @Status, @Severity, @CorrelationId, @Reference, @ExpectedAmount, @ActualAmount, @DifferenceAmount, @CurrencyCode, @Reason, @AssignedTo, @ResolutionNotes, @CreatedAt, @ResolvedAt)
        """, connection);
    BindReconciliationException(command, exception);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
    public async Task<ReconciliationException?> GetReconciliationExceptionAsync(Guid exceptionId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.reconciliationexceptions WHERE id = @Id LIMIT 1", connection);
        command.Parameters.AddWithValue("@Id", exceptionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadReconciliationException(reader) : null;
    }

    public async Task<IReadOnlyList<ReconciliationException>> GetOpenReconciliationExceptionsAsync(CancellationToken cancellationToken = default)
    {
        var exceptions = new List<ReconciliationException>();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            SELECT * FROM dbo.reconciliationexceptions WHERE status IN ('Open','Assigned','Escalated') ORDER BY createdat
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) exceptions.Add(ReadReconciliationException(reader));
        return exceptions;
    }

    public async Task UpdateReconciliationExceptionAsync(ReconciliationException exception, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            UPDATE dbo.reconciliationexceptions SET status = @Status, assignedto = @AssignedTo, resolutionnotes = @ResolutionNotes, resolvedat = @ResolvedAt, correlationid = @CorrelationId WHERE id = @Id
            """, connection);
        command.Parameters.AddWithValue("@Id", exception.Id);
        command.Parameters.AddWithValue("@Status", exception.Status.ToString());
        command.Parameters.AddWithValue("@AssignedTo", exception.AssignedTo);
        command.Parameters.AddWithValue("@ResolutionNotes", exception.ResolutionNotes);
        command.Parameters.AddWithValue("@ResolvedAt", (object?)exception.ResolvedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@CorrelationId", exception.CorrelationId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
public async Task AddGlJournalAsync(GlJournalEntry journal, IReadOnlyCollection<GlJournalLine> lines, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        await using (var command = new NpgsqlCommand("""
            INSERT INTO dbo.gljournalentries
            (id, journalnumber, correlationid, sourcemodule, reference, narrative, currencycode, debittotal, credittotal, status, createdat, postedat)
            VALUES (@Id, @JournalNumber, @CorrelationId, @SourceModule, @Reference, @Narrative, @CurrencyCode, @DebitTotal, @CreditTotal, @Status, @CreatedAt, @PostedAt)
            """, connection, tx))
        {
            BindGlJournal(command, journal);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var line in lines)
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO dbo.gljournallines
                (id, journalentryid, accountcode, direction, amount, currencycode, narrative)
                VALUES (@Id, @JournalEntryId, @AccountCode, @Direction, @Amount, @CurrencyCode, @Narrative)
                """, connection, tx);
            BindGlLine(command, line);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
    catch
    {
        await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
        throw;
    }
}
    public async Task<GlJournalEntry?> GetGlJournalAsync(Guid journalId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.gljournalentries WHERE id = @Id LIMIT 1", connection);
        command.Parameters.AddWithValue("@Id", journalId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadGlJournal(reader) : null;
    }

    public async Task<IReadOnlyList<GlJournalLine>> GetGlJournalLinesAsync(Guid journalId, CancellationToken cancellationToken = default)
    {
        var lines = new List<GlJournalLine>();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.gljournallines WHERE journalentryid = @JournalEntryId ORDER BY id", connection);
        command.Parameters.AddWithValue("@JournalEntryId", journalId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) lines.Add(ReadGlLine(reader));
        return lines;
    }

    public async Task<IReadOnlyList<GlJournalEntry>> GetGlJournalsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.gljournalentries ORDER BY createdat", connection);
        var results = new List<GlJournalEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) results.Add(ReadGlJournal(reader));
        return results;
    }

public async Task AddFinancialOperationAsync(FinancialOperation operation, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.financialoperations
        (id, operationtype, status, cardid, walletaccountid, originalcmstransactionid, originalrrn, originalstan, newrrn, newstan, maskedpan, panhash, direction, amount, feeamount, currencycode, reason, ticketreference, maker, checker, createdat, approvedat, postedat)
        VALUES (@Id, @OperationType, @Status, @CardId, @WalletAccountId, @OriginalCmsTransactionId, @OriginalRrn, @OriginalStan, @NewRrn, @NewStan, @MaskedPan, @PanHash, @Direction, @Amount, @FeeAmount, @CurrencyCode, @Reason, @TicketReference, @Maker, @Checker, @CreatedAt, @ApprovedAt, @PostedAt)
        """, connection);
    BindFinancialOperation(command, operation);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

public async Task UpdateFinancialOperationAsync(FinancialOperation operation, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        UPDATE dbo.financialoperations SET status = @Status, checker = @Checker, approvedat = @ApprovedAt, postedat = @PostedAt WHERE id = @Id
        """, connection);
    command.Parameters.AddWithValue("@Id", operation.Id);
    command.Parameters.AddWithValue("@Status", operation.Status.ToString());
    command.Parameters.AddWithValue("@Checker", operation.Checker);
    command.Parameters.AddWithValue("@ApprovedAt", (object?)operation.ApprovedAt ?? DBNull.Value);
    command.Parameters.AddWithValue("@PostedAt", (object?)operation.PostedAt ?? DBNull.Value);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
    public async Task<FinancialOperation?> GetFinancialOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.financialoperations WHERE id = @Id LIMIT 1", connection);
        command.Parameters.AddWithValue("@Id", operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadFinancialOperation(reader) : null;
    }

public async Task<bool> ExistsPostedFinancialOperationAsync(FinancialOperationType operationType, string originalRrn, string originalStan, string panHash, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        SELECT COUNT(1) FROM dbo.financialoperations
        WHERE operationtype = @OperationType AND originalrrn = @OriginalRrn AND originalstan = @OriginalStan AND panhash = @PanHash AND status = 'Posted'
        """, connection);
    command.Parameters.AddWithValue("@OperationType", operationType.ToString());
    command.Parameters.AddWithValue("@OriginalRrn", originalRrn ?? string.Empty);
    command.Parameters.AddWithValue("@OriginalStan", originalStan ?? string.Empty);
    command.Parameters.AddWithValue("@PanHash", panHash ?? string.Empty);
    var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0, System.Globalization.CultureInfo.InvariantCulture);
    return count > 0;
}
public async Task AddSettlementStatementAsync(SettlementStatement statement, IReadOnlyCollection<SettlementStatementLine> lines, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        await using (var command = new NpgsqlCommand("""
            INSERT INTO dbo.settlementstatements
            (id, partytype, partyid, statementnumber, currencycode, periodstart, periodend, grossdebitamount, grosscreditamount, feeamount, commissionamount, netsettlementamount, status, createdat, postedat)
            VALUES (@Id, @PartyType, @PartyId, @StatementNumber, @CurrencyCode, @PeriodStart, @PeriodEnd, @GrossDebitAmount, @GrossCreditAmount, @FeeAmount, @CommissionAmount, @NetSettlementAmount, @Status, @CreatedAt, @PostedAt)
            """, connection, tx))
        {
            BindSettlementStatement(command, statement);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var line in lines)
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO dbo.settlementstatementlines
                (id, settlementstatementid, transactiondate, sourcetype, reference, narrative, direction, amount, currencycode)
                VALUES (@Id, @SettlementStatementId, @TransactionDate, @SourceType, @Reference, @Narrative, @Direction, @Amount, @CurrencyCode)
                """, connection, tx);
            BindSettlementStatementLine(command, line);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
    catch
    {
        await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
        throw;
    }
}
    public async Task<SettlementStatement?> GetSettlementStatementAsync(Guid statementId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.settlementstatements WHERE id = @Id LIMIT 1", connection);
        command.Parameters.AddWithValue("@Id", statementId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadSettlementStatement(reader) : null;
    }

    public async Task<IReadOnlyList<SettlementStatementLine>> GetSettlementStatementLinesAsync(Guid statementId, CancellationToken cancellationToken = default)
    {
        var lines = new List<SettlementStatementLine>();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.settlementstatementlines WHERE settlementstatementid = @StatementId ORDER BY transactiondate", connection);
        command.Parameters.AddWithValue("@StatementId", statementId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) lines.Add(ReadSettlementStatementLine(reader));
        return lines;
    }

    private static void BindSettlementBatch(NpgsqlCommand command, SettlementBatch batch)
    {
        command.Parameters.AddWithValue("@Id", batch.Id);
        command.Parameters.AddWithValue("@BatchReference", batch.BatchReference);
        command.Parameters.AddWithValue("@FileName", batch.FileName);
        command.Parameters.AddWithValue("@SourceSystem", batch.SourceSystem);
        command.Parameters.AddWithValue("@CurrencyCode", batch.CurrencyCode);
        command.Parameters.AddWithValue("@SettlementDate", batch.SettlementDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@RecordCount", batch.RecordCount);
        command.Parameters.AddWithValue("@TotalDebitAmount", batch.TotalDebitAmount);
        command.Parameters.AddWithValue("@TotalCreditAmount", batch.TotalCreditAmount);
        command.Parameters.AddWithValue("@Status", batch.Status.ToString());
        command.Parameters.AddWithValue("@ImportedBy", batch.ImportedBy);
        command.Parameters.AddWithValue("@ImportedAt", batch.ImportedAt);
        command.Parameters.AddWithValue("@ProcessedAt", (object?)batch.ProcessedAt ?? DBNull.Value);
    }

    private static void BindSettlementRecord(NpgsqlCommand command, SettlementRecord record)
    {
        command.Parameters.AddWithValue("@Id", record.Id);
        command.Parameters.AddWithValue("@BatchId", record.BatchId);
        command.Parameters.AddWithValue("@ExternalReference", record.ExternalReference);
        command.Parameters.AddWithValue("@Rrn", record.Rrn);
        command.Parameters.AddWithValue("@Stan", record.Stan);
        command.Parameters.AddWithValue("@MaskedPan", record.MaskedPan);
        command.Parameters.AddWithValue("@PanHash", record.PanHash);
        command.Parameters.AddWithValue("@RecordType", record.RecordType.ToString());
        command.Parameters.AddWithValue("@Amount", record.Amount);
        command.Parameters.AddWithValue("@FeeAmount", record.FeeAmount);
        command.Parameters.AddWithValue("@CurrencyCode", record.CurrencyCode);
        command.Parameters.AddWithValue("@TransactionDate", record.TransactionDate);
        command.Parameters.AddWithValue("@Status", record.Status.ToString());
        command.Parameters.AddWithValue("@MatchedCmsTransactionId", (object?)record.MatchedCmsTransactionId ?? DBNull.Value);
        command.Parameters.AddWithValue("@ResponseCode", record.ResponseCode);
        command.Parameters.AddWithValue("@Narrative", record.Narrative);
    }

    private static void BindReconciliationException(NpgsqlCommand command, ReconciliationException exception)
    {
        command.Parameters.AddWithValue("@Id", exception.Id);
        command.Parameters.AddWithValue("@BatchId", (object?)exception.BatchId ?? DBNull.Value);
        command.Parameters.AddWithValue("@SettlementRecordId", (object?)exception.SettlementRecordId ?? DBNull.Value);
        command.Parameters.AddWithValue("@ExceptionType", exception.ExceptionType.ToString());
        command.Parameters.AddWithValue("@Status", exception.Status.ToString());
        command.Parameters.AddWithValue("@Severity", exception.Severity);
        command.Parameters.AddWithValue("@CorrelationId", exception.CorrelationId);
        command.Parameters.AddWithValue("@Reference", exception.Reference);
        command.Parameters.AddWithValue("@ExpectedAmount", exception.ExpectedAmount);
        command.Parameters.AddWithValue("@ActualAmount", exception.ActualAmount);
        command.Parameters.AddWithValue("@DifferenceAmount", exception.DifferenceAmount);
        command.Parameters.AddWithValue("@CurrencyCode", exception.CurrencyCode);
        command.Parameters.AddWithValue("@Reason", exception.Reason);
        command.Parameters.AddWithValue("@AssignedTo", exception.AssignedTo);
        command.Parameters.AddWithValue("@ResolutionNotes", exception.ResolutionNotes);
        command.Parameters.AddWithValue("@CreatedAt", exception.CreatedAt);
        command.Parameters.AddWithValue("@ResolvedAt", (object?)exception.ResolvedAt ?? DBNull.Value);
    }

    private static void BindGlJournal(NpgsqlCommand command, GlJournalEntry journal)
    {
        command.Parameters.AddWithValue("@Id", journal.Id);
        command.Parameters.AddWithValue("@JournalNumber", journal.JournalNumber);
        command.Parameters.AddWithValue("@CorrelationId", journal.CorrelationId);
        command.Parameters.AddWithValue("@SourceModule", journal.SourceModule);
        command.Parameters.AddWithValue("@Reference", journal.Reference);
        command.Parameters.AddWithValue("@Narrative", journal.Narrative);
        command.Parameters.AddWithValue("@CurrencyCode", journal.CurrencyCode);
        command.Parameters.AddWithValue("@DebitTotal", journal.DebitTotal);
        command.Parameters.AddWithValue("@CreditTotal", journal.CreditTotal);
        command.Parameters.AddWithValue("@Status", journal.Status.ToString());
        command.Parameters.AddWithValue("@CreatedAt", journal.CreatedAt);
        command.Parameters.AddWithValue("@PostedAt", (object?)journal.PostedAt ?? DBNull.Value);
    }

    private static void BindGlLine(NpgsqlCommand command, GlJournalLine line)
    {
        command.Parameters.AddWithValue("@Id", line.Id);
        command.Parameters.AddWithValue("@JournalEntryId", line.JournalEntryId);
        command.Parameters.AddWithValue("@AccountCode", line.AccountCode);
        command.Parameters.AddWithValue("@Direction", line.Direction.ToString());
        command.Parameters.AddWithValue("@Amount", line.Amount);
        command.Parameters.AddWithValue("@CurrencyCode", line.CurrencyCode);
        command.Parameters.AddWithValue("@Narrative", line.Narrative);
    }

    private static void BindFinancialOperation(NpgsqlCommand command, FinancialOperation operation)
    {
        command.Parameters.AddWithValue("@Id", operation.Id);
        command.Parameters.AddWithValue("@OperationType", operation.OperationType.ToString());
        command.Parameters.AddWithValue("@Status", operation.Status.ToString());
        command.Parameters.AddWithValue("@CardId", (object?)operation.CardId ?? DBNull.Value);
        command.Parameters.AddWithValue("@WalletAccountId", (object?)operation.WalletAccountId ?? DBNull.Value);
        command.Parameters.AddWithValue("@OriginalCmsTransactionId", (object?)operation.OriginalCmsTransactionId ?? DBNull.Value);
        command.Parameters.AddWithValue("@OriginalRrn", operation.OriginalRrn);
        command.Parameters.AddWithValue("@OriginalStan", operation.OriginalStan);
        command.Parameters.AddWithValue("@NewRrn", operation.NewRrn);
        command.Parameters.AddWithValue("@NewStan", operation.NewStan);
        command.Parameters.AddWithValue("@MaskedPan", operation.MaskedPan);
        command.Parameters.AddWithValue("@PanHash", operation.PanHash);
        command.Parameters.AddWithValue("@Direction", operation.Direction.ToString());
        command.Parameters.AddWithValue("@Amount", operation.Amount);
        command.Parameters.AddWithValue("@FeeAmount", operation.FeeAmount);
        command.Parameters.AddWithValue("@CurrencyCode", operation.CurrencyCode);
        command.Parameters.AddWithValue("@Reason", operation.Reason);
        command.Parameters.AddWithValue("@TicketReference", operation.TicketReference);
        command.Parameters.AddWithValue("@Maker", operation.Maker);
        command.Parameters.AddWithValue("@Checker", operation.Checker);
        command.Parameters.AddWithValue("@CreatedAt", operation.CreatedAt);
        command.Parameters.AddWithValue("@ApprovedAt", (object?)operation.ApprovedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@PostedAt", (object?)operation.PostedAt ?? DBNull.Value);
    }

    private static void BindSettlementStatement(NpgsqlCommand command, SettlementStatement statement)
    {
        command.Parameters.AddWithValue("@Id", statement.Id);
        command.Parameters.AddWithValue("@PartyType", statement.PartyType.ToString());
        command.Parameters.AddWithValue("@PartyId", statement.PartyId);
        command.Parameters.AddWithValue("@StatementNumber", statement.StatementNumber);
        command.Parameters.AddWithValue("@CurrencyCode", statement.CurrencyCode);
        command.Parameters.AddWithValue("@PeriodStart", statement.PeriodStart.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@PeriodEnd", statement.PeriodEnd.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@GrossDebitAmount", statement.GrossDebitAmount);
        command.Parameters.AddWithValue("@GrossCreditAmount", statement.GrossCreditAmount);
        command.Parameters.AddWithValue("@FeeAmount", statement.FeeAmount);
        command.Parameters.AddWithValue("@CommissionAmount", statement.CommissionAmount);
        command.Parameters.AddWithValue("@NetSettlementAmount", statement.NetSettlementAmount);
        command.Parameters.AddWithValue("@Status", statement.Status.ToString());
        command.Parameters.AddWithValue("@CreatedAt", statement.CreatedAt);
        command.Parameters.AddWithValue("@PostedAt", (object?)statement.PostedAt ?? DBNull.Value);
    }

    private static void BindSettlementStatementLine(NpgsqlCommand command, SettlementStatementLine line)
    {
        command.Parameters.AddWithValue("@Id", line.Id);
        command.Parameters.AddWithValue("@SettlementStatementId", line.SettlementStatementId);
        command.Parameters.AddWithValue("@TransactionDate", line.TransactionDate);
        command.Parameters.AddWithValue("@SourceType", line.SourceType);
        command.Parameters.AddWithValue("@Reference", line.Reference);
        command.Parameters.AddWithValue("@Narrative", line.Narrative);
        command.Parameters.AddWithValue("@Direction", line.Direction.ToString());
        command.Parameters.AddWithValue("@Amount", line.Amount);
        command.Parameters.AddWithValue("@CurrencyCode", line.CurrencyCode);
    }

    private static SettlementBatch ReadSettlementBatch(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        BatchReference = GetString(reader, "BatchReference"),
        FileName = GetString(reader, "FileName"),
        SourceSystem = GetString(reader, "SourceSystem"),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        SettlementDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("SettlementDate"))),
        RecordCount = reader.GetInt32(reader.GetOrdinal("RecordCount")),
        TotalDebitAmount = reader.GetDecimal(reader.GetOrdinal("TotalDebitAmount")),
        TotalCreditAmount = reader.GetDecimal(reader.GetOrdinal("TotalCreditAmount")),
        Status = Enum.Parse<SettlementBatchStatus>(GetString(reader, "Status")),
        ImportedBy = GetString(reader, "ImportedBy"),
        ImportedAt = GetDateTimeOffset(reader, "ImportedAt"),
        ProcessedAt = GetNullableDateTimeOffset(reader, "ProcessedAt")
    };

    private static SettlementRecord ReadSettlementRecord(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        BatchId = reader.GetGuid(reader.GetOrdinal("BatchId")),
        ExternalReference = GetString(reader, "ExternalReference"),
        Rrn = GetString(reader, "Rrn"),
        Stan = GetString(reader, "Stan"),
        MaskedPan = GetString(reader, "MaskedPan"),
        PanHash = GetString(reader, "PanHash"),
        RecordType = Enum.Parse<SettlementRecordType>(GetString(reader, "RecordType")),
        Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
        FeeAmount = reader.GetDecimal(reader.GetOrdinal("FeeAmount")),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        TransactionDate = GetDateTimeOffset(reader, "TransactionDate"),
        Status = Enum.Parse<SettlementRecordStatus>(GetString(reader, "Status")),
        MatchedCmsTransactionId = GetNullableGuid(reader, "MatchedCmsTransactionId"),
        ResponseCode = GetString(reader, "ResponseCode"),
        Narrative = GetString(reader, "Narrative")
    };

    private static ReconciliationException ReadReconciliationException(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        BatchId = GetNullableGuid(reader, "BatchId"),
        SettlementRecordId = GetNullableGuid(reader, "SettlementRecordId"),
        ExceptionType = Enum.Parse<ReconciliationExceptionType>(GetString(reader, "ExceptionType")),
        Status = Enum.Parse<ReconciliationExceptionStatus>(GetString(reader, "Status")),
        Severity = GetString(reader, "Severity"),
        CorrelationId = GetString(reader, "CorrelationId"),
        Reference = GetString(reader, "Reference"),
        ExpectedAmount = reader.GetDecimal(reader.GetOrdinal("ExpectedAmount")),
        ActualAmount = reader.GetDecimal(reader.GetOrdinal("ActualAmount")),
        DifferenceAmount = reader.GetDecimal(reader.GetOrdinal("DifferenceAmount")),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        Reason = GetString(reader, "Reason"),
        AssignedTo = GetString(reader, "AssignedTo"),
        ResolutionNotes = GetString(reader, "ResolutionNotes"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        ResolvedAt = GetNullableDateTimeOffset(reader, "ResolvedAt")
    };

    private static GlJournalEntry ReadGlJournal(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        JournalNumber = GetString(reader, "JournalNumber"),
        CorrelationId = GetString(reader, "CorrelationId"),
        SourceModule = GetString(reader, "SourceModule"),
        Reference = GetString(reader, "Reference"),
        Narrative = GetString(reader, "Narrative"),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        DebitTotal = reader.GetDecimal(reader.GetOrdinal("DebitTotal")),
        CreditTotal = reader.GetDecimal(reader.GetOrdinal("CreditTotal")),
        Status = Enum.Parse<GlJournalStatus>(GetString(reader, "Status")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        PostedAt = GetNullableDateTimeOffset(reader, "PostedAt")
    };

    private static GlJournalLine ReadGlLine(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        JournalEntryId = reader.GetGuid(reader.GetOrdinal("JournalEntryId")),
        AccountCode = GetString(reader, "AccountCode"),
        Direction = Enum.Parse<LedgerEntryDirection>(GetString(reader, "Direction")),
        Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        Narrative = GetString(reader, "Narrative")
    };

    private static FinancialOperation ReadFinancialOperation(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        OperationType = Enum.Parse<FinancialOperationType>(GetString(reader, "OperationType")),
        Status = Enum.Parse<FinancialOperationStatus>(GetString(reader, "Status")),
        CardId = GetNullableGuid(reader, "CardId"),
        WalletAccountId = GetNullableGuid(reader, "WalletAccountId"),
        OriginalCmsTransactionId = GetNullableGuid(reader, "OriginalCmsTransactionId"),
        OriginalRrn = GetString(reader, "OriginalRrn"),
        OriginalStan = GetString(reader, "OriginalStan"),
        NewRrn = GetString(reader, "NewRrn"),
        NewStan = GetString(reader, "NewStan"),
        MaskedPan = GetString(reader, "MaskedPan"),
        PanHash = GetString(reader, "PanHash"),
        Direction = Enum.Parse<FinancialAdjustmentDirection>(GetString(reader, "Direction")),
        Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
        FeeAmount = reader.GetDecimal(reader.GetOrdinal("FeeAmount")),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        Reason = GetString(reader, "Reason"),
        TicketReference = GetString(reader, "TicketReference"),
        Maker = GetString(reader, "Maker"),
        Checker = GetString(reader, "Checker"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        ApprovedAt = GetNullableDateTimeOffset(reader, "ApprovedAt"),
        PostedAt = GetNullableDateTimeOffset(reader, "PostedAt")
    };

    private static SettlementStatement ReadSettlementStatement(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        PartyType = Enum.Parse<SettlementPartyType>(GetString(reader, "PartyType")),
        PartyId = reader.GetGuid(reader.GetOrdinal("PartyId")),
        StatementNumber = GetString(reader, "StatementNumber"),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        PeriodStart = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("PeriodStart"))),
        PeriodEnd = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("PeriodEnd"))),
        GrossDebitAmount = reader.GetDecimal(reader.GetOrdinal("GrossDebitAmount")),
        GrossCreditAmount = reader.GetDecimal(reader.GetOrdinal("GrossCreditAmount")),
        FeeAmount = reader.GetDecimal(reader.GetOrdinal("FeeAmount")),
        CommissionAmount = reader.GetDecimal(reader.GetOrdinal("CommissionAmount")),
        NetSettlementAmount = reader.GetDecimal(reader.GetOrdinal("NetSettlementAmount")),
        Status = Enum.Parse<SettlementStatementStatus>(GetString(reader, "Status")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        PostedAt = GetNullableDateTimeOffset(reader, "PostedAt")
    };

    private static SettlementStatementLine ReadSettlementStatementLine(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        SettlementStatementId = reader.GetGuid(reader.GetOrdinal("SettlementStatementId")),
        TransactionDate = GetDateTimeOffset(reader, "TransactionDate"),
        SourceType = GetString(reader, "SourceType"),
        Reference = GetString(reader, "Reference"),
        Narrative = GetString(reader, "Narrative"),
        Direction = Enum.Parse<LedgerEntryDirection>(GetString(reader, "Direction")),
        Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
        CurrencyCode = GetString(reader, "CurrencyCode")
    };

    private static string GetString(NpgsqlDataReader reader, string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? string.Empty : reader.GetString(reader.GetOrdinal(name));
    private static Guid? GetNullableGuid(NpgsqlDataReader reader, string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetGuid(reader.GetOrdinal(name));
    private static DateTimeOffset GetDateTimeOffset(NpgsqlDataReader reader, string name) => reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal(name));
    private static DateTimeOffset? GetNullableDateTimeOffset(NpgsqlDataReader reader, string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal(name));
}
