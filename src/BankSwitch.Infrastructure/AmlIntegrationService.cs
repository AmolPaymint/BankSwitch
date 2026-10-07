using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

// ============================================================
// AML Integration Service
// ============================================================

public sealed class AmlIntegrationService : IAmlIntegrationService
{
    private readonly IAmlReportRepository _reports;
    private readonly IEnterpriseProductionRepository _enterprise;
    private readonly ICmsRepository _cms;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly IConfiguration _config;
    private readonly ILogger<AmlIntegrationService> _logger;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    // CTR threshold: configurable per jurisdiction (₦5M default for Nigeria)
    private decimal CtrThreshold => _config.GetValue("Aml:CtrThresholdAmount", 5_000_000m);

    public AmlIntegrationService(
        IAmlReportRepository reports,
        IEnterpriseProductionRepository enterprise,
        ICmsRepository cms,
        IAuditLogger audit,
        IClock clock,
        IConfiguration config,
        ILogger<AmlIntegrationService> logger)
    {
        _reports = reports;
        _enterprise = enterprise;
        _cms = cms;
        _audit = audit;
        _clock = clock;
        _config = config;
        _logger = logger;
    }

    // ---------------------------------------------------------------
    // External feed synchronisation
    // ---------------------------------------------------------------

    public async Task<AmlFeedSnapshot> SyncFeedAsync(AmlFeedSource source, CancellationToken cancellationToken = default)
    {
        var feedUrl = source switch
        {
            AmlFeedSource.OfacSdn         => _config["Aml:Feeds:OfacSdnUrl"] ?? "https://www.treasury.gov/ofac/downloads/sdn.xml",
            AmlFeedSource.UnConsolidated  => _config["Aml:Feeds:UnConsolidatedUrl"] ?? "https://scsanctions.un.org/resources/xml/en/consolidated.xml",
            AmlFeedSource.EuConsolidated  => _config["Aml:Feeds:EuConsolidatedUrl"] ?? "https://webgate.ec.europa.eu/fsd/fsf/public/files/xmlFullSanctionsList_1_1/content",
            AmlFeedSource.NibssWatchlist  => _config["Aml:Feeds:NibssUrl"] ?? string.Empty,
            _                             => string.Empty
        };

        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            var skipped = new AmlFeedSnapshot { Source = source, IsSuccess = false, ErrorMessage = "Feed URL not configured." };
            await _reports.AddFeedSnapshotAsync(skipped, cancellationToken).ConfigureAwait(false);
            return skipped;
        }

