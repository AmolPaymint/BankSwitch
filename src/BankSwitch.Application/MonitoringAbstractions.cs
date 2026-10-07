// MonitoringAbstractions.cs — view-model records used by the monitoring Razor pages.
// The IMonitoringService interface is now defined in MonitoringAndAlertingAbstractions.cs.
// This file is retained for the ApplicationHealthSnapshot and DeviceHealth types used
// by existing Razor pages and tests.
namespace BankSwitch.Application;

/// <summary>Application-level health: cluster node heartbeats plus switch throughput over the trailing window.</summary>
public sealed record ApplicationHealthSnapshot(
    ClusterHealthSnapshot Cluster,
    TimeSpan Window,
    int TransactionCount,
    int ApprovedCount,
    int DeclinedCount,
    double AverageLatencyMilliseconds);

/// <summary>Device/hardware health for one configured source or sink node, derived from configuration plus recent activity.</summary>
public sealed record DeviceHealth(
    string NodeId,
    string Name,
    string Direction,
    bool ConfiguredActive,
    int TpsLimit,
    int TransactionCount,
    int DeclinedCount,
    double AverageLatencyMilliseconds,
    DateTimeOffset? LastTransactionAt,
    string Status);
