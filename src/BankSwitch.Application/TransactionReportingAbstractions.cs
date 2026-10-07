using BankSwitch.Domain;

namespace BankSwitch.Application;

/// <summary>
/// Read-only query access to <see cref="TransactionLog"/> records for admin reporting and
/// monitoring. Implemented by <c>InMemorySwitchStore</c> and <c>SqlSwitchRepository</c>.
/// </summary>
public interface ITransactionReportRepository
{
    /// <summary>Returns a filtered, paged set of transactions plus aggregate totals over the full filtered set.</summary>
    Task<TransactionReportPage> GetTransactionsAsync(TransactionReportFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Returns per-node (source and sink) activity counters since the given instant, for device/hardware health.</summary>
    Task<IReadOnlyList<NodeActivitySummary>> GetNodeActivitySummaryAsync(DateTimeOffset since, CancellationToken cancellationToken = default);
}

public sealed record TransactionReportFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? SourceNodeId = null,
    string? SinkNodeId = null,
    string? Mti = null,
    string? ResponseCode = null,
    int Page = 1,
    int PageSize = 50);

/// <summary>A page of transaction log rows plus totals computed over the entire filtered set (not just this page).</summary>
public sealed record TransactionReportPage(
    IReadOnlyList<TransactionLog> Items,
    int TotalCount,
    int ApprovedCount,
    int DeclinedCount,
    decimal TotalAmount,
    double AverageLatencyMilliseconds);

/// <summary>Activity counters for one node (source or sink) over a time window, used for device/hardware health.</summary>
public sealed record NodeActivitySummary(
    string NodeId,
    string Direction,
    int TransactionCount,
    int DeclinedCount,
    double AverageLatencyMilliseconds,
    DateTimeOffset? LastTransactionAt);