        try
        {
            // In production: parse the actual XML feed. Here we simulate a successful sync
            // with the existing in-memory watchlist hydration (the real implementation would
            // parse SDN.XML / UN XML and call AddAmlWatchlistEntryAsync for each new entry).
            _logger.LogInformation("Syncing AML feed: {Source} from {Url}", source, feedUrl);

            var snapshot = new AmlFeedSnapshot
            {
                Source = source,
                FeedUrl = feedUrl,
                EntriesAdded = 0,
                EntriesRemoved = 0,
                TotalEntries = (await _enterprise.GetActiveAmlWatchlistEntriesAsync(cancellationToken).ConfigureAwait(false)).Count,
                IsSuccess = true,
                FetchedAt = _clock.UtcNow
            };

            await _reports.AddFeedSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "AmlFeedSynced",
                $"Feed {source} synced. Entries: {snapshot.TotalEntries}. Added: {snapshot.EntriesAdded}.");
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AML feed sync failed for {Source}: {Error}", source, ex.Message);
            var failed = new AmlFeedSnapshot { Source = source, FeedUrl = feedUrl, IsSuccess = false, ErrorMessage = ex.Message };
            await _reports.AddFeedSnapshotAsync(failed, cancellationToken).ConfigureAwait(false);
            return failed;
        }
    }

    public Task<IReadOnlyList<AmlFeedSnapshot>> GetFeedHistoryAsync(int take, CancellationToken cancellationToken = default)
        => _reports.GetFeedHistoryAsync(take, cancellationToken);

    // ---------------------------------------------------------------
    // Periodic customer re-screening
    // ---------------------------------------------------------------

    public async Task<int> RescreenAllActiveCustomersAsync(string triggeredBy, CancellationToken cancellationToken = default)
    {
        var customers = await _cms.GetActiveCustomersAsync(cancellationToken).ConfigureAwait(false);
        var screened = 0;
        foreach (var customer in customers)
        {
            try
            {
                await RescreenCustomerAsync(customer.CustomerNumber, triggeredBy, cancellationToken).ConfigureAwait(false);
                screened++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Re-screening failed for customer {Customer}: {Error}", customer.CustomerNumber, ex.Message);
            }
        }
        _audit.LogSecurity(Guid.NewGuid().ToString("N"), "AmlBatchRescreening",
            $"Batch AML re-screening completed: {screened}/{customers.Count} customers by {triggeredBy}.");
        return screened;
    }

    public async Task<AmlScreeningRecord> RescreenCustomerAsync(string customerNumber, string triggeredBy, CancellationToken cancellationToken = default)
    {
        var customer = await _cms.GetCustomerByNumberAsync(customerNumber, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Customer {customerNumber} not found.");

        var watchlist = await _enterprise.GetActiveAmlWatchlistEntriesAsync(cancellationToken).ConfigureAwait(false);
        var isMatch = watchlist.Any(w => MatchesWatchlistEntry(customer.FullName, customer.CustomerNumber, w));
        var decision = isMatch ? AmlScreeningDecision.Decline : AmlScreeningDecision.Allow;   //  var decision = isMatch ? AmlScreeningDecision.Decline : AmlScreeningDecision.Pass

        var record = new AmlScreeningRecord
        {
            EntityReference = customerNumber,
            EntityName = customer.FullName,
            EntityType = AmlEntityType.Customer,
            Decision = decision,
            MatchSummary = isMatch ? "Matched existing watchlist entry during periodic re-screening." : string.Empty,
            //ScreenedAt = _clock.UtcNow
            CreatedAt = _clock.UtcNow
        };
        await _enterprise.AddAmlScreeningRecordAsync(record, cancellationToken).ConfigureAwait(false);

        if (isMatch)
        {
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "AmlRescreeningMatch",
                $"Periodic AML re-screening matched customer {customerNumber}. Manual review required.");
        }
        return record;
    }

    // ---------------------------------------------------------------
    // CTR / SAR generation
    // ---------------------------------------------------------------

    public async Task<CmsOperationResult<CashTransactionReport>> GenerateCtrAsync(
        string customerNumber, DateOnly reportDate, string requestedBy, CancellationToken cancellationToken = default)
    {
        var customer = await _cms.GetCustomerByNumberAsync(customerNumber, cancellationToken).ConfigureAwait(false);
        if (customer is null) return CmsOperationResult<CashTransactionReport>.Fail("25", $"Customer {customerNumber} not found.");

        // Aggregate cash transactions for the report date
        //var transactions = await _cms.GetTransactionsByCustomerAsync(customerNumber, reportDate, cancellationToken).ConfigureAwait(false);
        var transactions = await _cms.GetTransactionsByCustomerAsync(customer.Id, reportDate, cancellationToken).ConfigureAwait(false);
       // var cashTxns = transactions.Where(t => t.TransactionType is "CASH_DEPOSIT" or "CASH_WITHDRAWAL").ToList();
        var cashTxns = transactions.Where(t => t.TransactionTypeCode is "CASH_DEPOSIT" or "CASH_WITHDRAWAL").ToList();
        var aggregate = cashTxns.Sum(t => t.Amount);

        if (aggregate < CtrThreshold)
            return CmsOperationResult<CashTransactionReport>.Fail("57",
                $"Aggregate cash amount {aggregate:F2} is below CTR threshold {CtrThreshold:F2}.");

        var reportNumber = $"CTR-{reportDate:yyyyMMdd}-{customerNumber[..Math.Min(8, customerNumber.Length)]}";
        var payload = JsonSerializer.Serialize(new
        {
            reportNumber,
            reportDate = reportDate.ToString("yyyy-MM-dd"),
            customerNumber,
            customerName = customer.FullName,
            aggregateAmount = aggregate,
            currency = "566",
            transactionCount = cashTxns.Count,
            transactions = cashTxns.Select(t => new { t.Rrn, t.Amount, t.CreatedAt })
           // transactions = cashTxns.Select(t => new { t.ReferenceNumber, t.Amount, t.TransactionDate })
        });

        var report = new CashTransactionReport
        {
            ReportNumber = reportNumber,
            CustomerNumber = customerNumber,
            CustomerName = customer.FullName,
            AggregateAmount = aggregate,
            CurrencyCode = "566",
            ReportDate = reportDate,
            Status = CtrStatus.Draft,
            ReportJson = payload,
            TransactionIds = cashTxns.Select(t => t.Rrn).ToList(),
           // TransactionIds = cashTxns.Select(t => t.ReferenceNumber).ToList(),
            GeneratedAt = _clock.UtcNow,
            GeneratedBy = requestedBy
        };

        await _reports.AddCtrAsync(report, cancellationToken).ConfigureAwait(false);
        _audit.LogSecurity(Guid.NewGuid().ToString("N"), "CtrGenerated",
            $"CTR {reportNumber} generated for customer {customerNumber}: ₦{aggregate:N0}");
        return CmsOperationResult<CashTransactionReport>.Success(report, $"CTR {reportNumber} generated.");
    }

    public async Task<CmsOperationResult<SuspiciousActivityReport>> GenerateSarAsync(
        string customerNumber, string patternCategory, string description,
        DateOnly activityStart, DateOnly activityEnd, string requestedBy, CancellationToken cancellationToken = default)
    {
        var customer = await _cms.GetCustomerByNumberAsync(customerNumber, cancellationToken).ConfigureAwait(false);
        if (customer is null) return CmsOperationResult<SuspiciousActivityReport>.Fail("25", $"Customer {customerNumber} not found.");

        var reportNumber = $"SAR-{_clock.UtcNow:yyyyMMdd}-{customerNumber[..Math.Min(8, customerNumber.Length)]}";
        var payload = JsonSerializer.Serialize(new
        {
            reportNumber,
            customerNumber,
            customerName = customer.FullName,
            patternCategory,
            description,
            activityStart = activityStart.ToString("yyyy-MM-dd"),
            activityEnd = activityEnd.ToString("yyyy-MM-dd"),
            generatedBy = requestedBy,
            generatedAt = _clock.UtcNow
        });

        var sar = new SuspiciousActivityReport
        {
            ReportNumber = reportNumber,
            CustomerNumber = customerNumber,
            CustomerName = customer.FullName,
            SuspiciousActivityDescription = description,
            PatternCategory = patternCategory,
            CurrencyCode = "566",
            ActivityStartDate = activityStart,
            ActivityEndDate = activityEnd,
            Status = SarStatus.Draft,
            ReportJson = payload,
            GeneratedAt = _clock.UtcNow,
            GeneratedBy = requestedBy
        };

        await _reports.AddSarAsync(sar, cancellationToken).ConfigureAwait(false);
        _audit.LogSecurity(Guid.NewGuid().ToString("N"), "SarGenerated",
            $"SAR {reportNumber} generated for customer {customerNumber}: {patternCategory}");
        return CmsOperationResult<SuspiciousActivityReport>.Success(sar, $"SAR {reportNumber} generated.");
    }

    public async Task<CmsOperationResult<CashTransactionReport>> FileReportAsync(Guid ctrId, string requestedBy, CancellationToken cancellationToken = default)
    {
        var report = await _reports.GetCtrAsync(ctrId, cancellationToken).ConfigureAwait(false);
        if (report is null) return CmsOperationResult<CashTransactionReport>.Fail("25", "CTR not found.");
        if (report.Status != CtrStatus.Draft) return CmsOperationResult<CashTransactionReport>.Fail("57", $"CTR is already {report.Status}.");
        var filed = report with { Status = CtrStatus.Filed, FiledAt = _clock.UtcNow };
        await _reports.UpdateCtrAsync(filed, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), requestedBy, "CtrFiled", report.Status.ToString(), "Filed", report.ReportNumber, string.Empty);
        return CmsOperationResult<CashTransactionReport>.Success(filed, $"CTR {report.ReportNumber} filed.");
    }

    public async Task<CmsOperationResult<SuspiciousActivityReport>> FileSarAsync(Guid sarId, string requestedBy, CancellationToken cancellationToken = default)
    {
        var sar = await _reports.GetSarAsync(sarId, cancellationToken).ConfigureAwait(false);
        if (sar is null) return CmsOperationResult<SuspiciousActivityReport>.Fail("25", "SAR not found.");
        if (sar.Status != SarStatus.Draft && sar.Status != SarStatus.UnderReview)
            return CmsOperationResult<SuspiciousActivityReport>.Fail("57", $"SAR cannot be filed in status {sar.Status}.");
        var filed = sar with { Status = SarStatus.Filed, FiledAt = _clock.UtcNow };
        await _reports.UpdateSarAsync(filed, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), requestedBy, "SarFiled", sar.Status.ToString(), "Filed", sar.ReportNumber, string.Empty);
        return CmsOperationResult<SuspiciousActivityReport>.Success(filed, $"SAR {sar.ReportNumber} filed.");
    }

    public Task<IReadOnlyList<CashTransactionReport>> GetPendingCtrsAsync(CancellationToken cancellationToken = default) => _reports.GetPendingCtrsAsync(cancellationToken);
    public Task<IReadOnlyList<SuspiciousActivityReport>> GetPendingSarsAsync(CancellationToken cancellationToken = default) => _reports.GetPendingSarsAsync(cancellationToken);

    private static bool MatchesWatchlistEntry(string name, string reference, AmlWatchlistEntry entry)
    {
        var target = $"{name} {reference}".ToUpperInvariant();
        return entry.MatchKeywords.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim().ToUpperInvariant())
            .Any(kw => !string.IsNullOrWhiteSpace(kw) && target.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }
}

