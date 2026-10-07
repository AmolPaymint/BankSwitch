using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

public sealed class MonitoringServiceTests
{
    private static (IMonitoringService Service, InMemorySwitchStore Store, IClock Clock) CreateService()
    {
        var store = new InMemorySwitchStore();
        var audit = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);
        var configurationService = new SwitchConfigurationService(store, audit);
        var clock = new SystemClock();
        var enterpriseService = new EnterpriseProductionService(new InMemoryEnterpriseProductionRepository(), audit, clock, new EnterpriseProductionOptions());
        return (new MonitoringService(store, configurationService, enterpriseService, null, null, null, clock), store, clock);
       // return (new MonitoringService(store, configurationService, enterpriseService, clock), store, clock);
    }

    private static TransactionLog MakeTransaction(string sourceNodeId, string sinkNodeId, string responseCode, long latencyMs, DateTimeOffset createdAt, decimal amount = 100m, string mti = "0200") => new()
    {
        CorrelationId = Guid.NewGuid().ToString("N"),
        Mti = mti,
        SourceNodeId = sourceNodeId,
        SinkNodeId = sinkNodeId,
        MaskedPan = "539983******1234",
        Stan = "000001",
        Rrn = "000000000001",
        Amount = amount,
        CurrencyCode = "USD",
        ResponseCode = responseCode,
        LatencyMilliseconds = latencyMs,
        RouteUsed = "539983",
        SchemeUsed = "Development scheme",
        FeeApplied = "Development flat fee",
        ReversalState = ReversalState.None,
        MacValidationStatus = "Valid",
        CreatedAt = createdAt
    };

    [Fact]
    public async Task Transaction_report_filters_by_source_node_and_aggregates_totals()
    {
        var (service, store, clock) = CreateService();
        var now = clock.UtcNow;
        await store.SaveTransactionAsync(MakeTransaction("SRC-DEV-001", "SNK-DEV-001", "00", 100, now.AddMinutes(-5), 100m));
        await store.SaveTransactionAsync(MakeTransaction("SRC-DEV-001", "SNK-DEV-001", "05", 200, now.AddMinutes(-3), 200m));
        await store.SaveTransactionAsync(MakeTransaction("SRC-OTHER", "SNK-DEV-001", "00", 50, now.AddMinutes(-2), 50m));

        var page = await service.GetTransactionReportAsync(new TransactionReportFilter(SourceNodeId: "SRC-DEV-001"));

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.ApprovedCount);
        Assert.Equal(1, page.DeclinedCount);
        Assert.Equal(300m, page.TotalAmount);
        Assert.Equal(150d, page.AverageLatencyMilliseconds);
    }

    [Fact]
    public async Task Transaction_report_pagination_returns_requested_page_size()
    {
        var (service, store, clock) = CreateService();
        var now = clock.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await store.SaveTransactionAsync(MakeTransaction("SRC-DEV-001", "SNK-DEV-001", "00", 100, now.AddMinutes(-i)));
        }

        var page1 = await service.GetTransactionReportAsync(new TransactionReportFilter(Page: 1, PageSize: 2));
        var page2 = await service.GetTransactionReportAsync(new TransactionReportFilter(Page: 2, PageSize: 2));

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);
    }

    [Fact]
    public async Task Node_activity_summary_groups_by_source_and_sink_with_decline_counts()
    {
        var (_, store, clock) = CreateService();
        var now = clock.UtcNow;
        await store.SaveTransactionAsync(MakeTransaction("SRC-DEV-001", "SNK-DEV-001", "00", 100, now.AddMinutes(-5)));
        await store.SaveTransactionAsync(MakeTransaction("SRC-DEV-001", "SNK-DEV-001", "05", 300, now.AddMinutes(-3)));

        var summary = await store.GetNodeActivitySummaryAsync(now.AddHours(-1));

        var source = Assert.Single(summary, x => x.Direction == "Source" && x.NodeId == "SRC-DEV-001");
        Assert.Equal(2, source.TransactionCount);
        Assert.Equal(1, source.DeclinedCount);
        Assert.Equal(200d, source.AverageLatencyMilliseconds);
    }

    [Fact]
    public async Task Device_health_marks_idle_configured_node_as_no_recent_activity()
    {
        var (service, _, _) = CreateService();

        var devices = await service.GetDeviceHealthAsync();

        var sourceDevice = Assert.Single(devices, x => x.Direction == "Source" && x.NodeId == "SRC-DEV-001");
        Assert.Equal("No recent activity", sourceDevice.Status);
    }

    [Fact]
    public async Task Device_health_marks_node_with_high_decline_rate_as_degraded()
    {
        var (service, store, clock) = CreateService();
        var now = clock.UtcNow;
        await store.SaveTransactionAsync(MakeTransaction("SRC-DEV-001", "SNK-DEV-001", "00", 100, now.AddMinutes(-5)));
        await store.SaveTransactionAsync(MakeTransaction("SRC-DEV-001", "SNK-DEV-001", "05", 100, now.AddMinutes(-4)));
        await store.SaveTransactionAsync(MakeTransaction("SRC-DEV-001", "SNK-DEV-001", "05", 100, now.AddMinutes(-3)));

        var devices = await service.GetDeviceHealthAsync();

        var sourceDevice = Assert.Single(devices, x => x.Direction == "Source" && x.NodeId == "SRC-DEV-001");
        Assert.Equal("Degraded", sourceDevice.Status);
    }

    [Fact]
    public async Task Application_health_includes_cluster_snapshot_and_recent_transaction_stats()
    {
        var (service, store, clock) = CreateService();
        var now = clock.UtcNow;
        await store.SaveTransactionAsync(MakeTransaction("SRC-DEV-001", "SNK-DEV-001", "00", 120, now.AddMinutes(-10)));

        var health = await service.GetApplicationHealthAsync();

        Assert.Equal(1, health.TransactionCount);
        Assert.Equal(1, health.ApprovedCount);
        Assert.Equal(0, health.DeclinedCount);
        Assert.NotNull(health.Cluster);
    }
}
