using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BankSwitch.Infrastructure;

public sealed class InMemorySwitchStore : ITransactionRepository, INodeRepository, IRouteRepository, IReversalRepository, ISwitchConfigurationRepository, ITransactionReportRepository
{
    private readonly ConcurrentDictionary<string, SourceNode> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, SinkNode> _sinks = new();
    private readonly ConcurrentDictionary<Guid, RouteDefinition> _routes = new();
    private readonly ConcurrentDictionary<Guid, Scheme> _schemes = new();
    private readonly ConcurrentDictionary<Guid, Fee> _fees = new();
    private readonly ConcurrentDictionary<Guid, Institution> _institutions = new();
    private readonly ConcurrentBag<TransactionLog> _transactions = new();
    private readonly ConcurrentDictionary<Guid, ReversalWorkItem> _reversals = new();
    private readonly ConcurrentDictionary<Guid, Guid> _acceptedReversalByOriginal = new();
    private readonly ISensitiveDataProtector _protector;
    private readonly ILogger<InMemorySwitchStore> _logger;

    public InMemorySwitchStore() : this(new DevelopmentSensitiveDataProtector(), NullLogger<InMemorySwitchStore>.Instance) { }

    public InMemorySwitchStore(ISensitiveDataProtector protector, ILogger<InMemorySwitchStore> logger)
    {
        _protector = protector;
        _logger = logger;
        SeedDevelopmentData();
    }

    public Task<bool> ExistsDuplicateAsync(string sourceNodeId, string stan, string rrn, decimal amount, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var exists = _transactions.Any(x =>
            x.SourceNodeId.Equals(sourceNodeId, StringComparison.OrdinalIgnoreCase)
            && x.Stan == stan
            && x.Rrn == rrn
            && x.Amount == amount
            && DateOnly.FromDateTime(x.CreatedAt.UtcDateTime) == businessDate);
        return Task.FromResult(exists);
    }

    public Task SaveTransactionAsync(TransactionLog log, CancellationToken cancellationToken = default)
    {
        _transactions.Add(log);
        return Task.CompletedTask;
    }

    private static readonly HashSet<string> _approvedCodes = new(StringComparer.Ordinal) { "00", "08", "10", "11" };

