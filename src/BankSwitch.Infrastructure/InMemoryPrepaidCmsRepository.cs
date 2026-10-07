using System.Collections.Concurrent;
using System.Security.Cryptography;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Configuration;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryPrepaidCmsRepository : ICmsRepository
{
    private readonly ConcurrentDictionary<Guid, PrepaidProgram> _programs = new();
    private readonly ConcurrentDictionary<string, Guid> _programByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, CardProduct> _products = new();
    private readonly ConcurrentDictionary<string, Guid> _productByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, LimitProfile> _limits = new();
    private readonly ConcurrentDictionary<Guid, CustomerProfile> _customers = new();
    private readonly ConcurrentDictionary<string, Guid> _customerByNumber = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, WalletAccount> _wallets = new();
    private readonly ConcurrentDictionary<Guid, PrepaidCard> _cards = new();
    private readonly ConcurrentDictionary<string, Guid> _cardByPanHash = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<LedgerEntry> _ledger = new();
    private readonly ConcurrentBag<CmsTransactionLog> _transactions = new();

    public InMemoryPrepaidCmsRepository(IConfiguration configuration, ISensitiveDataProtector protector)
    {
        SeedDevelopmentData(configuration, protector);
    }

    public Task AddProgramAsync(PrepaidProgram program, CancellationToken cancellationToken = default)
    {
        _programs[program.Id] = program;
        _programByCode[program.ProgramCode] = program.Id;
        return Task.CompletedTask;
    }

    public Task<PrepaidProgram?> GetProgramAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        _programs.TryGetValue(programId, out var program);
        return Task.FromResult(program);
    }

    public Task<PrepaidProgram?> GetProgramByCodeAsync(string programCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_programByCode.TryGetValue(programCode ?? string.Empty, out var id) && _programs.TryGetValue(id, out var program) ? program : null);
    }

    public Task AddProductAsync(CardProduct product, CancellationToken cancellationToken = default)
    {
        _products[product.Id] = product;
        _productByCode[product.ProductCode] = product.Id;
        return Task.CompletedTask;
    }

    public Task<CardProduct?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        _products.TryGetValue(productId, out var product);
        return Task.FromResult(product);
    }

    public Task<CardProduct?> GetProductByCodeAsync(string productCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_productByCode.TryGetValue(productCode ?? string.Empty, out var id) && _products.TryGetValue(id, out var product) ? product : null);
    }

    public Task AddLimitProfileAsync(LimitProfile profile, CancellationToken cancellationToken = default)
    {
        _limits[profile.Id] = profile;
        return Task.CompletedTask;
    }

    public Task<LimitProfile?> GetLimitProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        _limits.TryGetValue(profileId, out var profile);
        return Task.FromResult(profile);
    }

    public Task AddCustomerAsync(CustomerProfile customer, CancellationToken cancellationToken = default)
    {
        _customers[customer.Id] = customer;
        _customerByNumber[customer.CustomerNumber] = customer.Id;
        return Task.CompletedTask;
    }

    public Task<CustomerProfile?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        _customers.TryGetValue(customerId, out var customer);
        return Task.FromResult(customer);
    }

    public Task<CustomerProfile?> GetCustomerByNumberAsync(string customerNumber, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_customerByNumber.TryGetValue(customerNumber ?? string.Empty, out var id) && _customers.TryGetValue(id, out var customer) ? customer : null);
    }

    public Task UpdateCustomerAsync(CustomerProfile customer, CancellationToken cancellationToken = default)
    {
        _customers[customer.Id] = customer;
        _customerByNumber[customer.CustomerNumber] = customer.Id;
        return Task.CompletedTask;
    }

    public Task AddWalletAsync(WalletAccount wallet, CancellationToken cancellationToken = default)
    {
        _wallets[wallet.Id] = wallet;
        return Task.CompletedTask;
    }

    public Task<WalletAccount?> GetWalletAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        _wallets.TryGetValue(walletId, out var wallet);
        return Task.FromResult(wallet);
    }

    public Task UpdateWalletAsync(WalletAccount wallet, CancellationToken cancellationToken = default)
    {
        _wallets[wallet.Id] = wallet;
        return Task.CompletedTask;
    }

    public Task AddCardAsync(PrepaidCard card, CancellationToken cancellationToken = default)
    {
        _cards[card.Id] = card;
        _cardByPanHash[card.PanHash] = card.Id;
        return Task.CompletedTask;
    }

    public Task<PrepaidCard?> GetCardAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        _cards.TryGetValue(cardId, out var card);
        return Task.FromResult(card);
    }

    public Task<PrepaidCard?> GetCardByPanHashAsync(string panHash, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cardByPanHash.TryGetValue(panHash ?? string.Empty, out var id) && _cards.TryGetValue(id, out var card) ? card : null);
    }

    public Task<IReadOnlyList<PrepaidCard>> GetCardsForOwnerAsync(StatementOwnerType ownerType, Guid ownerId, CancellationToken cancellationToken = default)
    {
        IEnumerable<PrepaidCard> query = ownerType switch
        {
            StatementOwnerType.Customer => _cards.Values.Where(x => x.CustomerId == ownerId),
            StatementOwnerType.Card => _cards.Values.Where(x => x.Id == ownerId),
            StatementOwnerType.Agency => _cards.Values.Where(x => x.AgencyId == ownerId),
            StatementOwnerType.Corporate => _cards.Values.Where(x => x.CorporateId == ownerId),
            StatementOwnerType.Department => _cards.Values.Where(x => x.CorporateDepartmentId == ownerId),
            StatementOwnerType.Employee => _cards.Values.Where(x => x.CorporateEmployeeId == ownerId),
            _ => Array.Empty<PrepaidCard>()
        };
        return Task.FromResult<IReadOnlyList<PrepaidCard>>(query.ToList());
    }

    public Task UpdateCardAsync(PrepaidCard card, CancellationToken cancellationToken = default)
    {
        _cards[card.Id] = card;
        _cardByPanHash[card.PanHash] = card.Id;
        return Task.CompletedTask;
    }

    public Task AddLedgerEntryAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        _ledger.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LedgerEntry>> GetLedgerEntriesAsync(Guid walletAccountId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var entries = _ledger
            .Where(x => x.WalletAccountId == walletAccountId)
            .Where(x =>
            {
                var d = DateOnly.FromDateTime(x.CreatedAt.UtcDateTime);
                return d >= from && d <= to;
            })
            .OrderBy(x => x.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<LedgerEntry>>(entries);
    }

    public Task<decimal> GetUtilizedAmountAsync(Guid walletAccountId, IReadOnlyCollection<LedgerEntryType> entryTypes, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var total = _ledger
            .Where(x => x.WalletAccountId == walletAccountId && entryTypes.Contains(x.EntryType))
            .Where(x =>
            {
                var d = DateOnly.FromDateTime(x.CreatedAt.UtcDateTime);
                return d >= from && d <= to;
            })
            .Sum(x => x.Amount);
        return Task.FromResult(total);
    }

    public Task<int> GetTransactionCountAsync(Guid walletAccountId, IReadOnlyCollection<LedgerEntryType> entryTypes, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var count = _ledger
            .Where(x => x.WalletAccountId == walletAccountId && entryTypes.Contains(x.EntryType))
            .Count(x =>
            {
                var d = DateOnly.FromDateTime(x.CreatedAt.UtcDateTime);
                return d >= from && d <= to;
            });
        return Task.FromResult(count);
    }

    public Task AddCmsTransactionLogAsync(CmsTransactionLog log, CancellationToken cancellationToken = default)
    {
        _transactions.Add(log);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsCmsTransactionAsync(string rrn, string stan, string panHash, CancellationToken cancellationToken = default)
    {
        var exists = _transactions.Any(x =>
            !string.IsNullOrWhiteSpace(rrn) && !string.IsNullOrWhiteSpace(stan) && !string.IsNullOrWhiteSpace(panHash)
            && x.Rrn == rrn && x.Stan == stan && x.PanHash == panHash && x.ResponseCode == "00");
        return Task.FromResult(exists);
    }

    public Task<CmsTransactionLog?> GetCmsTransactionAsync(string rrn, string stan, string? panHash = null, bool approvedOnly = true, CancellationToken cancellationToken = default)
    {
        var tx = _transactions
            .Where(x => string.Equals(x.Rrn, rrn ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.Equals(x.Stan, stan ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(panHash) || string.Equals(x.PanHash, panHash, StringComparison.OrdinalIgnoreCase))
            .Where(x => !approvedOnly || x.ResponseCode == "00")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        return Task.FromResult<CmsTransactionLog?>(tx);
    }

    // B7 — AML re-screening and CTR generation extensions
    public Task<IReadOnlyList<CustomerProfile>> GetActiveCustomersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CustomerProfile>>(
            _customers.Values.Where(c => c.Status == CustomerLifecycleStatus.Active).ToList());

   /* public Task<IReadOnlyList<CmsTransactionLog>> GetTransactionsByCustomerAsync(string customerNumber, DateOnly date, CancellationToken cancellationToken = default)
    {
        // Map customer number to wallet/card, then find transactions for that day
        var card = _cards.Values.FirstOrDefault(c => c.CustomerNumber == customerNumber);
        if (card is null) return Task.FromResult<IReadOnlyList<CmsTransactionLog>>([]);
        var txns = _transactions
            .Where(t => t.PanHash == card.PanHash && DateOnly.FromDateTime(t.CreatedAt.Date) == date)
            .ToList();
        return Task.FromResult<IReadOnlyList<CmsTransactionLog>>(txns);
    }*/

    public Task<IReadOnlyList<CmsTransactionLog>> GetTransactionsByCustomerAsync(Guid customerid, DateOnly date, CancellationToken cancellationToken = default)
    {
        // Map customer number to wallet/card, then find transactions for that day
        var card = _cards.Values.FirstOrDefault(c => c.CustomerId == customerid);
        if (card is null) return Task.FromResult<IReadOnlyList<CmsTransactionLog>>([]);
        var txns = _transactions
            .Where(t => t.PanHash == card.PanHash && DateOnly.FromDateTime(t.CreatedAt.Date) == date)
            .ToList();
        return Task.FromResult<IReadOnlyList<CmsTransactionLog>>(txns);
    }

    private void SeedDevelopmentData(IConfiguration configuration, ISensitiveDataProtector protector)
    {
        var program = new PrepaidProgram
        {
            ProgramCode = "PREPAID-DEV",
            Name = "Development Prepaid Program",
            Description = "Seeded Phase 1 prepaid CMS program",
            CurrencyCode = "566",
            Reloadable = true,
            AllowedChannels = new HashSet<string> { "01" },
            AllowedTransactionTypes = new HashSet<string> { "00", "20", "LOAD" },
            Status = ProgramLifecycleStatus.Active,
            ActivatedAt = DateTimeOffset.UtcNow
        };
        AddProgramAsync(program).GetAwaiter().GetResult();

        var limit = new LimitProfile
        {
            ProgramId = program.Id,
            Name = "Development Tier 1 Limit",
            KycTier = KycTier.Tier1,
            MaxBalance = 1_000_000m,
            PerTransactionLimit = 250_000m,
            DailyLoadLimit = 500_000m,
            MonthlyLoadLimit = 2_000_000m,
            DailySpendLimit = 500_000m,
            MonthlySpendLimit = 2_000_000m,
            DailyTransactionCountLimit = 100,
            IsActive = true
        };
        AddLimitProfileAsync(limit).GetAwaiter().GetResult();

        var product = new CardProduct
        {
            ProgramId = program.Id,
            ProductCode = "VIRTUAL-DEV",
            Name = "Development Virtual Prepaid Card",
            CurrencyCode = "566",
            IssuingCountryCode = "NG",
            CardKind = PrepaidCardKind.Virtual,
            Reloadable = true,
            ExpiryPeriodMonths = 36,
            BinPrefix = "539983",
            LimitProfileId = limit.Id,
            AllowedChannels = new HashSet<string> { "01" },
            AllowedTransactionTypes = new HashSet<string> { "00", "20", "LOAD" },
            Status = CardProductStatus.Active
        };
        AddProductAsync(product).GetAwaiter().GetResult();

        var customer = new CustomerProfile
        {
            CustomerNumber = "CUST-DEV-001",
            FullName = "Development Cardholder",
            MobileNumber = "+2340000000000",
            Email = "dev-cardholder@example.local",
            KycTier = KycTier.Tier1,
            KycStatus = KycStatus.Verified,
            Status = CustomerLifecycleStatus.Active,
            RiskRating = "LOW"
        };
        AddCustomerAsync(customer).GetAwaiter().GetResult();

        var wallet = new WalletAccount
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            AccountNumber = "9000000001",
            CurrencyCode = "566",
            LedgerBalance = 100_000m,
            AvailableBalance = 100_000m,
            ReservedBalance = 0m,
            Status = WalletStatus.Active
        };
        AddWalletAsync(wallet).GetAwaiter().GetResult();

        var pan = configuration["CmsSeed:Pan"] ?? "5399838383838381";
        var hmacKey = configuration["Secrets:PanLookupHmacKey"] ?? "development-only-change-me";
        var expiry = DateTimeOffset.UtcNow.AddMonths(product.ExpiryPeriodMonths);
        var card = new PrepaidCard
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            WalletAccountId = wallet.Id,
            CardNumberToken = protector.Protect(pan, "PAN"),
            MaskedPan = CardholderDataProtector.MaskPan(pan),
            PanHash = CardholderDataProtector.HashForLookup(pan, hmacKey),
            ExpiryMonth = expiry.Month,
            ExpiryYear = expiry.Year,
            CardKind = product.CardKind,
            Status = PrepaidCardStatus.Active,
            ActivatedAt = DateTimeOffset.UtcNow
        };
        AddCardAsync(card).GetAwaiter().GetResult();
    }
}

public sealed class SecureCardNumberGenerator : ICardNumberGenerator
{
    public Task<string> GeneratePanAsync(string binPrefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(binPrefix) || binPrefix.Length < 6 || !binPrefix.All(char.IsDigit))
            throw new ArgumentException("BIN prefix must contain at least six numeric digits.", nameof(binPrefix));
        var bodyLength = 15 - binPrefix.Length;
        Span<byte> randomBytes = stackalloc byte[bodyLength];
        RandomNumberGenerator.Fill(randomBytes);
        var body = string.Concat(randomBytes.ToArray().Select(b => (b % 10).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var panWithoutCheck = binPrefix + body;
        var checkDigit = CalculateLuhnCheckDigit(panWithoutCheck);
        return Task.FromResult(panWithoutCheck + checkDigit.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public string GenerateAccountNumber()
    {
        Span<byte> bytes = stackalloc byte[10];
        RandomNumberGenerator.Fill(bytes);
        return "9" + string.Concat(bytes.ToArray().Take(9).Select(b => (b % 10).ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    public string GenerateAuthorizationCode()
    {
        Span<byte> bytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(bytes);
        return string.Concat(bytes.ToArray().Select(b => (b % 10).ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static int CalculateLuhnCheckDigit(string valueWithoutCheckDigit)
    {
        var sum = 0;
        var alternate = true;
        for (var i = valueWithoutCheckDigit.Length - 1; i >= 0; i--)
        {
            var n = valueWithoutCheckDigit[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return (10 - sum % 10) % 10;
    }
}
        