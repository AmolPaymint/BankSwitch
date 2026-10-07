using System.Data;
using BankSwitch.Application;
using BankSwitch.Domain;
//using Microsoft.Data.SqlClient;
using Npgsql;

namespace BankSwitch.Infrastructure;

public sealed class SqlPrepaidCmsRepository : ICmsRepository
{
    //private readonly SecureSqlConnectionFactory _connectionFactory;
    private readonly SecurePostgresConnectionFactory _connectionFactory;
    private readonly IClock _clock;

    public SqlPrepaidCmsRepository(SecurePostgresConnectionFactory connectionFactory, IClock clock)
    {
        _connectionFactory = connectionFactory;
        _clock = clock;
    }

    public async Task AddProgramAsync(PrepaidProgram program, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.prepaidprograms
        (id, programcode, name, description, currencycode, reloadable, allowedchannels, allowedtransactiontypes, status, createdat, activatedat)
        VALUES (@Id, @ProgramCode, @Name, @Description, @CurrencyCode, @Reloadable, @AllowedChannels, @AllowedTransactionTypes, @Status, @CreatedAt, @ActivatedAt)
        """, connection);

        BindProgram(command, program);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<PrepaidProgram?> GetProgramAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.prepaidprograms WHERE id = @Id LIMIT 1", connection);
        command.Parameters.AddWithValue("@Id", programId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadProgram(reader) : null;
    }

    public async Task<PrepaidProgram?> GetProgramByCodeAsync(string programCode, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.prepaidprograms WHERE programcode = @ProgramCode LIMIT 1", connection);
        command.Parameters.AddWithValue("@ProgramCode", programCode ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadProgram(reader) : null;
    }
    public async Task AddProductAsync(CardProduct product, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.cardproducts
        (id, programid, productcode, name, currencycode, cardkind, reloadable, expiryperiodmonths, binprefix, defaultfeeid, topupfeeid, purchasefeeid, limitprofileid, allowedchannels, allowedtransactiontypes, status, createdat)
        VALUES (@Id, @ProgramId, @ProductCode, @Name, @CurrencyCode, @CardKind, @Reloadable, @ExpiryPeriodMonths, @BinPrefix, @DefaultFeeId, @TopUpFeeId, @PurchaseFeeId, @LimitProfileId, @AllowedChannels, @AllowedTransactionTypes, @Status, @CreatedAt)
        """, connection);
        BindProduct(command, product);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<CardProduct?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.cardproducts WHERE id = @Id LIMIT 1", connection);
        command.Parameters.AddWithValue("@Id", productId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadProduct(reader) : null;
    }

