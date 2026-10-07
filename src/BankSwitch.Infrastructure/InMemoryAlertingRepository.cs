using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryAlertingRepository : IAlertingRepository
{
    private readonly ConcurrentDictionary<Guid, AlertRule> _rules = new();
    private readonly ConcurrentDictionary<Guid, AlertEvent> _events = new();

    // ---------------------------------------------------------------
    // Rules
    // ---------------------------------------------------------------

    public Task AddRuleAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        _rules[rule.Id] = rule;
        return Task.CompletedTask;
    }

    public Task<AlertRule?> GetRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _rules.TryGetValue(id, out var rule);
        return Task.FromResult(rule);
    }

    public Task<IReadOnlyList<AlertRule>> GetActiveRulesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AlertRule>>(_rules.Values.Where(r => r.IsActive).ToList());

    public Task<IReadOnlyList<AlertRule>> GetAllRulesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AlertRule>>(_rules.Values.OrderBy(r => r.Name).ToList());

    public Task UpdateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        _rules[rule.Id] = rule;
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------

    public Task AddEventAsync(AlertEvent alertEvent, CancellationToken cancellationToken = default)
    {
        _events[alertEvent.Id] = alertEvent;
        return Task.CompletedTask;
    }

    public Task<AlertEvent?> GetEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _events.TryGetValue(id, out var evt);
        return Task.FromResult(evt);
    }

    public Task<IReadOnlyList<AlertEvent>> GetEventsAsync(AlertEventFilter filter, CancellationToken cancellationToken = default)
    {
        IEnumerable<AlertEvent> query = _events.Values;
        if (filter.Severity.HasValue) query = query.Where(e => e.Severity == filter.Severity.Value);
        if (filter.Status.HasValue) query = query.Where(e => e.Status == filter.Status.Value);
        if (filter.RuleType.HasValue) query = query.Where(e => e.RuleType == filter.RuleType.Value);
        if (filter.Since.HasValue) query = query.Where(e => e.FiredAt >= filter.Since.Value);
        return Task.FromResult<IReadOnlyList<AlertEvent>>(query.OrderByDescending(e => e.FiredAt).Take(filter.MaxRows).ToList());
    }

    public Task UpdateEventAsync(AlertEvent alertEvent, CancellationToken cancellationToken = default)
    {
        _events[alertEvent.Id] = alertEvent;
        return Task.CompletedTask;
    }

    public Task<AlertEvent?> GetLatestEventForRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var latest = _events.Values
            .Where(e => e.RuleId == ruleId)
            .MaxBy(e => e.FiredAt);
        return Task.FromResult(latest);
    }
}