    public Task<IReadOnlyList<TransactionLog>> GetUnclearedTransactionsAsync(DateOnly businessDate, string settlementProfile, CancellationToken cancellationToken = default)
    {
        // Approved purchase/debit transactions (not reversals), on the business date, not yet cleared,
        // matching the settlement profile (or all if settlement profile is empty = DEFAULT)
        var results = _transactions.Where(t =>
            _approvedCodes.Contains(t.ResponseCode) &&
            !t.IsCleared &&
            t.Mti is "0200" or "0210" &&
            DateOnly.FromDateTime(t.CreatedAt.UtcDateTime) == businessDate &&
            (string.IsNullOrWhiteSpace(settlementProfile) || string.IsNullOrWhiteSpace(t.SettlementProfile) ||
             string.Equals(t.SettlementProfile, settlementProfile, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return Task.FromResult<IReadOnlyList<TransactionLog>>(results);
    }

    public Task MarkTransactionsClearedAsync(IReadOnlyCollection<string> correlationIds, Guid clearingBatchId, CancellationToken cancellationToken = default)
    {
        // In ConcurrentBag we can't mutate in-place — rebuild the bag with cleared records updated
        // This is acceptable in dev/test where the in-memory store is authoritative
        var idSet = new HashSet<string>(correlationIds, StringComparer.Ordinal);
        var updated = _transactions
            .Select(t => idSet.Contains(t.CorrelationId) ? t with { IsCleared = true, ClearingBatchId = clearingBatchId } : t)
            .ToList();
        // Re-populate: swap all items
        while (_transactions.TryTake(out _)) { }
        foreach (var t in updated) _transactions.Add(t);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TransactionLog>> GetApprovedTransactionsByDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        // Returns ALL approved transactions (cleared or not) for reconciliation
        var results = _transactions.Where(t =>
            _approvedCodes.Contains(t.ResponseCode) &&
            t.Mti is "0200" or "0210" &&
            DateOnly.FromDateTime(t.CreatedAt.UtcDateTime) == businessDate)
            .OrderBy(t => t.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<TransactionLog>>(results);
    }

    public Task<SourceNode?> GetSourceNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        _sources.TryGetValue(nodeId, out var node);
        return Task.FromResult(node);
    }

    public Task<SinkNode?> GetSinkNodeAsync(Guid sinkNodeId, CancellationToken cancellationToken = default)
    {
        _sinks.TryGetValue(sinkNodeId, out var node);
        return Task.FromResult(node);
    }

    public Task<RouteDefinition?> GetRouteByBinAsync(string binPrefix, CancellationToken cancellationToken = default)
    {
        var route = _routes.Values
            .Where(x => x.IsActive && binPrefix.StartsWith(x.BinPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.BinPrefix.Length)
            .ThenByDescending(x => x.Priority)
            .FirstOrDefault();
        return Task.FromResult(route);
    }

    public Task<RouteDefinition?> GetBestRouteAsync(RouteMatchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var route = _routes.Values
            .Where(x => x.IsActive && RouteMatches(x, criteria))
            .Select(x => new { Route = x, Score = ScoreRoute(x, criteria) })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Route.Priority)
            .ThenByDescending(x => LongestCardRange(x.Route))
            .ThenBy(x => x.Route.BinPrefix, StringComparer.Ordinal)
            .FirstOrDefault()?.Route;
        return Task.FromResult(route);
    }

    public Task<Scheme?> GetSchemeForSourceAndRouteAsync(Guid sourceNodeId, Guid routeId, CancellationToken cancellationToken = default)
    {
        var scheme = _schemes.Values.FirstOrDefault(x => x.SourceNodeId == sourceNodeId && x.RouteId == routeId && x.IsActive);
        return Task.FromResult(scheme);
    }

    public Task<Fee?> GetFeeAsync(Guid feeId, CancellationToken cancellationToken = default)
    {
        _fees.TryGetValue(feeId, out var fee);
        return Task.FromResult(fee);
    }

    public Task<ReversalWorkItem?> TryStartReversalAsync(string originalDataElement, string correlationId, CancellationToken cancellationToken = default)
    {
        var original = _transactions.FirstOrDefault(x => x.Rrn == originalDataElement || x.CorrelationId == originalDataElement);
        if (original is null) return Task.FromResult<ReversalWorkItem?>(null);
        if (_acceptedReversalByOriginal.ContainsKey(original.Id)) return Task.FromResult<ReversalWorkItem?>(null);
        if (_reversals.TryGetValue(original.Id, out var existing) && existing.State is ReversalState.Pending or ReversalState.RetryScheduled or ReversalState.Sent)
        {
            return Task.FromResult<ReversalWorkItem?>(existing);
        }

        var sink = _sinks.Values.FirstOrDefault(x => x.NodeId.Equals(original.SinkNodeId, StringComparison.OrdinalIgnoreCase)) ?? _sinks.Values.FirstOrDefault();
        if (sink is null) return Task.FromResult<ReversalWorkItem?>(null);
        var pan = _protector.Unprotect(original.PanToken, "PAN");
        if (string.IsNullOrWhiteSpace(pan) || pan.Contains('*'))
        {
            _logger.LogError(
                "Reversal {OriginalTransactionId}: PAN token decrypted to a masked or empty value — reversal permanently abandoned.",
                original.Id);
        //    _reversals[original.Id] = original with { ReversalState = ReversalState.Failed };
        _reversals[original.Id] = new ReversalWorkItem( OriginalTransactionId: original.Id,
            OriginalDataElement: string.Empty, // TODO: Replace with your actual ISO message data string if available
            ReversalMessage: null!,            // TODO: Pass your actual IsoMessage instance if you have one here
            SinkNodeId: Guid.TryParse(original.SinkNodeId, out var sinkGuid) ? sinkGuid : Guid.Empty, // Parses string SinkNodeId to Guid
            CorrelationId: original.CorrelationId,
            AttemptCount: 1,                   // First and final failed attempt
            State: ReversalState.Failed
        );

            return Task.FromResult<ReversalWorkItem?>(null);
        }

        var reversalMessage = new IsoMessage("0420")
            .SetField(2, pan)
            .SetField(3, "200000")
            .SetField(4, ((long)(original.Amount * 100m)).ToString("000000000000"))
            .SetField(7, DateTimeOffset.UtcNow.ToString("MMddHHmmss"))
            .SetField(11, original.Stan)
            .SetField(37, original.Rrn)
            .SetField(41, "REVERSAL")
            .SetField(49, original.CurrencyCode)
            .SetField(90, NormalizeOriginalDataElement(originalDataElement))
            .SetField(123, "000000000000001");
        reversalMessage.CorrelationId = correlationId;

        var item = new ReversalWorkItem(original.Id, originalDataElement, reversalMessage, sink.Id, correlationId, 0, ReversalState.Pending);
        _reversals[original.Id] = item;
        return Task.FromResult<ReversalWorkItem?>(item);
    }

    public Task<IReadOnlyCollection<ReversalWorkItem>> GetDueReversalsAsync(DateTimeOffset now, int maxAttempts, CancellationToken cancellationToken = default)
    {
        var due = _reversals.Values.Where(x => x.AttemptCount < maxAttempts && x.State is ReversalState.Pending or ReversalState.RetryScheduled && x.NextAttemptAt <= now).ToArray();
        return Task.FromResult<IReadOnlyCollection<ReversalWorkItem>>(due);
    }

    public Task MarkAcceptedAsync(ReversalWorkItem item, string responseCode, CancellationToken cancellationToken = default)
    {
        _acceptedReversalByOriginal[item.OriginalTransactionId] = Guid.NewGuid();
        _reversals[item.OriginalTransactionId] = item with { State = ReversalState.Accepted, AttemptCount = item.AttemptCount + 1 };
        return Task.CompletedTask;
    }

    public Task MarkRejectedAsync(ReversalWorkItem item, string responseCode, CancellationToken cancellationToken = default)
    {
        _reversals[item.OriginalTransactionId] = item with { State = ReversalState.Rejected, AttemptCount = item.AttemptCount + 1 };
        return Task.CompletedTask;
    }

    public Task ScheduleRetryAsync(ReversalWorkItem item, string reason, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken = default)
    {
        _reversals[item.OriginalTransactionId] = item with { AttemptCount = item.AttemptCount + 1, State = ReversalState.RetryScheduled, NextAttemptAt = nextAttemptAt };
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(ReversalWorkItem item, string reason, CancellationToken cancellationToken = default)
    {
        _reversals[item.OriginalTransactionId] = item with { State = ReversalState.Failed };
        return Task.CompletedTask;
    }

    private static string NormalizeOriginalDataElement(string value) => value.Length == 42 ? value : value.PadRight(42, '0')[..42];

    // ---------------------------------------------------------------
    // ISwitchConfigurationRepository
    // ---------------------------------------------------------------

    public Task<IReadOnlyList<SourceNode>> GetSourceNodesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SourceNode>>(_sources.Values.OrderBy(x => x.NodeId, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<SourceNode?> GetSourceNodeByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_sources.Values.FirstOrDefault(x => x.Id == id));

    public Task AddSourceNodeAsync(SourceNode node, CancellationToken cancellationToken = default)
    {
        _sources[node.NodeId] = node;
        return Task.CompletedTask;
    }

    public Task UpdateSourceNodeAsync(SourceNode node, CancellationToken cancellationToken = default)
    {
        var existingKey = _sources.FirstOrDefault(kv => kv.Value.Id == node.Id).Key;
        if (existingKey is not null && !string.Equals(existingKey, node.NodeId, StringComparison.OrdinalIgnoreCase))
        {
            _sources.TryRemove(existingKey, out _);
        }
        _sources[node.NodeId] = node;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SinkNode>> GetSinkNodesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SinkNode>>(_sinks.Values.OrderBy(x => x.NodeId, StringComparer.OrdinalIgnoreCase).ToList());

    public Task AddSinkNodeAsync(SinkNode node, CancellationToken cancellationToken = default)
    {
        _sinks[node.Id] = node;
        return Task.CompletedTask;
    }

    public Task UpdateSinkNodeAsync(SinkNode node, CancellationToken cancellationToken = default)
    {
        _sinks[node.Id] = node;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RouteDefinition>> GetRoutesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RouteDefinition>>(_routes.Values.OrderByDescending(x => x.Priority).ThenBy(x => x.BinPrefix, StringComparer.Ordinal).ToList());

    public Task<RouteDefinition?> GetRouteByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_routes.Values.FirstOrDefault(x => x.Id == id));

    public Task AddRouteAsync(RouteDefinition route, CancellationToken cancellationToken = default)
    {
        _routes[route.Id] = route;
        return Task.CompletedTask;
    }

    public Task UpdateRouteAsync(RouteDefinition route, CancellationToken cancellationToken = default)
    {
        _routes[route.Id] = route;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Fee>> GetFeesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Fee>>(_fees.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList());

    public Task AddFeeAsync(Fee fee, CancellationToken cancellationToken = default)
    {
        _fees[fee.Id] = fee;
        return Task.CompletedTask;
    }

    public Task UpdateFeeAsync(Fee fee, CancellationToken cancellationToken = default)
    {
        _fees[fee.Id] = fee;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Scheme>> GetSchemesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Scheme>>(_schemes.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<Scheme?> GetSchemeByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_schemes.Values.FirstOrDefault(x => x.Id == id));

    public Task AddSchemeAsync(Scheme scheme, CancellationToken cancellationToken = default)
    {
        _schemes[scheme.Id] = scheme;
        return Task.CompletedTask;
    }

    public Task UpdateSchemeAsync(Scheme scheme, CancellationToken cancellationToken = default)
    {
        _schemes[scheme.Id] = scheme;
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------
    // ITransactionReportRepository
    // ---------------------------------------------------------------

    public Task<TransactionReportPage> GetTransactionsAsync(TransactionReportFilter filter, CancellationToken cancellationToken = default)
    {
        IEnumerable<TransactionLog> query = _transactions;
        if (filter.From.HasValue) query = query.Where(x => x.CreatedAt >= filter.From.Value);
        if (filter.To.HasValue) query = query.Where(x => x.CreatedAt <= filter.To.Value);
        if (!string.IsNullOrWhiteSpace(filter.SourceNodeId)) query = query.Where(x => string.Equals(x.SourceNodeId, filter.SourceNodeId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.SinkNodeId)) query = query.Where(x => string.Equals(x.SinkNodeId, filter.SinkNodeId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Mti)) query = query.Where(x => string.Equals(x.Mti, filter.Mti, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(filter.ResponseCode)) query = query.Where(x => string.Equals(x.ResponseCode, filter.ResponseCode, StringComparison.Ordinal));

        var matched = query.OrderByDescending(x => x.CreatedAt).ToList();
        var totalCount = matched.Count;
        var approvedCount = matched.Count(x => x.ResponseCode == "00");
        var declinedCount = totalCount - approvedCount;
        var totalAmount = matched.Sum(x => x.Amount);
        var averageLatency = totalCount == 0 ? 0d : matched.Average(x => (double)x.LatencyMilliseconds);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Max(1, filter.PageSize);
        var items = matched.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new TransactionReportPage(items, totalCount, approvedCount, declinedCount, totalAmount, averageLatency));
    }

    public Task<IReadOnlyList<NodeActivitySummary>> GetNodeActivitySummaryAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var recent = _transactions.Where(x => x.CreatedAt >= since).ToList();
        var results = new List<NodeActivitySummary>();

        foreach (var group in recent.GroupBy(x => x.SourceNodeId, StringComparer.OrdinalIgnoreCase))
        {
            results.Add(new NodeActivitySummary(group.Key, "Source", group.Count(), group.Count(x => x.ResponseCode != "00"), group.Average(x => (double)x.LatencyMilliseconds), group.Max(x => x.CreatedAt)));
        }
        foreach (var group in recent.GroupBy(x => x.SinkNodeId, StringComparer.OrdinalIgnoreCase))
        {
            results.Add(new NodeActivitySummary(group.Key, "Sink", group.Count(), group.Count(x => x.ResponseCode != "00"), group.Average(x => (double)x.LatencyMilliseconds), group.Max(x => x.CreatedAt)));
        }

        return Task.FromResult<IReadOnlyList<NodeActivitySummary>>(results);
    }

    // ---------------------------------------------------------------
    // ISwitchConfigurationRepository - institutions
    // ---------------------------------------------------------------

    public Task<IReadOnlyList<Institution>> GetInstitutionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Institution>>(_institutions.Values.OrderBy(x => x.Code, StringComparer.Ordinal).ToList());

    public Task<Institution?> GetInstitutionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _institutions.TryGetValue(id, out var institution);
        return Task.FromResult(institution);
    }

    public Task<Institution?> GetInstitutionByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var match = _institutions.Values.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    public Task AddInstitutionAsync(Institution institution, CancellationToken cancellationToken = default)
    {
        _institutions[institution.Id] = institution;
        return Task.CompletedTask;
    }

    public Task UpdateInstitutionAsync(Institution institution, CancellationToken cancellationToken = default)
    {
        _institutions[institution.Id] = institution;
        return Task.CompletedTask;
    }

    private static bool RouteMatches(RouteDefinition route, RouteMatchCriteria criteria)
        => MatchesCard(route, criteria)
           && MatchesSet(route.CountryCodes, criteria.CountryCode)
           && MatchesSet(route.MerchantCategoryCodes, criteria.MerchantCategoryCode)
           && MatchesSet(route.CurrencyCodes, criteria.CurrencyCode)
           && MatchesSet(route.DeviceCodes, criteria.DeviceCode)
           && MatchesSet(route.InterchangeCodes, criteria.InterchangeCode)
           && MatchesSet(route.InstitutionCodes, criteria.InstitutionCode)
           && MatchesSet(route.ProductCodes, criteria.ProductCode)
           && MatchesSet(route.NetworkCodes, criteria.NetworkCode)
           && MatchesRangeSet(route.AccountRanges, criteria.AccountNumber);

    private static int ScoreRoute(RouteDefinition route, RouteMatchCriteria criteria)
    {
        var score = 0;
        if (MatchesCard(route, criteria)) score += 10 + LongestCardRange(route);
        if (HasMatch(route.CountryCodes, criteria.CountryCode)) score += 10;
        if (HasMatch(route.MerchantCategoryCodes, criteria.MerchantCategoryCode)) score += 10;
        if (HasMatch(route.CurrencyCodes, criteria.CurrencyCode)) score += 10;
        if (HasMatch(route.DeviceCodes, criteria.DeviceCode)) score += 10;
        if (HasMatch(route.InterchangeCodes, criteria.InterchangeCode)) score += 10;
        if (HasMatch(route.InstitutionCodes, criteria.InstitutionCode)) score += 10;
        if (HasMatch(route.ProductCodes, criteria.ProductCode)) score += 10;
        if (HasMatch(route.NetworkCodes, criteria.NetworkCode)) score += 10;
        if (HasRangeMatch(route.AccountRanges, criteria.AccountNumber)) score += 10;
        return score;
    }

    private static bool MatchesCard(RouteDefinition route, RouteMatchCriteria criteria)
    {
        var ranges = route.CardRangePrefixes.Count > 0 ? route.CardRangePrefixes : new HashSet<string> { route.BinPrefix };
        return MatchesRangeSet(ranges, criteria.Pan) || MatchesRangeSet(ranges, criteria.BinPrefix);
    }

    private static bool MatchesSet(IReadOnlySet<string> allowed, string value) => allowed.Count == 0 || HasMatch(allowed, value);
    private static bool HasMatch(IReadOnlySet<string> allowed, string value) => !string.IsNullOrWhiteSpace(value) && allowed.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    private static bool MatchesRangeSet(IReadOnlySet<string> allowed, string value) => allowed.Count == 0 || HasRangeMatch(allowed, value);

    private static bool HasRangeMatch(IReadOnlySet<string> allowed, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();
        foreach (var rule in allowed)
        {
            if (string.IsNullOrWhiteSpace(rule)) continue;
            var r = rule.Trim();
            var parts = r.Split('-', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && normalized.Length >= parts[0].Length && normalized.Length >= parts[1].Length && string.CompareOrdinal(normalized[..parts[0].Length], parts[0]) >= 0 && string.CompareOrdinal(normalized[..parts[1].Length], parts[1]) <= 0) return true;
            if (normalized.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static int LongestCardRange(RouteDefinition route)
    {
        var ranges = route.CardRangePrefixes.Count > 0 ? route.CardRangePrefixes : new HashSet<string> { route.BinPrefix };
        return ranges.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Split('-', 2)[0].Trim().Length).DefaultIfEmpty(0).Max();
    }

    private void SeedDevelopmentData()
    {
        var sink = new SinkNode
        {
            NodeId = "SNK-DEV-001",
            Name = "Development Sink",
            Host = "127.0.0.1",
            Port = 5001,
            IsActive = true,
            PermittedMtis = new HashSet<string> { "0200", "0420" },
            PermittedChannels = new HashSet<string> { "01" },
            Security = new NodeSecurityProfile { RequireMtls = false, RequirePrivateNetwork = false, AllowedCidrs = new HashSet<string> { "127.0.0.1" } },
            Limits = new NodeLimits { TpsLimit = 100, MaxMessageBytes = 4096, IdleTimeout = TimeSpan.FromSeconds(65) }
        };
        _sinks[sink.Id] = sink;

        var source = new SourceNode
        {
            NodeId = "SRC-DEV-001",
            Name = "Development Source",
            IsActive = true,
            PermittedMtis = new HashSet<string> { "0200", "0420" },
            PermittedChannels = new HashSet<string> { "01" },
            AllowedBinRanges = new HashSet<string> { "539983" },
            Security = new NodeSecurityProfile { RequireMtls = false, RequirePrivateNetwork = false, AllowedCidrs = new HashSet<string> { "127.0.0.1" } },
            Limits = new NodeLimits { TpsLimit = 100, MaxMessageBytes = 4096, IdleTimeout = TimeSpan.FromSeconds(65) }
        };
        _sources[source.NodeId] = source;

        var route = new RouteDefinition { BinPrefix = "539983", SinkNodeId = sink.Id, IsActive = true };
        _routes[route.Id] = route;

        var fee = new Fee
        {
            Name = "Development flat fee",
            FlatAmount = 10.00m,
            PercentageOfTransaction = 0m,
            Minimum = 0m,
            Maximum = 0m,
            IsActive = true
        };
        _fees[fee.Id] = fee;

        var scheme = new Scheme
        {
            Name = "Development scheme",
            SourceNodeId = source.Id,
            RouteId = route.Id,
            IsActive = true,
            Permissions = new[]
            {
                new TransactionPermission("00", "01", fee.Id),
                new TransactionPermission("20", "01", fee.Id)
            }
        };
        _schemes[scheme.Id] = scheme;
    }
}