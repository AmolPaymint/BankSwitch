using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

// ============================================================
// GL Account Service
// ============================================================

public sealed class GlAccountService : IGlAccountService
{
    private readonly IGlAccountRepository _repository;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public GlAccountService(IGlAccountRepository repository, IAuditLogger audit, IClock clock)
    {
        _repository = repository;
        _audit = audit;
        _clock = clock;
    }

    public async Task<CmsOperationResult<GlAccount>> AddAccountAsync(
        AddGlAccountRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AccountCode))
            return CmsOperationResult<GlAccount>.Fail("30", "Account code is required.");
        var existing = await _repository.GetByCodeAsync(request.AccountCode, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return CmsOperationResult<GlAccount>.Fail("57", $"GL account {request.AccountCode} already exists.");

        var account = new GlAccount
        {
            AccountCode = request.AccountCode.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            AccountType = request.AccountType,
            CurrencyCode = request.CurrencyCode.Trim(),
            IsActive = request.IsActive
        };
        await _repository.AddAccountAsync(account, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "AddGlAccount",
            string.Empty, account.AccountCode, $"type={account.AccountType}", string.Empty);
        return CmsOperationResult<GlAccount>.Success(account);
    }

    public async Task<CmsOperationResult<GlAccount>> UpdateAccountAsync(
        Guid id, AddGlAccountRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var accounts = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var existing = accounts.FirstOrDefault(a => a.Id == id);
        if (existing is null) return CmsOperationResult<GlAccount>.Fail("25", "GL account not found.");

        var updated = existing with
        {
            Name = request.Name.Trim(),
            AccountType = request.AccountType,
            IsActive = request.IsActive
        };
        await _repository.UpdateAccountAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "UpdateGlAccount",
            existing.IsActive.ToString(), updated.IsActive.ToString(), $"account={existing.AccountCode}", string.Empty);
        return CmsOperationResult<GlAccount>.Success(updated);
    }

    public Task<GlAccount?> GetAccountAsync(string accountCode, CancellationToken cancellationToken = default)
        => _repository.GetByCodeAsync(accountCode, cancellationToken);

    public Task<IReadOnlyList<GlAccount>> GetChartOfAccountsAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<GlAccountBalance?> GetAccountBalanceAsync(string accountCode, DateOnly date, CancellationToken cancellationToken = default)
        => _repository.GetBalanceAsync(accountCode, date, cancellationToken);

    public Task<IReadOnlyList<GlAccountBalance>> GetAccountBalancesAsync(DateOnly date, CancellationToken cancellationToken = default)
        => _repository.GetBalancesAsync(date, cancellationToken);

    public async Task<TrialBalanceReport> GenerateTrialBalanceAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var accounts = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var balances = await _repository.GetBalancesAsync(date, cancellationToken).ConfigureAwait(false);
        var balanceByCode = balances.ToDictionary(b => b.AccountCode, StringComparer.OrdinalIgnoreCase);

        var lines = accounts
            .Where(a => a.IsActive)
            .OrderBy(a => a.AccountCode)
            .Select(a =>
            {
                balanceByCode.TryGetValue(a.AccountCode, out var bal);
                var debits = bal?.TotalDebits ?? 0m;
                var credits = bal?.TotalCredits ?? 0m;
                return new TrialBalanceLine(a.AccountCode, a.Name, a.AccountType, debits, credits, debits - credits);
            })
            .ToList();

        var totalDebits = lines.Sum(l => l.TotalDebits);
        var totalCredits = lines.Sum(l => l.TotalCredits);
        var isBalanced = Math.Abs(totalDebits - totalCredits) <= 0.005m;

        return new TrialBalanceReport(date, lines, totalDebits, totalCredits, isBalanced, DateTimeOffset.UtcNow);
    }

    public async Task UpdateBalanceAsync(string accountCode, string currencyCode, DateOnly businessDate,
        decimal debitAmount, decimal creditAmount, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetBalanceAsync(accountCode, businessDate, cancellationToken).ConfigureAwait(false);
        var updated = new GlAccountBalance
        {
            AccountCode = accountCode,
            CurrencyCode = currencyCode,
            BalanceDate = businessDate,
            OpeningBalance = existing?.OpeningBalance ?? 0m,
            TotalDebits = (existing?.TotalDebits ?? 0m) + debitAmount,
            TotalCredits = (existing?.TotalCredits ?? 0m) + creditAmount,
            ClosingBalance = (existing?.OpeningBalance ?? 0m) + ((existing?.TotalDebits ?? 0m) + debitAmount) - ((existing?.TotalCredits ?? 0m) + creditAmount),
            JournalLineCount = (existing?.JournalLineCount ?? 0) + (debitAmount > 0 || creditAmount > 0 ? 1 : 0),
            LastUpdatedAt = DateTimeOffset.UtcNow
        };
        await _repository.UpsertBalanceAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsAccountActiveAsync(string accountCode, CancellationToken cancellationToken = default)
    {
        var acct = await _repository.GetByCodeAsync(accountCode, cancellationToken).ConfigureAwait(false);
        return acct is not null && acct.IsActive;
    }
}

