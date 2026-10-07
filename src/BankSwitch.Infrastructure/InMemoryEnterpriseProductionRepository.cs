using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryEnterpriseProductionRepository : IEnterpriseProductionRepository
{
    private readonly ConcurrentDictionary<string, CryptoKeyProfile> _keyProfiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, AmlWatchlistEntry> _watchlist = new();
    private readonly ConcurrentBag<AmlScreeningRecord> _amlScreenings = new();
    private readonly ConcurrentDictionary<Guid, ThreeDsAuthenticationRecord> _threeDs = new();
    private readonly ConcurrentDictionary<string, Guid> _threeDsByDsTransactionId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<FraudMonitoringEvent> _fraudEvents = new();
    private readonly ConcurrentDictionary<Guid, FraudAlert> _fraudAlerts = new();
    private readonly ConcurrentDictionary<Guid, SiemSecurityEvent> _siemEvents = new();
    private readonly ConcurrentDictionary<Guid, DataWarehouseExportJob> _warehouseJobs = new();
    private readonly ConcurrentDictionary<string, ClusterNodeHeartbeat> _heartbeats = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<FailoverEvent> _failoverEvents = new();
    private readonly ConcurrentDictionary<string, DisasterRecoveryPlan> _drPlans = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<DisasterRecoveryDrill> _drDrills = new();
    private readonly ConcurrentDictionary<Guid, RegulatoryReport> _regulatoryReports = new();
    private readonly ConcurrentDictionary<Guid, List<RegulatoryReportLine>> _regulatoryLines = new();

    public InMemoryEnterpriseProductionRepository()
    {
        Seed();
    }

    public Task AddOrUpdateKeyProfileAsync(CryptoKeyProfile profile, CancellationToken cancellationToken = default)
    {
        _keyProfiles[profile.KeyProfileCode] = profile;
        return Task.CompletedTask;
    }

    public Task<CryptoKeyProfile?> GetKeyProfileByCodeAsync(string keyProfileCode, CancellationToken cancellationToken = default)
    {
        _keyProfiles.TryGetValue(keyProfileCode ?? string.Empty, out var profile);
        return Task.FromResult(profile);
    }
    public Task<IReadOnlyList<CryptoKeyProfile>> GetAllKeyProfilesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CryptoKeyProfile>>(_keyProfiles.Values.OrderBy(p => p.KeyProfileCode).ToList());

    public Task AddAmlWatchlistEntryAsync(AmlWatchlistEntry entry, CancellationToken cancellationToken = default)
    {
        _watchlist[entry.Id] = entry;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AmlWatchlistEntry>> GetActiveAmlWatchlistEntriesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AmlWatchlistEntry>>(_watchlist.Values.Where(x => x.IsActive).OrderBy(x => x.ListType).ThenBy(x => x.EntityName).ToList());
    }

    public Task AddAmlScreeningRecordAsync(AmlScreeningRecord record, CancellationToken cancellationToken = default)
    {
        _amlScreenings.Add(record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AmlScreeningRecord>> GetAmlScreeningsAsync(string entityReference, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AmlScreeningRecord>>(_amlScreenings.Where(x => string.Equals(x.EntityReference, entityReference ?? string.Empty, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.CreatedAt).ToList());
    }

    public Task AddThreeDsAuthenticationAsync(ThreeDsAuthenticationRecord record, CancellationToken cancellationToken = default)
    {
        _threeDs[record.Id] = record;
        if (!string.IsNullOrWhiteSpace(record.DirectoryServerTransactionId)) _threeDsByDsTransactionId[record.DirectoryServerTransactionId] = record.Id;
        return Task.CompletedTask;
    }

    public Task<ThreeDsAuthenticationRecord?> GetThreeDsAuthenticationAsync(Guid authenticationId, CancellationToken cancellationToken = default)
    {
        _threeDs.TryGetValue(authenticationId, out var record);
        return Task.FromResult(record);
    }

    public Task<ThreeDsAuthenticationRecord?> GetThreeDsAuthenticationByDsTransactionIdAsync(string directoryServerTransactionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_threeDsByDsTransactionId.TryGetValue(directoryServerTransactionId ?? string.Empty, out var id) && _threeDs.TryGetValue(id, out var record) ? record : null);
    }

    public Task UpdateThreeDsAuthenticationAsync(ThreeDsAuthenticationRecord record, CancellationToken cancellationToken = default)
    {
        _threeDs[record.Id] = record;
        if (!string.IsNullOrWhiteSpace(record.DirectoryServerTransactionId)) _threeDsByDsTransactionId[record.DirectoryServerTransactionId] = record.Id;
        return Task.CompletedTask;
    }

    public Task AddFraudMonitoringEventAsync(FraudMonitoringEvent fraudEvent, CancellationToken cancellationToken = default)
    {
        _fraudEvents.Add(fraudEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FraudMonitoringEvent>> GetRecentFraudEventsAsync(string panHash, TimeSpan window, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var floor = now - window;
        var events = _fraudEvents.Where(x => string.Equals(x.PanHash, panHash ?? string.Empty, StringComparison.OrdinalIgnoreCase) && x.CreatedAt >= floor).OrderByDescending(x => x.CreatedAt).ToList();
        return Task.FromResult<IReadOnlyList<FraudMonitoringEvent>>(events);
    }

    public Task AddFraudAlertAsync(FraudAlert alert, CancellationToken cancellationToken = default)
    {
        _fraudAlerts[alert.Id] = alert;
        return Task.CompletedTask;
    }

    public Task<FraudAlert?> GetFraudAlertAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        _fraudAlerts.TryGetValue(alertId, out var alert);
        return Task.FromResult(alert);
    }

    public Task UpdateFraudAlertAsync(FraudAlert alert, CancellationToken cancellationToken = default)
    {
        _fraudAlerts[alert.Id] = alert;
        return Task.CompletedTask;
    }

    // B7 — Audit evidence generation
    public Task<IReadOnlyList<FraudAlert>> GetFraudAlertsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FraudAlert>>(_fraudAlerts.Values.OrderByDescending(a => a.CreatedAt).ToList());

    public Task AddSiemEventAsync(SiemSecurityEvent securityEvent, CancellationToken cancellationToken = default)
    {
        _siemEvents[securityEvent.Id] = securityEvent;
        return Task.CompletedTask;
    }

    public Task<SiemSecurityEvent?> GetSiemEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _siemEvents.TryGetValue(id, out var evt);
        return Task.FromResult(evt);
    }

    public Task<IReadOnlyList<SiemSecurityEvent>> GetPendingSiemEventsAsync(int take, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SiemSecurityEvent>>(_siemEvents.Values.Where(x => x.DeliveryStatus == SiemDeliveryStatus.Pending).OrderBy(x => x.CreatedAt).Take(take).ToList());
    }

    public Task UpdateSiemEventAsync(SiemSecurityEvent securityEvent, CancellationToken cancellationToken = default)
    {
        _siemEvents[securityEvent.Id] = securityEvent;
        return Task.CompletedTask;
    }

    public Task AddWarehouseExportJobAsync(DataWarehouseExportJob job, CancellationToken cancellationToken = default)
    {
        _warehouseJobs[job.Id] = job;
        return Task.CompletedTask;
    }

    public Task<DataWarehouseExportJob?> GetWarehouseExportJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _warehouseJobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<IReadOnlyList<DataWarehouseExportJob>> GetDueWarehouseExportJobsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<DataWarehouseExportJob>>(_warehouseJobs.Values.Where(x => x.Status == WarehouseExportStatus.Scheduled && x.CreatedAt <= now).OrderBy(x => x.CreatedAt).Take(take).ToList());
    }

    public Task UpdateWarehouseExportJobAsync(DataWarehouseExportJob job, CancellationToken cancellationToken = default)
    {
        _warehouseJobs[job.Id] = job;
        return Task.CompletedTask;
    }

    public Task AddOrUpdateHeartbeatAsync(ClusterNodeHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        _heartbeats[$"{heartbeat.NodeName}|{heartbeat.InstanceId}"] = heartbeat;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ClusterNodeHeartbeat>> GetClusterHeartbeatsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ClusterNodeHeartbeat>>(_heartbeats.Values.OrderBy(x => x.NodeName).ThenBy(x => x.InstanceId).ToList());
    }

    public Task AddFailoverEventAsync(FailoverEvent failoverEvent, CancellationToken cancellationToken = default)
    {
        _failoverEvents.Add(failoverEvent);
        return Task.CompletedTask;
    }

    public Task AddDisasterRecoveryPlanAsync(DisasterRecoveryPlan plan, CancellationToken cancellationToken = default)
    {
        _drPlans[plan.PlanCode] = plan;
        return Task.CompletedTask;
    }

    public Task<DisasterRecoveryPlan?> GetDisasterRecoveryPlanByCodeAsync(string planCode, CancellationToken cancellationToken = default)
    {
        _drPlans.TryGetValue(planCode ?? string.Empty, out var plan);
        return Task.FromResult(plan);
    }

    public Task AddDisasterRecoveryDrillAsync(DisasterRecoveryDrill drill, CancellationToken cancellationToken = default)
    {
        _drDrills.Add(drill);
        return Task.CompletedTask;
    }

    public Task AddRegulatoryReportAsync(RegulatoryReport report, IReadOnlyCollection<RegulatoryReportLine> lines, CancellationToken cancellationToken = default)
    {
        _regulatoryReports[report.Id] = report;
        _regulatoryLines[report.Id] = lines.ToList();
        return Task.CompletedTask;
    }

    public Task<RegulatoryReport?> GetRegulatoryReportAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        _regulatoryReports.TryGetValue(reportId, out var report);
        return Task.FromResult(report);
    }

    public Task<IReadOnlyList<RegulatoryReportLine>> BuildRegulatoryReportLinesAsync(RegulatoryReportType reportType, DateOnly periodStart, DateOnly periodEnd, string currencyCode, CancellationToken cancellationToken = default)
    {
        var lines = new List<RegulatoryReportLine>
        {
            new()
            {
                LineType = reportType.ToString(),
                Reference = $"{periodStart:yyyyMMdd}-{periodEnd:yyyyMMdd}",
                CurrencyCode = currencyCode ?? string.Empty,
                Count = Math.Max(1, (periodEnd.DayNumber - periodStart.DayNumber) + 1),
                Amount = 0m,
                Narrative = "Generated enterprise regulatory summary line. Replace with regulator-specific extractor when deploying to production."
            }
        };
        return Task.FromResult<IReadOnlyList<RegulatoryReportLine>>(lines);
    }

    public Task UpdateRegulatoryReportAsync(RegulatoryReport report, CancellationToken cancellationToken = default)
    {
        _regulatoryReports[report.Id] = report;
        return Task.CompletedTask;
    }

    private void Seed()
    {
        var key = new CryptoKeyProfile
        {
            KeyProfileCode = "DEV-MAC",
            Name = "Development MAC key profile",
            Purpose = CryptoKeyPurpose.Mac,
            HsmKeyAlias = "dev/mac/key-v1",
            HsmPartition = "development",
            Algorithm = "HMACSHA256",
            Status = CryptoKeyProfileStatus.Active,
            CreatedBy = "seed",
            CreatedAt = DateTimeOffset.UtcNow,
            EffectiveFrom = DateTimeOffset.UtcNow,
            RotationDueAt = DateTimeOffset.UtcNow.AddDays(90)
        };
        _keyProfiles[key.KeyProfileCode] = key;
    }
}
