using BankSwitch.Application;
using BankSwitch.Domain;
using Npgsql;

namespace BankSwitch.Infrastructure;

public sealed class SqlOperationalControlRepository : IOperationalControlRepository
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;
    public SqlOperationalControlRepository(SecurePostgresConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

public async Task AddAgencyAsync(AgencyProfile agency, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.agencyprofiles
        (id, agencycode, name, parentagencyid, contactname, mobilenumber, email, countrycode, creditmode, creditlimit, availablecredit, usedcredit, reservedcredit, commissionprofilecode, settlementaccountnumber, status, createdat)
        VALUES (@Id, @AgencyCode, @Name, @ParentAgencyId, @ContactName, @MobileNumber, @Email, @CountryCode, @CreditMode, @CreditLimit, @AvailableCredit, @UsedCredit, @ReservedCredit, @CommissionProfileCode, @SettlementAccountNumber, @Status, @CreatedAt)
        """, connection);

    BindAgency(command, agency);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

public async Task UpdateAgencyAsync(AgencyProfile agency, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        UPDATE dbo.agencyprofiles
        SET name=@Name, parentagencyid=@ParentAgencyId, contactname=@ContactName, mobilenumber=@MobileNumber, email=@Email, countrycode=@CountryCode,
            creditmode=@CreditMode, creditlimit=@CreditLimit, availablecredit=@AvailableCredit, usedcredit=@UsedCredit, reservedcredit=@ReservedCredit,
            commissionprofilecode=@CommissionProfileCode, settlementaccountnumber=@SettlementAccountNumber, status=@Status
        WHERE id=@Id
        """, connection);

    BindAgency(command, agency);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
    public async Task<AgencyProfile?> GetAgencyAsync(Guid agencyId, CancellationToken cancellationToken = default)
    {
        return await QuerySingleAsync("SELECT * FROM dbo.agencyprofiles WHERE id=@Id  LIMIT 1", AddId, ReadAgency, agencyId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AgencyProfile?> GetAgencyByCodeAsync(string agencyCode, CancellationToken cancellationToken = default)
    {
        return await QuerySingleAsync("SELECT * FROM dbo.agencyprofiles WHERE agencycode=@Code  LIMIT 1", c => c.Parameters.AddWithValue("@Code", agencyCode ?? string.Empty), ReadAgency, cancellationToken).ConfigureAwait(false);
    }
public async Task AddAgencyCreditLedgerEntryAsync(
    AgencyCreditLedgerEntry entry,
    CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.agencycreditledgerentries
        (id, agencyid, direction, amount, availablecreditafter, reference, narrative, correlationid, createdat)
        VALUES (@Id, @AgencyId, @Direction, @Amount, @AvailableCreditAfter, @Reference, @Narrative, @CorrelationId, @CreatedAt)
        """, connection);

    command.Parameters.AddWithValue("@Id", entry.Id);
    command.Parameters.AddWithValue("@AgencyId", entry.AgencyId);
    command.Parameters.AddWithValue("@Direction", entry.Direction.ToString());
    command.Parameters.AddWithValue("@Amount", entry.Amount);
    command.Parameters.AddWithValue("@AvailableCreditAfter", entry.AvailableCreditAfter);
    command.Parameters.AddWithValue("@Reference", entry.Reference);
    command.Parameters.AddWithValue("@Narrative", entry.Narrative);
    command.Parameters.AddWithValue("@CorrelationId", entry.CorrelationId);
    command.Parameters.AddWithValue("@CreatedAt", entry.CreatedAt);

    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

public async Task AddCorporateAsync(
    CorporateProfile corporate,
    CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.corporateprofiles
        (id, corporatecode, name, registrationnumber, contactname, mobilenumber, email, currencycode, riskrating, fundingbalance, availablefundingbalance, status, createdat)
        VALUES (@Id, @CorporateCode, @Name, @RegistrationNumber, @ContactName, @MobileNumber, @Email, @CurrencyCode, @RiskRating, @FundingBalance, @AvailableFundingBalance, @Status, @CreatedAt)
        """, connection);

    BindCorporate(command, corporate);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
    public async Task UpdateCorporateAsync(CorporateProfile corporate, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            UPDATE dbo.corporateprofiles
            SET name=@Name, registrationnumber=@RegistrationNumber, contactname=@ContactName, mobilenumber=@MobileNumber, email=@Email, currencycode=@CurrencyCode,
                riskrating=@RiskRating, fundingbalance=@FundingBalance, availablfundingbalance=@AvailableFundingBalance, status=@Status
            WHERE id=@Id
            """, connection);
        BindCorporate(command, corporate);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CorporateProfile?> GetCorporateAsync(Guid corporateId, CancellationToken cancellationToken = default)
    {
        return await QuerySingleAsync("SELECT * FROM dbo.corporateprofiles WHERE id=@Id  LIMIT 1", AddId, ReadCorporate, corporateId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CorporateProfile?> GetCorporateByCodeAsync(string corporateCode, CancellationToken cancellationToken = default)
    {
        return await QuerySingleAsync("SELECT * FROM dbo.corporateprofiles WHERE corporatecode=@Code  LIMIT 1", c => c.Parameters.AddWithValue("@Code", corporateCode ?? string.Empty), ReadCorporate, cancellationToken).ConfigureAwait(false);
    }
public async Task AddDepartmentAsync(
    CorporateDepartment department,
    CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.corporatedepartments
        (id, corporateid, departmentcode, name, costcentercode, status, createdat)
        VALUES (@Id, @CorporateId, @DepartmentCode, @Name, @CostCenterCode, @Status, @CreatedAt)
        """, connection);

    BindDepartment(command, department);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

public async Task<CorporateDepartment?> GetDepartmentAsync(
    Guid departmentId,
    CancellationToken cancellationToken = default)
{
    return await QuerySingleAsync(
        "SELECT * FROM dbo.corporatedepartments WHERE id=@Id LIMIT 1",
        AddId,
        ReadDepartment,
        departmentId,
        cancellationToken).ConfigureAwait(false);
}
    public async Task<CorporateDepartment?> GetDepartmentByCodeAsync(Guid corporateId, string departmentCode, CancellationToken cancellationToken = default)
    {
        return await QuerySingleAsync("SELECT  * FROM dbo.corporatedepartments WHERE corporateid=@CorporateId AND departmentcode=@DepartmentCode  LIMIT 1", c => { c.Parameters.AddWithValue("@CorporateId", corporateId); c.Parameters.AddWithValue("@DepartmentCode", departmentCode ?? string.Empty); }, ReadDepartment, cancellationToken).ConfigureAwait(false);
    }
public async Task AddEmployeeAsync(
    CorporateEmployee employee,
    CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.corporateemployees
        (id, corporateid, departmentid, employeenumber, fullname, mobilenumber, email, status, createdat)
        VALUES (@Id, @CorporateId, @DepartmentId, @EmployeeNumber, @FullName, @MobileNumber, @Email, @Status, @CreatedAt)
        """, connection);

    BindEmployee(command, employee);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
    public async Task<CorporateEmployee?> GetEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        return await QuerySingleAsync("SELECT  * FROM dbo.corporateemployees WHERE id=@Id  LIMIT 1", AddId, ReadEmployee, employeeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CorporateEmployee?> GetEmployeeByNumberAsync(Guid corporateId, string employeeNumber, CancellationToken cancellationToken = default)
    {
        return await QuerySingleAsync("SELECT * FROM dbo.corporateemployees WHERE corporateid=@CorporateId AND employeenumber=@EmployeeNumber  LIMIT 1", c => { c.Parameters.AddWithValue("@CorporateId", corporateId); c.Parameters.AddWithValue("@EmployeeNumber", employeeNumber ?? string.Empty); }, ReadEmployee, cancellationToken).ConfigureAwait(false);
    }
public async Task AddCorporateBudgetAsync(
    CorporateBudget budget,
    CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.corporatebudgets
        (id, corporateid, departmentid, employeeid, budgetcode, currencycode, budgetamount, availableamount, periodstart, periodend, status, createdat)
        VALUES (@Id, @CorporateId, @DepartmentId, @EmployeeId, @BudgetCode, @CurrencyCode, @BudgetAmount, @AvailableAmount, @PeriodStart, @PeriodEnd, @Status, @CreatedAt)
        """, connection);

    BindBudget(command, budget);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

public async Task<CorporateBudget?> GetCorporateBudgetAsync(
    Guid budgetId,
    CancellationToken cancellationToken = default)
{
    return await QuerySingleAsync(
        "SELECT * FROM dbo.corporatebudgets WHERE id=@Id LIMIT 1",
        AddId,
        ReadBudget,
        budgetId,
        cancellationToken).ConfigureAwait(false);
}
public async Task<IReadOnlyList<CorporateBudget>> GetActiveBudgetsAsync(
    Guid corporateId,
    Guid? departmentId,
    Guid? employeeId,
    DateOnly businessDate,
    CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        SELECT * FROM dbo.corporatebudgets
        WHERE corporateid=@CorporateId AND status='Active' AND periodstart<=@BusinessDate AND periodend>=@BusinessDate
          AND (departmentid IS NULL OR departmentid=@DepartmentId) AND (employeeid IS NULL OR employeeid=@EmployeeId)
        ORDER BY CASE WHEN employeeid IS NOT NULL THEN 0 WHEN departmentid IS NOT NULL THEN 1 ELSE 2 END
        """, connection);

    command.Parameters.AddWithValue("@CorporateId", corporateId);
    command.Parameters.AddWithValue("@DepartmentId", (object?)departmentId ?? DBNull.Value);
    command.Parameters.AddWithValue("@EmployeeId", (object?)employeeId ?? DBNull.Value);
    command.Parameters.AddWithValue("@BusinessDate", businessDate.ToDateTime(TimeOnly.MinValue));

    return await QueryListAsync(command, ReadBudget, cancellationToken).ConfigureAwait(false);
}

public async Task AddCardStockBatchAsync(
    CardStockBatch batch,
    CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.cardstockbatches
        (id, productid, batchreference, ownertype, ownerid, quantity, availablequantity, reservedquantity, issuedquantity, status, createdat)
        VALUES (@Id, @ProductId, @BatchReference, @OwnerType, @OwnerId, @Quantity, @AvailableQuantity, @ReservedQuantity, @IssuedQuantity, @Status, @CreatedAt)
        """, connection);

    BindStock(command, batch);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

public async Task UpdateCardStockBatchAsync(
    CardStockBatch batch,
    CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        UPDATE dbo.cardstockbatches
        SET availablequantity=@AvailableQuantity,
            reservedquantity=@ReservedQuantity,
            issuedquantity=@IssuedQuantity,
            status=@Status
        WHERE id=@Id
        """, connection);

    command.Parameters.AddWithValue("@Id", batch.Id);
    command.Parameters.AddWithValue("@AvailableQuantity", batch.AvailableQuantity);
    command.Parameters.AddWithValue("@ReservedQuantity", batch.ReservedQuantity);
    command.Parameters.AddWithValue("@IssuedQuantity", batch.IssuedQuantity);
    command.Parameters.AddWithValue("@Status", batch.Status.ToString());

    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

public async Task<CardStockBatch?> GetCardStockBatchByReferenceAsync(
    string batchReference,
    CancellationToken cancellationToken = default)
{
    return await QuerySingleAsync(
        "SELECT * FROM dbo.cardstockbatches WHERE batchreference=@BatchReference LIMIT 1",
        c => c.Parameters.AddWithValue("@BatchReference", batchReference ?? string.Empty),
        ReadStock,
        cancellationToken).ConfigureAwait(false);
}
public async Task<IReadOnlyList<CardStockBatch>> GetCardStockBatchesAsync(
    Guid productId,
    CardOwnerType? ownerType,
    Guid? ownerId,
    CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        SELECT * FROM dbo.cardstockbatches
        WHERE productid=@ProductId
          AND (@OwnerType IS NULL OR ownertype=@OwnerType)
          AND (@OwnerId IS NULL OR ownerid=@OwnerId)
        ORDER BY createdat
        """, connection);

    command.Parameters.AddWithValue("@ProductId", productId);
    command.Parameters.AddWithValue("@OwnerType", ownerType.HasValue ? (object)ownerType.Value.ToString() : DBNull.Value);
    command.Parameters.AddWithValue("@OwnerId", (object?)ownerId ?? DBNull.Value);

    return await QueryListAsync(command, ReadStock, cancellationToken).ConfigureAwait(false);
}

public async Task AddAdvancedLimitRuleAsync(
    AdvancedLimitRule rule,
    CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.advancedlimitrules
        (id, rulecode, name, scope, scopeid, transactiontypecode, channelcode, currencycode, period, amountlimit, countlimit, priority, isactive, createdat)
        VALUES (@Id, @RuleCode, @Name, @Scope, @ScopeId, @TransactionTypeCode, @ChannelCode, @CurrencyCode, @Period, @AmountLimit, @CountLimit, @Priority, @IsActive, @CreatedAt)
        """, connection);

    BindLimitRule(command, rule);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
    public async Task<IReadOnlyList<AdvancedLimitRule>> GetActiveAdvancedLimitRulesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.advancedlimitrules WHERE isactive=1 ORDER BY priority, createdat", connection);
        return await QueryListAsync(command, ReadLimitRule, cancellationToken).ConfigureAwait(false);
    }
    public async Task AddRiskRuleAsync(BankSwitch.Domain.RiskRule rule, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            INSERT INTO dbo.RiskRules
            (Id, RuleCode, Name, RuleType, MatchValue, Action, ResponseCode, AmountThreshold, Priority, IsActive, AlertTemplateCode, CreatedAt)
            VALUES (@Id, @RuleCode, @Name, @RuleType, @MatchValue, @Action, @ResponseCode, @AmountThreshold, @Priority, @IsActive, @AlertTemplateCode, @CreatedAt)
            """, connection);
        BindRiskRule(command, rule);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BankSwitch.Domain.RiskRule>> GetActiveRiskRulesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.RiskRules WHERE IsActive=1 ORDER BY Priority, CreatedAt", connection);
        return await QueryListAsync(command, ReadRiskRule, cancellationToken).ConfigureAwait(false);
    }

    

public async Task AddNotificationAsync(
    NotificationMessage notification,
    CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.notificationmessages
        (id, channel, recipient, templatecode, subject, payloadjson, reference, correlationid, status, attempts, createdat, sentat)
        VALUES (@Id, @Channel, @Recipient, @TemplateCode, @Subject, @PayloadJson, @Reference, @CorrelationId, @Status, @Attempts, @CreatedAt, @SentAt)
        """, connection);

    BindNotification(command, notification);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
public async Task<IReadOnlyList<NotificationMessage>> GetPendingNotificationsAsync(int take, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("SELECT * FROM dbo.notificationmessages WHERE status='Pending' ORDER BY createdat LIMIT @Take", connection);
    command.Parameters.AddWithValue("@Take", take);
    return await QueryListAsync(command, ReadNotification, cancellationToken).ConfigureAwait(false);
}

public async Task UpdateNotificationAsync(NotificationMessage notification, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var command = new NpgsqlCommand("""
        UPDATE dbo.notificationmessages SET status=@Status, attempts=@Attempts, sentat=@SentAt WHERE id=@Id
        """, connection);
    command.Parameters.AddWithValue("@Id", notification.Id);
    command.Parameters.AddWithValue("@Status", notification.Status.ToString());
    command.Parameters.AddWithValue("@Attempts", notification.Attempts);
    command.Parameters.AddWithValue("@SentAt", (object?)notification.SentAt ?? DBNull.Value);
    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

public async Task AddStatementAsync(StatementDocument statement, IReadOnlyCollection<StatementLine> lines, CancellationToken cancellationToken = default)
{
    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO dbo.statementdocuments
            (id, ownertype, ownerid, statementnumber, currencycode, periodstart, periodend, openingbalance, closingbalance, debittotal, credittotal, transactioncount, status, createdat)
            VALUES (@Id, @OwnerType, @OwnerId, @StatementNumber, @CurrencyCode, @PeriodStart, @PeriodEnd, @OpeningBalance, @ClosingBalance, @DebitTotal, @CreditTotal, @TransactionCount, @Status, @CreatedAt)
            """, connection, (NpgsqlTransaction)transaction);
        BindStatement(command, statement);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        foreach (var line in lines)
        {
            await using var lineCommand = new NpgsqlCommand("""
                INSERT INTO dbo.statementlines
                (id, statementid, transactiondate, reference, narrative, direction, amount, balanceafter)
                VALUES (@Id, @StatementId, @TransactionDate, @Reference, @Narrative, @Direction, @Amount, @BalanceAfter)
                """, connection, (NpgsqlTransaction)transaction);
            BindStatementLine(lineCommand, line);
            await lineCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
    catch
    {
        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        throw;
    }
}

public async Task<StatementDocument?> GetStatementAsync(Guid statementId, CancellationToken cancellationToken = default)
{
    return await QuerySingleAsync("SELECT * FROM dbo.statementdocuments WHERE id=@Id LIMIT 1", AddId, ReadStatement, statementId, cancellationToken).ConfigureAwait(false);
}
    public async Task<IReadOnlyList<StatementLine>> GetStatementLinesAsync(Guid statementId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.statementlines WHERE statementid=@StatementId ORDER BY transactiondate, id", connection);
        command.Parameters.AddWithValue("@StatementId", statementId);
        return await QueryListAsync(command, ReadStatementLine, cancellationToken).ConfigureAwait(false);
    }

    private static void AddId(NpgsqlCommand command, Guid id) => command.Parameters.AddWithValue("@Id", id);

    private async Task<T?> QuerySingleAsync<T>(string sql, Action<NpgsqlCommand> bind, Func<NpgsqlDataReader, T> read, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        bind(command);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? read(reader) : default;
    }

    private async Task<T?> QuerySingleAsync<T>(string sql, Action<NpgsqlCommand, Guid> bind, Func<NpgsqlDataReader, T> read, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        bind(command, id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? read(reader) : default;
    }

    private static async Task<IReadOnlyList<T>> QueryListAsync<T>(NpgsqlCommand command, Func<NpgsqlDataReader, T> read, CancellationToken cancellationToken)
    {
        var list = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) list.Add(read(reader));
        return list;
    }

    private static void BindAgency(NpgsqlCommand command, AgencyProfile agency)
    {
        command.Parameters.AddWithValue("@Id", agency.Id);
        command.Parameters.AddWithValue("@AgencyCode", agency.AgencyCode);
        command.Parameters.AddWithValue("@Name", agency.Name);
        command.Parameters.AddWithValue("@ParentAgencyId", (object?)agency.ParentAgencyId ?? DBNull.Value);
        command.Parameters.AddWithValue("@ContactName", agency.ContactName);
        command.Parameters.AddWithValue("@MobileNumber", agency.MobileNumber);
        command.Parameters.AddWithValue("@Email", agency.Email);
        command.Parameters.AddWithValue("@CountryCode", agency.CountryCode);
        command.Parameters.AddWithValue("@CreditMode", agency.CreditMode.ToString());
        command.Parameters.AddWithValue("@CreditLimit", agency.CreditLimit);
        command.Parameters.AddWithValue("@AvailableCredit", agency.AvailableCredit);
        command.Parameters.AddWithValue("@UsedCredit", agency.UsedCredit);
        command.Parameters.AddWithValue("@ReservedCredit", agency.ReservedCredit);
        command.Parameters.AddWithValue("@CommissionProfileCode", agency.CommissionProfileCode);
        command.Parameters.AddWithValue("@SettlementAccountNumber", agency.SettlementAccountNumber);
        command.Parameters.AddWithValue("@Status", agency.Status.ToString());
        command.Parameters.AddWithValue("@CreatedAt", agency.CreatedAt);
    }

    private static void BindCorporate(NpgsqlCommand command, CorporateProfile corporate)
    {
        command.Parameters.AddWithValue("@Id", corporate.Id);
        command.Parameters.AddWithValue("@CorporateCode", corporate.CorporateCode);
        command.Parameters.AddWithValue("@Name", corporate.Name);
        command.Parameters.AddWithValue("@RegistrationNumber", corporate.RegistrationNumber);
        command.Parameters.AddWithValue("@ContactName", corporate.ContactName);
        command.Parameters.AddWithValue("@MobileNumber", corporate.MobileNumber);
        command.Parameters.AddWithValue("@Email", corporate.Email);
        command.Parameters.AddWithValue("@CurrencyCode", corporate.CurrencyCode);
        command.Parameters.AddWithValue("@RiskRating", corporate.RiskRating);
        command.Parameters.AddWithValue("@FundingBalance", corporate.FundingBalance);
        command.Parameters.AddWithValue("@AvailableFundingBalance", corporate.AvailableFundingBalance);
        command.Parameters.AddWithValue("@Status", corporate.Status.ToString());
        command.Parameters.AddWithValue("@CreatedAt", corporate.CreatedAt);
    }

    private static void BindDepartment(NpgsqlCommand command, CorporateDepartment department)
    {
        command.Parameters.AddWithValue("@Id", department.Id);
        command.Parameters.AddWithValue("@CorporateId", department.CorporateId);
        command.Parameters.AddWithValue("@DepartmentCode", department.DepartmentCode);
        command.Parameters.AddWithValue("@Name", department.Name);
        command.Parameters.AddWithValue("@CostCenterCode", department.CostCenterCode);
        command.Parameters.AddWithValue("@Status", department.Status.ToString());
        command.Parameters.AddWithValue("@CreatedAt", department.CreatedAt);
    }

    private static void BindEmployee(NpgsqlCommand command, CorporateEmployee employee)
    {
        command.Parameters.AddWithValue("@Id", employee.Id);
        command.Parameters.AddWithValue("@CorporateId", employee.CorporateId);
        command.Parameters.AddWithValue("@DepartmentId", (object?)employee.DepartmentId ?? DBNull.Value);
        command.Parameters.AddWithValue("@EmployeeNumber", employee.EmployeeNumber);
        command.Parameters.AddWithValue("@FullName", employee.FullName);
        command.Parameters.AddWithValue("@MobileNumber", employee.MobileNumber);
        command.Parameters.AddWithValue("@Email", employee.Email);
        command.Parameters.AddWithValue("@Status", employee.Status.ToString());
        command.Parameters.AddWithValue("@CreatedAt", employee.CreatedAt);
    }

    private static void BindBudget(NpgsqlCommand command, CorporateBudget budget)
    {
        command.Parameters.AddWithValue("@Id", budget.Id);
        command.Parameters.AddWithValue("@CorporateId", budget.CorporateId);
        command.Parameters.AddWithValue("@DepartmentId", (object?)budget.DepartmentId ?? DBNull.Value);
        command.Parameters.AddWithValue("@EmployeeId", (object?)budget.EmployeeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@BudgetCode", budget.BudgetCode);
        command.Parameters.AddWithValue("@CurrencyCode", budget.CurrencyCode);
        command.Parameters.AddWithValue("@BudgetAmount", budget.BudgetAmount);
        command.Parameters.AddWithValue("@AvailableAmount", budget.AvailableAmount);
        command.Parameters.AddWithValue("@PeriodStart", budget.PeriodStart.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@PeriodEnd", budget.PeriodEnd.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@Status", budget.Status.ToString());
        command.Parameters.AddWithValue("@CreatedAt", budget.CreatedAt);
    }

    private static void BindStock(NpgsqlCommand command, CardStockBatch batch)
    {
        command.Parameters.AddWithValue("@Id", batch.Id);
        command.Parameters.AddWithValue("@ProductId", batch.ProductId);
        command.Parameters.AddWithValue("@BatchReference", batch.BatchReference);
        command.Parameters.AddWithValue("@OwnerType", batch.OwnerType.ToString());
        command.Parameters.AddWithValue("@OwnerId", (object?)batch.OwnerId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Quantity", batch.Quantity);
        command.Parameters.AddWithValue("@AvailableQuantity", batch.AvailableQuantity);
        command.Parameters.AddWithValue("@ReservedQuantity", batch.ReservedQuantity);
        command.Parameters.AddWithValue("@IssuedQuantity", batch.IssuedQuantity);
        command.Parameters.AddWithValue("@Status", batch.Status.ToString());
        command.Parameters.AddWithValue("@CreatedAt", batch.CreatedAt);
    }

    private static void BindLimitRule(NpgsqlCommand command, AdvancedLimitRule rule)
    {
        command.Parameters.AddWithValue("@Id", rule.Id);
        command.Parameters.AddWithValue("@RuleCode", rule.RuleCode);
        command.Parameters.AddWithValue("@Name", rule.Name);
        command.Parameters.AddWithValue("@Scope", rule.Scope.ToString());
        command.Parameters.AddWithValue("@ScopeId", (object?)rule.ScopeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@TransactionTypeCode", rule.TransactionTypeCode);
        command.Parameters.AddWithValue("@ChannelCode", rule.ChannelCode);
        command.Parameters.AddWithValue("@CurrencyCode", rule.CurrencyCode);
        command.Parameters.AddWithValue("@Period", rule.Period.ToString());
        command.Parameters.AddWithValue("@AmountLimit", rule.AmountLimit);
        command.Parameters.AddWithValue("@CountLimit", rule.CountLimit);
        command.Parameters.AddWithValue("@Priority", rule.Priority);
        command.Parameters.AddWithValue("@IsActive", rule.IsActive);
        command.Parameters.AddWithValue("@CreatedAt", rule.CreatedAt);
    }

    private static void BindRiskRule(NpgsqlCommand command, BankSwitch.Domain.RiskRule rule)
    {
        command.Parameters.AddWithValue("@Id", rule.Id);
        command.Parameters.AddWithValue("@RuleCode", rule.RuleCode);
        command.Parameters.AddWithValue("@Name", rule.Name);
        command.Parameters.AddWithValue("@RuleType", rule.RuleType.ToString());
        command.Parameters.AddWithValue("@MatchValue", rule.MatchValue);
        command.Parameters.AddWithValue("@Action", rule.Action.ToString());
        command.Parameters.AddWithValue("@ResponseCode", rule.ResponseCode);
        command.Parameters.AddWithValue("@AmountThreshold", (object?)rule.AmountThreshold ?? DBNull.Value);
        command.Parameters.AddWithValue("@Priority", rule.Priority);
        command.Parameters.AddWithValue("@IsActive", rule.Enabled);
        command.Parameters.AddWithValue("@AlertTemplateCode", rule.AlertTemplateCode);
        command.Parameters.AddWithValue("@CreatedAt", rule.CreatedAt);
    }

    private static void BindNotification(NpgsqlCommand command, NotificationMessage notification)
    {
        command.Parameters.AddWithValue("@Id", notification.Id);
        command.Parameters.AddWithValue("@Channel", notification.Channel.ToString());
        command.Parameters.AddWithValue("@Recipient", notification.Recipient);
        command.Parameters.AddWithValue("@TemplateCode", notification.TemplateCode);
        command.Parameters.AddWithValue("@Subject", notification.Subject);
        command.Parameters.AddWithValue("@PayloadJson", notification.PayloadJson);
        command.Parameters.AddWithValue("@Reference", notification.Reference);
        command.Parameters.AddWithValue("@CorrelationId", notification.CorrelationId);
        command.Parameters.AddWithValue("@Status", notification.Status.ToString());
        command.Parameters.AddWithValue("@Attempts", notification.Attempts);
        command.Parameters.AddWithValue("@CreatedAt", notification.CreatedAt);
        command.Parameters.AddWithValue("@SentAt", (object?)notification.SentAt ?? DBNull.Value);
    }

    private static void BindStatement(NpgsqlCommand command, StatementDocument statement)
    {
        command.Parameters.AddWithValue("@Id", statement.Id);
        command.Parameters.AddWithValue("@OwnerType", statement.OwnerType.ToString());
        command.Parameters.AddWithValue("@OwnerId", statement.OwnerId);
        command.Parameters.AddWithValue("@StatementNumber", statement.StatementNumber);
        command.Parameters.AddWithValue("@CurrencyCode", statement.CurrencyCode);
        command.Parameters.AddWithValue("@PeriodStart", statement.PeriodStart.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@PeriodEnd", statement.PeriodEnd.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@OpeningBalance", statement.OpeningBalance);
        command.Parameters.AddWithValue("@ClosingBalance", statement.ClosingBalance);
        command.Parameters.AddWithValue("@DebitTotal", statement.DebitTotal);
        command.Parameters.AddWithValue("@CreditTotal", statement.CreditTotal);
        command.Parameters.AddWithValue("@TransactionCount", statement.TransactionCount);
        command.Parameters.AddWithValue("@Status", statement.Status.ToString());
        command.Parameters.AddWithValue("@CreatedAt", statement.CreatedAt);
    }

    private static void BindStatementLine(NpgsqlCommand command, StatementLine line)
    {
        command.Parameters.AddWithValue("@Id", line.Id);
        command.Parameters.AddWithValue("@StatementId", line.StatementId);
        command.Parameters.AddWithValue("@TransactionDate", line.TransactionDate);
        command.Parameters.AddWithValue("@Reference", line.Reference);
        command.Parameters.AddWithValue("@Narrative", line.Narrative);
        command.Parameters.AddWithValue("@Direction", line.Direction.ToString());
        command.Parameters.AddWithValue("@Amount", line.Amount);
        command.Parameters.AddWithValue("@BalanceAfter", line.BalanceAfter);
    }

    private static AgencyProfile ReadAgency(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        AgencyCode = GetString(reader, "AgencyCode"),
        Name = GetString(reader, "Name"),
        ParentAgencyId = GetNullableGuid(reader, "ParentAgencyId"),
        ContactName = GetString(reader, "ContactName"),
        MobileNumber = GetString(reader, "MobileNumber"),
        Email = GetString(reader, "Email"),
        CountryCode = GetString(reader, "CountryCode"),
        CreditMode = Enum.Parse<AgencyCreditMode>(GetString(reader, "CreditMode")),
        CreditLimit = reader.GetDecimal(reader.GetOrdinal("CreditLimit")),
        AvailableCredit = reader.GetDecimal(reader.GetOrdinal("AvailableCredit")),
        UsedCredit = reader.GetDecimal(reader.GetOrdinal("UsedCredit")),
        ReservedCredit = reader.GetDecimal(reader.GetOrdinal("ReservedCredit")),
        CommissionProfileCode = GetString(reader, "CommissionProfileCode"),
        SettlementAccountNumber = GetString(reader, "SettlementAccountNumber"),
        Status = Enum.Parse<OperationalStatus>(GetString(reader, "Status")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static CorporateProfile ReadCorporate(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        CorporateCode = GetString(reader, "CorporateCode"),
        Name = GetString(reader, "Name"),
        RegistrationNumber = GetString(reader, "RegistrationNumber"),
        ContactName = GetString(reader, "ContactName"),
        MobileNumber = GetString(reader, "MobileNumber"),
        Email = GetString(reader, "Email"),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        RiskRating = GetString(reader, "RiskRating"),
        FundingBalance = reader.GetDecimal(reader.GetOrdinal("FundingBalance")),
        AvailableFundingBalance = reader.GetDecimal(reader.GetOrdinal("AvailableFundingBalance")),
        Status = Enum.Parse<OperationalStatus>(GetString(reader, "Status")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static CorporateDepartment ReadDepartment(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        CorporateId = reader.GetGuid(reader.GetOrdinal("CorporateId")),
        DepartmentCode = GetString(reader, "DepartmentCode"),
        Name = GetString(reader, "Name"),
        CostCenterCode = GetString(reader, "CostCenterCode"),
        Status = Enum.Parse<OperationalStatus>(GetString(reader, "Status")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static CorporateEmployee ReadEmployee(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        CorporateId = reader.GetGuid(reader.GetOrdinal("CorporateId")),
        DepartmentId = GetNullableGuid(reader, "DepartmentId"),
        EmployeeNumber = GetString(reader, "EmployeeNumber"),
        FullName = GetString(reader, "FullName"),
        MobileNumber = GetString(reader, "MobileNumber"),
        Email = GetString(reader, "Email"),
        Status = Enum.Parse<OperationalStatus>(GetString(reader, "Status")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static CorporateBudget ReadBudget(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        CorporateId = reader.GetGuid(reader.GetOrdinal("CorporateId")),
        DepartmentId = GetNullableGuid(reader, "DepartmentId"),
        EmployeeId = GetNullableGuid(reader, "EmployeeId"),
        BudgetCode = GetString(reader, "BudgetCode"),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        BudgetAmount = reader.GetDecimal(reader.GetOrdinal("BudgetAmount")),
        AvailableAmount = reader.GetDecimal(reader.GetOrdinal("AvailableAmount")),
        PeriodStart = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("PeriodStart"))),
        PeriodEnd = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("PeriodEnd"))),
        Status = Enum.Parse<OperationalStatus>(GetString(reader, "Status")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static CardStockBatch ReadStock(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        ProductId = reader.GetGuid(reader.GetOrdinal("ProductId")),
        BatchReference = GetString(reader, "BatchReference"),
        OwnerType = Enum.Parse<CardOwnerType>(GetString(reader, "OwnerType")),
        OwnerId = GetNullableGuid(reader, "OwnerId"),
        Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
        AvailableQuantity = reader.GetInt32(reader.GetOrdinal("AvailableQuantity")),
        ReservedQuantity = reader.GetInt32(reader.GetOrdinal("ReservedQuantity")),
        IssuedQuantity = reader.GetInt32(reader.GetOrdinal("IssuedQuantity")),
        Status = Enum.Parse<CardStockStatus>(GetString(reader, "Status")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static AdvancedLimitRule ReadLimitRule(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        RuleCode = GetString(reader, "RuleCode"),
        Name = GetString(reader, "Name"),
        Scope = Enum.Parse<LimitScope>(GetString(reader, "Scope")),
        ScopeId = GetNullableGuid(reader, "ScopeId"),
        TransactionTypeCode = GetString(reader, "TransactionTypeCode"),
        ChannelCode = GetString(reader, "ChannelCode"),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        Period = Enum.Parse<LimitPeriod>(GetString(reader, "Period")),
        AmountLimit = reader.GetDecimal(reader.GetOrdinal("AmountLimit")),
        CountLimit = reader.GetInt32(reader.GetOrdinal("CountLimit")),
        Priority = reader.GetInt32(reader.GetOrdinal("Priority")),
        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static BankSwitch.Domain.RiskRule ReadRiskRule(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        RuleCode = GetString(reader, "RuleCode"),
        Name = GetString(reader, "Name"),
        RuleType = Enum.Parse<BankSwitch.Domain.RiskRuleType>(GetString(reader, "RuleType")),
        MatchValue = GetString(reader, "MatchValue"),
        Action = (BankSwitch.Domain.RiskRuleAction) Enum.Parse<RiskAction>(GetString(reader, "Action")),
        ResponseCode = GetString(reader, "ResponseCode"),
        AmountThreshold = reader.IsDBNull(reader.GetOrdinal("AmountThreshold")) ? null : reader.GetDecimal(reader.GetOrdinal("AmountThreshold")),
        Priority = reader.GetInt32(reader.GetOrdinal("Priority")),
        Enabled = reader.GetBoolean(reader.GetOrdinal("IsActive")),
        AlertTemplateCode = GetString(reader, "AlertTemplateCode"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static NotificationMessage ReadNotification(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        Channel = Enum.Parse<BankSwitch.Domain.NotificationChannel>(GetString(reader, "Channel")),
        Recipient = GetString(reader, "Recipient"),
        TemplateCode = GetString(reader, "TemplateCode"),
        Subject = GetString(reader, "Subject"),
        PayloadJson = GetString(reader, "PayloadJson"),
        Reference = GetString(reader, "Reference"),
        CorrelationId = GetString(reader, "CorrelationId"),
        Status = Enum.Parse<BankSwitch.Domain.NotificationStatus>(GetString(reader, "Status")),
        Attempts = reader.GetInt32(reader.GetOrdinal("Attempts")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        SentAt = GetNullableDateTimeOffset(reader, "SentAt")
    };

    private static StatementDocument ReadStatement(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        OwnerType = Enum.Parse<StatementOwnerType>(GetString(reader, "OwnerType")),
        OwnerId = reader.GetGuid(reader.GetOrdinal("OwnerId")),
        StatementNumber = GetString(reader, "StatementNumber"),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        PeriodStart = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("PeriodStart"))),
        PeriodEnd = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("PeriodEnd"))),
        OpeningBalance = reader.GetDecimal(reader.GetOrdinal("OpeningBalance")),
        ClosingBalance = reader.GetDecimal(reader.GetOrdinal("ClosingBalance")),
        DebitTotal = reader.GetDecimal(reader.GetOrdinal("DebitTotal")),
        CreditTotal = reader.GetDecimal(reader.GetOrdinal("CreditTotal")),
        TransactionCount = reader.GetInt32(reader.GetOrdinal("TransactionCount")),
        Status = Enum.Parse<StatementStatus>(GetString(reader, "Status")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static StatementLine ReadStatementLine(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        StatementId = reader.GetGuid(reader.GetOrdinal("StatementId")),
        TransactionDate = GetDateTimeOffset(reader, "TransactionDate"),
        Reference = GetString(reader, "Reference"),
        Narrative = GetString(reader, "Narrative"),
        Direction = Enum.Parse<LedgerEntryDirection>(GetString(reader, "Direction")),
        Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
        BalanceAfter = reader.GetDecimal(reader.GetOrdinal("BalanceAfter"))
    };

    private static string GetString(NpgsqlDataReader reader, string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? string.Empty : reader.GetString(reader.GetOrdinal(name));
    private static Guid? GetNullableGuid(NpgsqlDataReader reader, string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetGuid(reader.GetOrdinal(name));
    private static DateTimeOffset GetDateTimeOffset(NpgsqlDataReader reader, string name) => reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal(name));
    private static DateTimeOffset? GetNullableDateTimeOffset(NpgsqlDataReader reader, string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal(name));
}