// ============================================================
// In-Memory GL Account Repository
// ============================================================

public sealed class InMemoryGlAccountRepository : IGlAccountRepository
{
    private readonly ConcurrentDictionary<string, GlAccount> _accounts =
        new(StringComparer.OrdinalIgnoreCase);
    // Key: accountCode:date
    private readonly ConcurrentDictionary<string, GlAccountBalance> _balances =
        new(StringComparer.OrdinalIgnoreCase);

    public InMemoryGlAccountRepository() { SeedChartOfAccounts(); }

    public Task AddAccountAsync(GlAccount account, CancellationToken cancellationToken = default)
    {
        _accounts[account.AccountCode] = account;
        return Task.CompletedTask;
    }
    public Task<GlAccount?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        _accounts.TryGetValue(code, out var a);
        return Task.FromResult(a);
    }
    public Task<IReadOnlyList<GlAccount>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GlAccount>>(_accounts.Values.OrderBy(a => a.AccountCode).ToList());

    public Task UpdateAccountAsync(GlAccount account, CancellationToken cancellationToken = default)
    {
        _accounts[account.AccountCode] = account;
        return Task.CompletedTask;
    }

    public Task UpsertBalanceAsync(GlAccountBalance balance, CancellationToken cancellationToken = default)
    {
        var key = $"{balance.AccountCode}:{balance.BalanceDate:yyyyMMdd}";
        _balances.AddOrUpdate(key, balance, (_, existing) => balance with
        {
            TotalDebits = existing.TotalDebits + balance.TotalDebits,
            TotalCredits = existing.TotalCredits + balance.TotalCredits,
            ClosingBalance = (existing.OpeningBalance) +
                             (existing.TotalDebits + balance.TotalDebits) -
                             (existing.TotalCredits + balance.TotalCredits),
            JournalLineCount = existing.JournalLineCount + balance.JournalLineCount
        });
        return Task.CompletedTask;
    }

    public Task<GlAccountBalance?> GetBalanceAsync(string code, DateOnly date, CancellationToken cancellationToken = default)
    {
        var key = $"{code}:{date:yyyyMMdd}";
        _balances.TryGetValue(key, out var b);
        return Task.FromResult(b);
    }

    public Task<IReadOnlyList<GlAccountBalance>> GetBalancesAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var dateStr = date.ToString("yyyyMMdd");
        var results = _balances.Where(kv => kv.Key.EndsWith($":{dateStr}", StringComparison.Ordinal))
            .Select(kv => kv.Value).OrderBy(b => b.AccountCode).ToList();
        return Task.FromResult<IReadOnlyList<GlAccountBalance>>(results);
    }

    /// <summary>
    /// Seeds the standard BankSwitch Chart of Accounts.
    /// Account codes follow: 1xxx=Assets, 2xxx=Liabilities, 3xxx=Equity, 4xxx=Income, 5xxx=Expenses.
    /// </summary>
    private void SeedChartOfAccounts()
    {
        void Add(string code, string name, GlAccountType type) =>
            _accounts[code] = new GlAccount { AccountCode = code, Name = name, AccountType = type, CurrencyCode = "566", IsActive = true };

        // Assets
        Add("1000-SETTLEMENT-CLEARING", "Settlement Clearing Account", GlAccountType.Clearing);
        Add("1100-NOSTRO-FUNDING", "Nostro Funding / Inter-bank Receivable", GlAccountType.Asset);
        Add("1200-PREPAID-CARD-FLOAT", "Prepaid Card Float — Collected Funds", GlAccountType.Asset);
        Add("1300-FEE-RECEIVABLE", "Fee Receivable — Uncollected Interchange", GlAccountType.Asset);
        Add("1400-CHARGEBACK-RECEIVABLE", "Chargeback Receivable", GlAccountType.Asset);

        // Liabilities
        Add("2000-DUE-TO-MEMBERS", "Due to Member Institutions", GlAccountType.Liability);
        Add("2100-CARDHOLDER-LIABILITY", "Cardholder Liability — Prepaid Balances", GlAccountType.Liability);
        Add("2200-SUSPENSE-CREDIT", "Suspense Credit — Unallocated Receipts", GlAccountType.Liability);
        Add("2300-CHARGEBACK-LIABILITY", "Chargeback Liability — Pending Recovery", GlAccountType.Liability);

        // Income
        Add("4000-INTERCHANGE-INCOME", "Interchange Fee Income", GlAccountType.Income);
        Add("4100-SCHEME-FEE-INCOME", "Scheme Processing Fee Income", GlAccountType.Income);
        Add("4200-CARD-FEE-INCOME", "Card Issuance / Maintenance Fee Income", GlAccountType.Income);
        Add("4300-FX-INCOME", "Foreign Exchange Spread Income", GlAccountType.Income);

        // Expenses
        Add("5000-SCHEME-FEE-EXPENSE", "Scheme Fee Expense (Visa/MC/NIBSS)", GlAccountType.Expense);
        Add("5100-HSM-PROCESSING-COST", "HSM/Cryptography Processing Cost", GlAccountType.Expense);
        Add("5200-REVERSAL-EXPENSE", "Reversal & Chargeback Write-off Expense", GlAccountType.Expense);
        Add("5300-ADJUSTMENT-EXPENSE", "Financial Adjustment Expense", GlAccountType.Expense);

        // GL alias used in existing code
        Add("4050-CARD-FEE-INCOME", "Card Fee Income (alternate)", GlAccountType.Income);
    }
}