    public async Task<CardProduct?> GetProductByCodeAsync(string productCode, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.cardproducts WHERE productcode = @ProductCode LIMIT 1", connection);
        command.Parameters.AddWithValue("@ProductCode", productCode ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadProduct(reader) : null;
    }
    public async Task AddLimitProfileAsync(LimitProfile profile, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.limitprofiles
        (id, programid, productid, name, kyctier, maxbalance, pertransactionlimit, dailyloadlimit, monthlyloadlimit, dailyspendlimit, monthlyspendlimit, dailytransactioncountlimit, isactive)
        VALUES (@Id, @ProgramId, @ProductId, @Name, @KycTier, @MaxBalance, @PerTransactionLimit, @DailyLoadLimit, @MonthlyLoadLimit, @DailySpendLimit, @MonthlySpendLimit, @DailyTransactionCountLimit, @IsActive)
        """, connection);
        BindLimit(command, profile);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<LimitProfile?> GetLimitProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT  * FROM dbo.limitprofiles WHERE id = @Id LIMIT 1", connection);
        command.Parameters.AddWithValue("@Id", profileId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadLimit(reader) : null;
    }

    public async Task AddCustomerAsync(CustomerProfile customer, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.customers
        (id, customernumber, fullname, mobilenumber, email, kyctier, kycstatus, status, riskrating, createdat)
        VALUES (@Id, @CustomerNumber, @FullName, @MobileNumber, @Email, @KycTier, @KycStatus, @Status, @RiskRating, @CreatedAt)
        """, connection);
        BindCustomer(command, customer);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<CustomerProfile?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.customers WHERE id = @Id LIMIT 1", connection);
        command.Parameters.AddWithValue("@Id", customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadCustomer(reader) : null;
    }

    public async Task<CustomerProfile?> GetCustomerByNumberAsync(string customerNumber, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.customers WHERE customernumber = @CustomerNumber LIMIT 1", connection);
        command.Parameters.AddWithValue("@CustomerNumber", customerNumber ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadCustomer(reader) : null;
    }

    public async Task UpdateCustomerAsync(CustomerProfile customer, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        UPDATE dbo.customers
        SET fullname = @FullName, mobilenumber = @MobileNumber, email = @Email,
            kyctier = @KycTier, kycstatus = @KycStatus, status = @Status,
            riskrating = @RiskRating, updatedat = @UpdatedAt
        WHERE id = @Id
        """, connection);
        command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = customer.Id;
        command.Parameters.AddWithValue("@FullName", customer.FullName);
        command.Parameters.AddWithValue("@MobileNumber", customer.MobileNumber);
        command.Parameters.AddWithValue("@Email", customer.Email);
        command.Parameters.AddWithValue("@KycTier", customer.KycTier.ToString());
        command.Parameters.AddWithValue("@KycStatus", customer.KycStatus.ToString());
        command.Parameters.AddWithValue("@Status", customer.Status.ToString());
        command.Parameters.AddWithValue("@RiskRating", customer.RiskRating);
        command.Parameters.Add("@UpdatedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)(customer.UpdatedAt ?? _clock.UtcNow) ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task AddWalletAsync(WalletAccount wallet, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.walletaccounts
        (id, accountnumber, customerid, productid, currencycode, ledgerbalance, availablebalance, reservedbalance, status, createdat)
        VALUES (@Id, @AccountNumber, @CustomerId, @ProductId, @CurrencyCode, @LedgerBalance, @AvailableBalance, @ReservedBalance, @Status, @CreatedAt)
        """, connection);
        BindWallet(command, wallet);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<WalletAccount?> GetWalletAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.walletaccounts WHERE id = @Id LIMIT 1", connection);
        command.Parameters.AddWithValue("@Id", walletId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadWallet(reader) : null;
    }

    public async Task UpdateWalletAsync(WalletAccount wallet, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // CD-02 FIX: Use RowVersion for optimistic concurrency.
        // If RowVersion is populated (always on SQL reads), enforce the WHERE clause on it.
        // If zero rows affected, another concurrent transaction already modified this wallet.
        var hasRowVersion = wallet.RowVersion is { Length: > 0 };
        var sql = hasRowVersion
            ? """
          UPDATE dbo.walletaccounts
          SET ledgerbalance = @LedgerBalance, availablebalance = @AvailableBalance, reservedbalance = @ReservedBalance, status = @Status
          WHERE id = @Id AND rowversion = @RowVersion
          """
            : """
          UPDATE dbo.walletaccounts
          SET ledgerbalance = @LedgerBalance, availablebalance = @AvailableBalance, reservedbalance = @ReservedBalance, status = @Status
          WHERE id = @Id
          """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", wallet.Id);
        command.Parameters.AddWithValue("@LedgerBalance", wallet.LedgerBalance);
        command.Parameters.AddWithValue("@AvailableBalance", wallet.AvailableBalance);
        command.Parameters.AddWithValue("@ReservedBalance", wallet.ReservedBalance);
        command.Parameters.AddWithValue("@Status", wallet.Status.ToString());
        if (hasRowVersion)
            //command.Parameters.Add("@RowVersion", NpgsqlTypes.NpgsqlDbType.Binary, 8).Value = wallet.RowVersion;
            command.Parameters.Add("@RowVersion", NpgsqlTypes.NpgsqlDbType.Bytea).Value = wallet.RowVersion;
        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (rowsAffected == 0)
            throw new WalletConcurrencyException(wallet.Id);
    }

    public async Task AddCardAsync(PrepaidCard card, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.prepaidcards
        (id, customerid, productid, walletaccountid, cardnumbertoken, maskedpan, panhash, expirymonth, expiryyear, cardkind, status, ownertype, agencyid, corporateid, corporatedepartmentid, corporateemployeeid, inventorybatchreference, createdat, activatedat, cvv2token, pintoken, blockreason, blockedat, replacedbycardid, updatedat)
        VALUES (@Id, @CustomerId, @ProductId, @WalletAccountId, @CardNumberToken, @MaskedPan, @PanHash, @ExpiryMonth, @ExpiryYear, @CardKind, @Status, @OwnerType, @AgencyId, @CorporateId, @CorporateDepartmentId, @CorporateEmployeeId, @InventoryBatchReference, @CreatedAt, @ActivatedAt, @Cvv2Token, @PinToken, @BlockReason, @BlockedAt, @ReplacedByCardId, @UpdatedAt)
        """, connection);
        BindCard(command, card);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<PrepaidCard?> GetCardAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.prepaidcards WHERE id = @Id LIMIT 1", connection);
        command.Parameters.AddWithValue("@Id", cardId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadCard(reader) : null;
    }

    public async Task<PrepaidCard?> GetCardByPanHashAsync(string panHash, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT * FROM dbo.prepaidcards WHERE panhash = @PanHash LIMIT 1", connection);
        command.Parameters.AddWithValue("@PanHash", panHash ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadCard(reader) : null;
    }

    public async Task<IReadOnlyList<PrepaidCard>> GetCardsForOwnerAsync(StatementOwnerType ownerType, Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var where = ownerType switch
        {
            StatementOwnerType.Customer => "customerid = @OwnerId",
            StatementOwnerType.Card => "id = @OwnerId",
            StatementOwnerType.Agency => "agencyid = @OwnerId",
            StatementOwnerType.Corporate => "corporateid = @OwnerId",
            StatementOwnerType.Department => "corporatedepartmentid = @OwnerId",
            StatementOwnerType.Employee => "corporateemployeeid = @OwnerId",
            _ => "1 = 0"
        };
        await using var command = new NpgsqlCommand($"SELECT * FROM dbo.prepaidcards WHERE {where}", connection);
        command.Parameters.AddWithValue("@OwnerId", ownerId);
        var cards = new List<PrepaidCard>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) cards.Add(ReadCard(reader));
        return cards;
    }

    public async Task UpdateCardAsync(PrepaidCard card, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        UPDATE dbo.prepaidcards
        SET status = @Status, activatedat = @ActivatedAt, ownertype = @OwnerType, agencyid = @AgencyId,
            corporateid = @CorporateId, corporatedepartmentid = @CorporateDepartmentId, corporateemployeeid = @CorporateEmployeeId,
            inventorybatchreference = @InventoryBatchReference, cvv2token = @Cvv2Token, pintoken = @PinToken,
            blockreason = @BlockReason, blockedat = @BlockedAt, replacedbycardid = @ReplacedByCardId, updatedat = @UpdatedAt
        WHERE id = @Id
        """, connection);
        command.Parameters.AddWithValue("@Id", card.Id);
        command.Parameters.AddWithValue("@Status", card.Status.ToString());
        command.Parameters.AddWithValue("@ActivatedAt", (object?)card.ActivatedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@OwnerType", card.OwnerType.ToString());
        command.Parameters.AddWithValue("@AgencyId", (object?)card.AgencyId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CorporateId", (object?)card.CorporateId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CorporateDepartmentId", (object?)card.CorporateDepartmentId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CorporateEmployeeId", (object?)card.CorporateEmployeeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@InventoryBatchReference", card.InventoryBatchReference);
        command.Parameters.AddWithValue("@Cvv2Token", string.IsNullOrEmpty(card.Cvv2Token) ? DBNull.Value : (object)card.Cvv2Token);
        command.Parameters.AddWithValue("@PinToken", string.IsNullOrEmpty(card.PinToken) ? DBNull.Value : (object)card.PinToken);
        command.Parameters.AddWithValue("@BlockReason", card.BlockReason ?? string.Empty);
        command.Parameters.Add("@BlockedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)card.BlockedAt ?? DBNull.Value;
        command.Parameters.Add("@ReplacedByCardId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = (object?)card.ReplacedByCardId ?? DBNull.Value;
        command.Parameters.Add("@UpdatedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)card.UpdatedAt ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddLedgerEntryAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.ledgerentries
        (id, walletaccountid, correlationid, entrytype, direction, amount, currencycode, balanceafter, reference, narrative, createdat)
        VALUES (@Id, @WalletAccountId, @CorrelationId, @EntryType, @Direction, @Amount, @CurrencyCode, @BalanceAfter, @Reference, @Narrative, @CreatedAt)
        """, connection);
        BindLedger(command, entry);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetLedgerEntriesAsync(Guid walletAccountId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        SELECT * FROM dbo.ledgerentries
        WHERE walletaccountid = @WalletAccountId AND CAST(createdat AS date) BETWEEN @From AND @To
        ORDER BY createdat, id
        """, connection);
        command.Parameters.AddWithValue("@WalletAccountId", walletAccountId);
        command.Parameters.AddWithValue("@From", from.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@To", to.ToDateTime(TimeOnly.MinValue));
        var entries = new List<LedgerEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) entries.Add(ReadLedger(reader));
        return entries;
    }
    public async Task<decimal> GetUtilizedAmountAsync(Guid walletAccountId, IReadOnlyCollection<LedgerEntryType> entryTypes, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var types = entryTypes.Select(x => x.ToString()).ToArray();
        if (types.Length == 0) return 0m;
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var names = string.Join(",", types.Select((_, i) => $"@Type{i}"));
        await using var command = new NpgsqlCommand($"""
        SELECT COALESCE(SUM(amount), 0) FROM dbo.ledgerentries
        WHERE walletaccountid = @WalletAccountId AND entrytype IN ({names}) AND createdat::date BETWEEN @From AND @To
        """, connection);
        command.Parameters.AddWithValue("@WalletAccountId", walletAccountId);
        command.Parameters.AddWithValue("@From", from.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@To", to.ToDateTime(TimeOnly.MinValue));
        for (var i = 0; i < types.Length; i++) command.Parameters.AddWithValue($"@Type{i}", types[i]);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToDecimal(result ?? 0m, System.Globalization.CultureInfo.InvariantCulture);
    }
    public async Task<int> GetTransactionCountAsync(Guid walletAccountId, IReadOnlyCollection<LedgerEntryType> entryTypes, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var types = entryTypes.Select(x => x.ToString()).ToArray();
        if (types.Length == 0) return 0;
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var names = string.Join(",", types.Select((_, i) => $"@Type{i}"));
        await using var command = new NpgsqlCommand($"""
        SELECT COUNT(1) FROM dbo.ledgerentries
        WHERE walletaccountid = @WalletAccountId AND entrytype IN ({names}) AND createdat::date BETWEEN @From AND @To
        """, connection);
        command.Parameters.AddWithValue("@WalletAccountId", walletAccountId);
        command.Parameters.AddWithValue("@From", from.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@To", to.ToDateTime(TimeOnly.MinValue));
        for (var i = 0; i < types.Length; i++) command.Parameters.AddWithValue($"@Type{i}", types[i]);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result ?? 0, System.Globalization.CultureInfo.InvariantCulture);
    }
    public async Task AddCmsTransactionLogAsync(CmsTransactionLog log, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        INSERT INTO dbo.cmstransactionlogs
        (id, correlationid, cardid, walletaccountid, maskedpan, panhash, transactiontypecode, channelcode, stan, rrn, amount, feeamount, currencycode, responsecode, responsedescription, authorizationcode, createdat)
        VALUES (@Id, @CorrelationId, @CardId, @WalletAccountId, @MaskedPan, @PanHash, @TransactionTypeCode, @ChannelCode, @Stan, @Rrn, @Amount, @FeeAmount, @CurrencyCode, @ResponseCode, @ResponseDescription, @AuthorizationCode, @CreatedAt)
        """, connection);
        BindCmsLog(command, log);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsCmsTransactionAsync(string rrn, string stan, string panHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rrn) || string.IsNullOrWhiteSpace(stan) || string.IsNullOrWhiteSpace(panHash)) return false;
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        SELECT COUNT(1) FROM dbo.cmstransactionlogs WHERE rrn = @Rrn AND stan = @Stan AND panhash = @PanHash AND responsecode = '00'
        """, connection);
        command.Parameters.AddWithValue("@Rrn", rrn);
        command.Parameters.AddWithValue("@Stan", stan);
        command.Parameters.AddWithValue("@PanHash", panHash);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0, System.Globalization.CultureInfo.InvariantCulture);
        return count > 0;
    }

    public async Task<CmsTransactionLog?> GetCmsTransactionAsync(string rrn, string stan, string? panHash = null, bool approvedOnly = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rrn) || string.IsNullOrWhiteSpace(stan)) return null;
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        SELECT id, correlationid, cardid, walletaccountid, maskedpan, panhash, transactiontypecode, channelcode, stan, rrn, amount, feeamount, currencycode, responsecode, responsedescription, authorizationcode, createdat
        FROM dbo.cmstransactionlogs
        WHERE rrn = @Rrn AND stan = @Stan
          AND (@PanHash = '' OR panhash = @PanHash)
          AND (@ApprovedOnly = 0 OR responsecode = '00')
        ORDER BY createdat DESC LIMIT 1
        """, connection);
        command.Parameters.AddWithValue("@Rrn", rrn);
        command.Parameters.AddWithValue("@Stan", stan);
        command.Parameters.AddWithValue("@PanHash", panHash ?? string.Empty);
        command.Parameters.AddWithValue("@ApprovedOnly", approvedOnly ? 1 : 0);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadCmsLog(reader) : null;
    }

    // B7 — AML re-screening and CTR generation extensions
    public async Task<IReadOnlyList<CustomerProfile>> GetActiveCustomersAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<CustomerProfile>();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            "SELECT id, customernumber,fullname,dateofbirth,nationalid,phonenumber,email,address,kycstatus,kyctier,status,createdat,activatedat FROM dbo.customerprofiles WHERE status='active'",
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(ReadCustomer(reader));
        return list;
    }
  /*  public async Task<IReadOnlyList<CmsTransactionLog>> GetTransactionsByCustomerAsync(string customerNumber, DateOnly date, CancellationToken cancellationToken = default)
    {
        var list = new List<CmsTransactionLog>();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        SELECT tl.id,tl.correlationid,tl.cardid,tl.walletaccountid,tl.maskedpan,tl.panhash,
               tl.transactiontypecode,tl.channelcode,tl.stan,tl.rrn,tl.amount,tl.feeamount,
               tl.currencycode,tl.responsecode,tl.responsedescription,tl.authorizationcode,tl.createdat
        FROM dbo.cmstransactionlogs tl
        JOIN dbo.prepaidcards pc ON pc.panhash = tl.panhash
        WHERE pc.customernumber = @customernumber
          AND tl.createdat::date = @date
        ORDER BY tl.createdat DESC
        """, connection);
        command.Parameters.AddWithValue("@CustomerNumber", customerNumber);
        command.Parameters.AddWithValue("@Date", date.ToDateTime(TimeOnly.MinValue));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(ReadCmsLog(reader));
        return list;
    }*/
     public async Task<IReadOnlyList<CmsTransactionLog>> GetTransactionsByCustomerAsync(Guid customerid, DateOnly date, CancellationToken cancellationToken = default)
    {
        var list = new List<CmsTransactionLog>();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
        SELECT tl.id,tl.correlationid,tl.cardid,tl.walletaccountid,tl.maskedpan,tl.panhash,
               tl.transactiontypecode,tl.channelcode,tl.stan,tl.rrn,tl.amount,tl.feeamount,
               tl.currencycode,tl.responsecode,tl.responsedescription,tl.authorizationcode,tl.createdat
        FROM dbo.cmstransactionlogs tl
        JOIN dbo.prepaidcards pc ON pc.panhash = tl.panhash
        WHERE pc.customerid = @customerid
          AND tl.createdat::date = @date
        ORDER BY tl.createdat DESC
        """, connection);
        command.Parameters.AddWithValue("@customerid", customerid);
        command.Parameters.AddWithValue("@Date", date.ToDateTime(TimeOnly.MinValue));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(ReadCmsLog(reader));
        return list;
    }
    private static void BindProgram(NpgsqlCommand command, PrepaidProgram program)
    {
        command.Parameters.AddWithValue("@Id", program.Id);
        command.Parameters.AddWithValue("@ProgramCode", program.ProgramCode);
        command.Parameters.AddWithValue("@Name", program.Name);
        command.Parameters.AddWithValue("@Description", program.Description);
        command.Parameters.AddWithValue("@CurrencyCode", program.CurrencyCode);
        command.Parameters.AddWithValue("@Reloadable", program.Reloadable);
        command.Parameters.AddWithValue("@AllowedChannels", Join(program.AllowedChannels));
        command.Parameters.AddWithValue("@AllowedTransactionTypes", Join(program.AllowedTransactionTypes));
        command.Parameters.AddWithValue("@Status", program.Status.ToString());
        command.Parameters.AddWithValue("@CreatedAt", program.CreatedAt);
        command.Parameters.AddWithValue("@ActivatedAt", (object?)program.ActivatedAt ?? DBNull.Value);
    }

    private static void BindProduct(NpgsqlCommand command, CardProduct product)
    {
        command.Parameters.AddWithValue("@Id", product.Id);
        command.Parameters.AddWithValue("@ProgramId", product.ProgramId);
        command.Parameters.AddWithValue("@ProductCode", product.ProductCode);
        command.Parameters.AddWithValue("@Name", product.Name);
        command.Parameters.AddWithValue("@CurrencyCode", product.CurrencyCode);
        command.Parameters.AddWithValue("@CardKind", product.CardKind.ToString());
        command.Parameters.AddWithValue("@Reloadable", product.Reloadable);
        command.Parameters.AddWithValue("@ExpiryPeriodMonths", product.ExpiryPeriodMonths);
        command.Parameters.AddWithValue("@BinPrefix", product.BinPrefix);
        command.Parameters.AddWithValue("@DefaultFeeId", (object?)product.DefaultFeeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@TopUpFeeId", (object?)product.TopUpFeeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@PurchaseFeeId", (object?)product.PurchaseFeeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@LimitProfileId", product.LimitProfileId);
        command.Parameters.AddWithValue("@AllowedChannels", Join(product.AllowedChannels));
        command.Parameters.AddWithValue("@AllowedTransactionTypes", Join(product.AllowedTransactionTypes));
        command.Parameters.AddWithValue("@Status", product.Status.ToString());
        command.Parameters.AddWithValue("@CreatedAt", product.CreatedAt);
    }

    private static void BindLimit(NpgsqlCommand command, LimitProfile profile)
    {
        command.Parameters.AddWithValue("@Id", profile.Id);
        command.Parameters.AddWithValue("@ProgramId", profile.ProgramId);
        command.Parameters.AddWithValue("@ProductId", (object?)profile.ProductId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Name", profile.Name);
        command.Parameters.AddWithValue("@KycTier", profile.KycTier.ToString());
        command.Parameters.AddWithValue("@MaxBalance", profile.MaxBalance);
        command.Parameters.AddWithValue("@PerTransactionLimit", profile.PerTransactionLimit);
        command.Parameters.AddWithValue("@DailyLoadLimit", profile.DailyLoadLimit);
        command.Parameters.AddWithValue("@MonthlyLoadLimit", profile.MonthlyLoadLimit);
        command.Parameters.AddWithValue("@DailySpendLimit", profile.DailySpendLimit);
        command.Parameters.AddWithValue("@MonthlySpendLimit", profile.MonthlySpendLimit);
        command.Parameters.AddWithValue("@DailyTransactionCountLimit", profile.DailyTransactionCountLimit);
        command.Parameters.AddWithValue("@IsActive", profile.IsActive);
    }

    private static void BindCustomer(NpgsqlCommand command, CustomerProfile customer)
    {
        command.Parameters.AddWithValue("@Id", customer.Id);
        command.Parameters.AddWithValue("@CustomerNumber", customer.CustomerNumber);
        command.Parameters.AddWithValue("@FullName", customer.FullName);
        command.Parameters.AddWithValue("@MobileNumber", customer.MobileNumber);
        command.Parameters.AddWithValue("@Email", customer.Email);
        command.Parameters.AddWithValue("@KycTier", customer.KycTier.ToString());
        command.Parameters.AddWithValue("@KycStatus", customer.KycStatus.ToString());
        command.Parameters.AddWithValue("@Status", customer.Status.ToString());
        command.Parameters.AddWithValue("@RiskRating", customer.RiskRating);
        command.Parameters.AddWithValue("@CreatedAt", customer.CreatedAt);
    }

    private static void BindWallet(NpgsqlCommand command, WalletAccount wallet)
    {
        command.Parameters.AddWithValue("@Id", wallet.Id);
        command.Parameters.AddWithValue("@AccountNumber", wallet.AccountNumber);
        command.Parameters.AddWithValue("@CustomerId", wallet.CustomerId);
        command.Parameters.AddWithValue("@ProductId", wallet.ProductId);
        command.Parameters.AddWithValue("@CurrencyCode", wallet.CurrencyCode);
        command.Parameters.AddWithValue("@LedgerBalance", wallet.LedgerBalance);
        command.Parameters.AddWithValue("@AvailableBalance", wallet.AvailableBalance);
        command.Parameters.AddWithValue("@ReservedBalance", wallet.ReservedBalance);
        command.Parameters.AddWithValue("@Status", wallet.Status.ToString());
        command.Parameters.AddWithValue("@CreatedAt", wallet.CreatedAt);
    }

    private static void BindCard(NpgsqlCommand command, PrepaidCard card)
    {
        command.Parameters.AddWithValue("@Id", card.Id);
        command.Parameters.AddWithValue("@CustomerId", card.CustomerId);
        command.Parameters.AddWithValue("@ProductId", card.ProductId);
        command.Parameters.AddWithValue("@WalletAccountId", card.WalletAccountId);
        command.Parameters.AddWithValue("@CardNumberToken", card.CardNumberToken);
        command.Parameters.AddWithValue("@MaskedPan", card.MaskedPan);
        command.Parameters.AddWithValue("@PanHash", card.PanHash);
        command.Parameters.AddWithValue("@ExpiryMonth", card.ExpiryMonth);
        command.Parameters.AddWithValue("@ExpiryYear", card.ExpiryYear);
        command.Parameters.AddWithValue("@CardKind", card.CardKind.ToString());
        command.Parameters.AddWithValue("@Status", card.Status.ToString());
        command.Parameters.AddWithValue("@OwnerType", card.OwnerType.ToString());
        command.Parameters.AddWithValue("@AgencyId", (object?)card.AgencyId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CorporateId", (object?)card.CorporateId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CorporateDepartmentId", (object?)card.CorporateDepartmentId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CorporateEmployeeId", (object?)card.CorporateEmployeeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@InventoryBatchReference", card.InventoryBatchReference);
        command.Parameters.AddWithValue("@CreatedAt", card.CreatedAt);
        command.Parameters.AddWithValue("@ActivatedAt", (object?)card.ActivatedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@Cvv2Token", string.IsNullOrEmpty(card.Cvv2Token) ? DBNull.Value : (object)card.Cvv2Token);
        command.Parameters.AddWithValue("@PinToken", string.IsNullOrEmpty(card.PinToken) ? DBNull.Value : (object)card.PinToken);
        command.Parameters.AddWithValue("@BlockReason", card.BlockReason);
        command.Parameters.Add("@BlockedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)card.BlockedAt ?? DBNull.Value;
        command.Parameters.Add("@ReplacedByCardId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = (object?)card.ReplacedByCardId ?? DBNull.Value;
        command.Parameters.Add("@UpdatedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)card.UpdatedAt ?? DBNull.Value;
    }

    private static void BindLedger(NpgsqlCommand command, LedgerEntry entry)
    {
        command.Parameters.AddWithValue("@Id", entry.Id);
        command.Parameters.AddWithValue("@WalletAccountId", entry.WalletAccountId);
        command.Parameters.AddWithValue("@CorrelationId", entry.CorrelationId);
        command.Parameters.AddWithValue("@EntryType", entry.EntryType.ToString());
        command.Parameters.AddWithValue("@Direction", entry.Direction.ToString());
        command.Parameters.AddWithValue("@Amount", entry.Amount);
        command.Parameters.AddWithValue("@CurrencyCode", entry.CurrencyCode);
        command.Parameters.AddWithValue("@BalanceAfter", entry.BalanceAfter);
        command.Parameters.AddWithValue("@Reference", entry.Reference);
        command.Parameters.AddWithValue("@Narrative", entry.Narrative);
        command.Parameters.AddWithValue("@CreatedAt", entry.CreatedAt);
    }

    private static void BindCmsLog(NpgsqlCommand command, CmsTransactionLog log)
    {
        command.Parameters.AddWithValue("@Id", log.Id);
        command.Parameters.AddWithValue("@CorrelationId", log.CorrelationId);
        command.Parameters.AddWithValue("@CardId", (object?)log.CardId ?? DBNull.Value);
        command.Parameters.AddWithValue("@WalletAccountId", (object?)log.WalletAccountId ?? DBNull.Value);
        command.Parameters.AddWithValue("@MaskedPan", log.MaskedPan);
        command.Parameters.AddWithValue("@PanHash", log.PanHash);
        command.Parameters.AddWithValue("@TransactionTypeCode", log.TransactionTypeCode);
        command.Parameters.AddWithValue("@ChannelCode", log.ChannelCode);
        command.Parameters.AddWithValue("@Stan", log.Stan);
        command.Parameters.AddWithValue("@Rrn", log.Rrn);
        command.Parameters.AddWithValue("@Amount", log.Amount);
        command.Parameters.AddWithValue("@FeeAmount", log.FeeAmount);
        command.Parameters.AddWithValue("@CurrencyCode", log.CurrencyCode);
        command.Parameters.AddWithValue("@ResponseCode", log.ResponseCode);
        command.Parameters.AddWithValue("@ResponseDescription", log.ResponseDescription);
        command.Parameters.AddWithValue("@AuthorizationCode", log.AuthorizationCode);
        command.Parameters.AddWithValue("@CreatedAt", log.CreatedAt);
    }

    private static PrepaidProgram ReadProgram(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        ProgramCode = GetString(reader, "ProgramCode"),
        Name = GetString(reader, "Name"),
        Description = GetString(reader, "Description"),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        Reloadable = reader.GetBoolean(reader.GetOrdinal("Reloadable")),
        AllowedChannels = Split(GetString(reader, "AllowedChannels")),
        AllowedTransactionTypes = Split(GetString(reader, "AllowedTransactionTypes")),
        Status = Enum.Parse<ProgramLifecycleStatus>(GetString(reader, "Status")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        ActivatedAt = GetNullableDateTimeOffset(reader, "ActivatedAt")
    };

    private static CardProduct ReadProduct(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        ProgramId = reader.GetGuid(reader.GetOrdinal("ProgramId")),
        ProductCode = GetString(reader, "ProductCode"),
        Name = GetString(reader, "Name"),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        CardKind = Enum.Parse<PrepaidCardKind>(GetString(reader, "CardKind")),
        Reloadable = reader.GetBoolean(reader.GetOrdinal("Reloadable")),
        ExpiryPeriodMonths = reader.GetInt32(reader.GetOrdinal("ExpiryPeriodMonths")),
        BinPrefix = GetString(reader, "BinPrefix"),
        DefaultFeeId = GetNullableGuid(reader, "DefaultFeeId"),
        TopUpFeeId = GetNullableGuid(reader, "TopUpFeeId"),
        PurchaseFeeId = GetNullableGuid(reader, "PurchaseFeeId"),
        LimitProfileId = reader.GetGuid(reader.GetOrdinal("LimitProfileId")),
        AllowedChannels = Split(GetString(reader, "AllowedChannels")),
        AllowedTransactionTypes = Split(GetString(reader, "AllowedTransactionTypes")),
        Status = Enum.Parse<CardProductStatus>(GetString(reader, "Status")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static LimitProfile ReadLimit(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        ProgramId = reader.GetGuid(reader.GetOrdinal("ProgramId")),
        ProductId = GetNullableGuid(reader, "ProductId"),
        Name = GetString(reader, "Name"),
        KycTier = Enum.Parse<KycTier>(GetString(reader, "KycTier")),
        MaxBalance = reader.GetDecimal(reader.GetOrdinal("MaxBalance")),
        PerTransactionLimit = reader.GetDecimal(reader.GetOrdinal("PerTransactionLimit")),
        DailyLoadLimit = reader.GetDecimal(reader.GetOrdinal("DailyLoadLimit")),
        MonthlyLoadLimit = reader.GetDecimal(reader.GetOrdinal("MonthlyLoadLimit")),
        DailySpendLimit = reader.GetDecimal(reader.GetOrdinal("DailySpendLimit")),
        MonthlySpendLimit = reader.GetDecimal(reader.GetOrdinal("MonthlySpendLimit")),
        DailyTransactionCountLimit = reader.GetInt32(reader.GetOrdinal("DailyTransactionCountLimit")),
        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
    };

    private static CustomerProfile ReadCustomer(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        CustomerNumber = GetString(reader, "CustomerNumber"),
        FullName = GetString(reader, "FullName"),
        MobileNumber = GetString(reader, "MobileNumber"),
        Email = GetString(reader, "Email"),
        KycTier = Enum.Parse<KycTier>(GetString(reader, "KycTier")),
        KycStatus = Enum.Parse<KycStatus>(GetString(reader, "KycStatus")),
        Status = Enum.Parse<CustomerLifecycleStatus>(GetString(reader, "Status")),
        RiskRating = GetString(reader, "RiskRating"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static WalletAccount ReadWallet(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        AccountNumber = GetString(reader, "AccountNumber"),
        CustomerId = reader.GetGuid(reader.GetOrdinal("CustomerId")),
        ProductId = reader.GetGuid(reader.GetOrdinal("ProductId")),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        LedgerBalance = reader.GetDecimal(reader.GetOrdinal("LedgerBalance")),
        AvailableBalance = reader.GetDecimal(reader.GetOrdinal("AvailableBalance")),
        ReservedBalance = reader.GetDecimal(reader.GetOrdinal("ReservedBalance")),
        Status = Enum.Parse<WalletStatus>(GetString(reader, "Status")),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        RowVersion = reader.IsDBNull(reader.GetOrdinal("RowVersion"))
            ? Array.Empty<byte>()
            : (byte[])reader["RowVersion"]
    };

    private static PrepaidCard ReadCard(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        CustomerId = reader.GetGuid(reader.GetOrdinal("CustomerId")),
        ProductId = reader.GetGuid(reader.GetOrdinal("ProductId")),
        WalletAccountId = reader.GetGuid(reader.GetOrdinal("WalletAccountId")),
        CardNumberToken = GetString(reader, "CardNumberToken"),
        MaskedPan = GetString(reader, "MaskedPan"),
        PanHash = GetString(reader, "PanHash"),
        ExpiryMonth = reader.GetInt32(reader.GetOrdinal("ExpiryMonth")),
        ExpiryYear = reader.GetInt32(reader.GetOrdinal("ExpiryYear")),
        CardKind = Enum.Parse<PrepaidCardKind>(GetString(reader, "CardKind")),
        Status = Enum.Parse<PrepaidCardStatus>(GetString(reader, "Status")),
        OwnerType = Enum.TryParse<CardOwnerType>(GetString(reader, "OwnerType"), out var ownerType) ? ownerType : CardOwnerType.Customer,
        AgencyId = GetNullableGuid(reader, "AgencyId"),
        CorporateId = GetNullableGuid(reader, "CorporateId"),
        CorporateDepartmentId = GetNullableGuid(reader, "CorporateDepartmentId"),
        CorporateEmployeeId = GetNullableGuid(reader, "CorporateEmployeeId"),
        InventoryBatchReference = GetString(reader, "InventoryBatchReference"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        ActivatedAt = GetNullableDateTimeOffset(reader, "ActivatedAt"),
        Cvv2Token = GetString(reader, "Cvv2Token"),
        PinToken = GetString(reader, "PinToken"),
        BlockReason = GetString(reader, "BlockReason"),
        BlockedAt = GetNullableDateTimeOffset(reader, "BlockedAt"),
        ReplacedByCardId = GetNullableGuid(reader, "ReplacedByCardId"),
        UpdatedAt = GetNullableDateTimeOffset(reader, "UpdatedAt")
    };

    private static LedgerEntry ReadLedger(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        WalletAccountId = reader.GetGuid(reader.GetOrdinal("WalletAccountId")),
        CorrelationId = GetString(reader, "CorrelationId"),
        EntryType = Enum.Parse<LedgerEntryType>(GetString(reader, "EntryType")),
        Direction = Enum.Parse<LedgerEntryDirection>(GetString(reader, "Direction")),
        Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        BalanceAfter = reader.GetDecimal(reader.GetOrdinal("BalanceAfter")),
        Reference = GetString(reader, "Reference"),
        Narrative = GetString(reader, "Narrative"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static CmsTransactionLog ReadCmsLog(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        CorrelationId = GetString(reader, "CorrelationId"),
        CardId = GetNullableGuid(reader, "CardId"),
        WalletAccountId = GetNullableGuid(reader, "WalletAccountId"),
        MaskedPan = GetString(reader, "MaskedPan"),
        PanHash = GetString(reader, "PanHash"),
        TransactionTypeCode = GetString(reader, "TransactionTypeCode"),
        ChannelCode = GetString(reader, "ChannelCode"),
        Stan = GetString(reader, "Stan"),
        Rrn = GetString(reader, "Rrn"),
        Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
        FeeAmount = reader.GetDecimal(reader.GetOrdinal("FeeAmount")),
        CurrencyCode = GetString(reader, "CurrencyCode"),
        ResponseCode = GetString(reader, "ResponseCode"),
        ResponseDescription = GetString(reader, "ResponseDescription"),
        AuthorizationCode = GetString(reader, "AuthorizationCode"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static string Join(IEnumerable<string> values) => string.Join(',', values ?? Array.Empty<string>());
    private static IReadOnlySet<string> Split(string values) => values.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static string GetString(NpgsqlDataReader reader, string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? string.Empty : reader.GetString(reader.GetOrdinal(name));
    private static Guid? GetNullableGuid(NpgsqlDataReader reader, string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetGuid(reader.GetOrdinal(name));
    private static DateTimeOffset GetDateTimeOffset(NpgsqlDataReader reader, string name) => reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal(name));
    private static DateTimeOffset? GetNullableDateTimeOffset(NpgsqlDataReader reader, string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal(name));
}