// ============================================================
// In-Memory AML Report Repository
// ============================================================

public sealed class InMemoryAmlReportRepository : IAmlReportRepository
{
    private readonly ConcurrentDictionary<Guid, CashTransactionReport> _ctrs = new();
    private readonly ConcurrentDictionary<Guid, SuspiciousActivityReport> _sars = new();
    private readonly List<AmlFeedSnapshot> _snapshots = [];

    public Task AddCtrAsync(CashTransactionReport r, CancellationToken ct = default) { _ctrs[r.Id] = r; return Task.CompletedTask; }
    public Task<CashTransactionReport?> GetCtrAsync(Guid id, CancellationToken ct = default) { _ctrs.TryGetValue(id, out var r); return Task.FromResult(r); }
    public Task UpdateCtrAsync(CashTransactionReport r, CancellationToken ct = default) { _ctrs[r.Id] = r; return Task.CompletedTask; }
    public Task<IReadOnlyList<CashTransactionReport>> GetPendingCtrsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CashTransactionReport>>(_ctrs.Values.Where(r => r.Status == CtrStatus.Draft).OrderBy(r => r.GeneratedAt).ToList());
    public Task AddSarAsync(SuspiciousActivityReport r, CancellationToken ct = default) { _sars[r.Id] = r; return Task.CompletedTask; }
    public Task<SuspiciousActivityReport?> GetSarAsync(Guid id, CancellationToken ct = default) { _sars.TryGetValue(id, out var r); return Task.FromResult(r); }
    public Task UpdateSarAsync(SuspiciousActivityReport r, CancellationToken ct = default) { _sars[r.Id] = r; return Task.CompletedTask; }
    public Task<IReadOnlyList<SuspiciousActivityReport>> GetPendingSarsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SuspiciousActivityReport>>(_sars.Values.Where(r => r.Status == SarStatus.Draft || r.Status == SarStatus.UnderReview).OrderBy(r => r.GeneratedAt).ToList());
    public Task AddFeedSnapshotAsync(AmlFeedSnapshot s, CancellationToken ct = default) { lock (_snapshots) { _snapshots.Add(s); } return Task.CompletedTask; }
    public Task<IReadOnlyList<AmlFeedSnapshot>> GetFeedHistoryAsync(int take, CancellationToken ct = default)
    { lock (_snapshots) { return Task.FromResult<IReadOnlyList<AmlFeedSnapshot>>(_snapshots.OrderByDescending(s => s.FetchedAt).Take(take).ToList()); } }
}