// ============================================================
// In-Memory GL Period Repository
// ============================================================

public sealed class InMemoryGlPeriodRepository : IGlPeriodRepository
{
    private readonly ConcurrentDictionary<string, GlPeriod> _periods = new(StringComparer.Ordinal);

    public Task AddPeriodAsync(GlPeriod period, CancellationToken cancellationToken = default)
    {
        _periods[period.BusinessDate.ToString("yyyyMMdd")] = period;
        return Task.CompletedTask;
    }
    public Task<GlPeriod?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        _periods.TryGetValue(date.ToString("yyyyMMdd"), out var p);
        return Task.FromResult(p);
    }
    public Task<GlPeriod?> GetLatestOpenPeriodAsync(CancellationToken cancellationToken = default)
    {
        var open = _periods.Values
            .Where(p => p.Status == GlPeriodStatus.Open)
            .OrderByDescending(p => p.BusinessDate)
            .FirstOrDefault();
        return Task.FromResult(open);
    }
    public Task<IReadOnlyList<GlPeriod>> GetPeriodsAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        IEnumerable<GlPeriod> q = _periods.Values;
        if (from.HasValue) q = q.Where(p => p.BusinessDate >= from.Value);
        if (to.HasValue) q = q.Where(p => p.BusinessDate <= to.Value);
        return Task.FromResult<IReadOnlyList<GlPeriod>>(q.OrderByDescending(p => p.BusinessDate).ToList());
    }
    public Task UpdatePeriodAsync(GlPeriod period, CancellationToken cancellationToken = default)
    {
        _periods[period.BusinessDate.ToString("yyyyMMdd")] = period;
        return Task.CompletedTask;
    }
}