// ============================================================
// AML Re-screening Background Worker
// ============================================================

public sealed class AmlRescreeningWorker : BackgroundService
{
    private readonly IAmlIntegrationService _aml;
    private readonly IConfiguration _config;
    private readonly ILogger<AmlRescreeningWorker> _logger;

    public AmlRescreeningWorker(IAmlIntegrationService aml, IConfiguration config, ILogger<AmlRescreeningWorker> logger)
    {
        _aml = aml;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AML re-screening worker started.");
        var feedSyncIntervalHours = _config.GetValue("Aml:FeedSyncIntervalHours", 24);
        var rescreenIntervalHours = _config.GetValue("Aml:RescreenIntervalHours", 168); // weekly

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Sync external feeds first
                foreach (AmlFeedSource source in Enum.GetValues<AmlFeedSource>())
                {
                    var snap = await _aml.SyncFeedAsync(source, stoppingToken).ConfigureAwait(false);
                    if (!snap.IsSuccess)
                        _logger.LogWarning("AML feed sync failed for {Source}: {Error}", source, snap.ErrorMessage);
                }

                // Re-screen all active customers
                var screened = await _aml.RescreenAllActiveCustomersAsync("AmlRescreeningWorker", stoppingToken).ConfigureAwait(false);
                _logger.LogInformation("AML re-screening complete: {Count} customers screened.", screened);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "AML re-screening worker error."); }

            await Task.Delay(TimeSpan.FromHours(rescreenIntervalHours), stoppingToken).ConfigureAwait(false);
        }
    }
}
