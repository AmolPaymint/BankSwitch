using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using BankSwitch.Application;

namespace BankSwitch.Admin.Services;

public interface IMakerCheckerService
{
    IReadOnlyCollection<ConfigChangeRequest> GetAll();
    ConfigChangeRequest Draft(string area, string oldValue, string newValue, string maker, string reason, string ticketReference, DateTimeOffset? effectiveAt);
    ConfigChangeRequest Submit(Guid id, string actor);
    ConfigChangeRequest Approve(Guid id, string checker);
    ConfigChangeRequest Activate(Guid id, string actor);
    ConfigChangeRequest Archive(Guid id, string actor);
}

public sealed class InMemoryMakerCheckerService : IMakerCheckerService
{
    private readonly ConcurrentDictionary<Guid, ConfigChangeRequest> _items = new();
    private readonly IAuditLogger _audit;

    public InMemoryMakerCheckerService(IAuditLogger audit) => _audit = audit;

    public IReadOnlyCollection<ConfigChangeRequest> GetAll() => _items.Values.OrderByDescending(x => x.CreatedAt).ToArray();

    public ConfigChangeRequest Draft(string area, string oldValue, string newValue, string maker, string reason, string ticketReference, DateTimeOffset? effectiveAt)
    {
        var request = new ConfigChangeRequest
        {
            Area = area,
            OldValue = oldValue,
            NewValue = newValue,
            Maker = maker,
            Reason = reason,
            TicketReference = ticketReference,
            EffectiveAt = effectiveAt,
            State = MakerCheckerState.Draft
        };
        _items[request.Id] = request;
        _audit.LogAdminAudit(request.CorrelationId, maker, "DraftConfigChange", oldValue, newValue, reason, ticketReference);
        return request;
    }

    public ConfigChangeRequest Submit(Guid id, string actor) => Mutate(id, actor, MakerCheckerState.Draft, MakerCheckerState.Submitted, "SubmitConfigChange");

    public ConfigChangeRequest Approve(Guid id, string checker)
    {
        var item = Get(id);
        if (item.State != MakerCheckerState.Submitted) throw new InvalidOperationException("Only submitted changes can be approved.");
        if (string.Equals(item.Maker, checker, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Maker cannot approve their own change.");
        var updated = item with { Checker = checker, ApprovedAt = DateTimeOffset.UtcNow, State = item.EffectiveAt.HasValue && item.EffectiveAt.Value > DateTimeOffset.UtcNow ? MakerCheckerState.Scheduled : MakerCheckerState.Approved };
        _items[id] = updated;
        _audit.LogAdminAudit(updated.CorrelationId, checker, "ApproveConfigChange", updated.OldValue, updated.NewValue, updated.Reason, updated.TicketReference);
        return updated;
    }

    public ConfigChangeRequest Activate(Guid id, string actor) => Mutate(id, actor, MakerCheckerState.Approved, MakerCheckerState.Active, "ActivateConfigChange", allowFromScheduled: true);

    public ConfigChangeRequest Archive(Guid id, string actor)
    {
        var item = Get(id);
        var updated = item with { State = MakerCheckerState.Archived };
        _items[id] = updated;
        _audit.LogAdminAudit(updated.CorrelationId, actor, "ArchiveConfigChange", updated.OldValue, updated.NewValue, updated.Reason, updated.TicketReference);
        return updated;
    }

    private ConfigChangeRequest Mutate(Guid id, string actor, MakerCheckerState expected, MakerCheckerState nextState, string action, bool allowFromScheduled = false)
    {
        var item = Get(id);
        if (item.State != expected && !(allowFromScheduled && item.State == MakerCheckerState.Scheduled))
        {
            throw new InvalidOperationException($"Change is {item.State}; expected {expected}.");
        }
        var updated = item with { State = nextState };
        _items[id] = updated;
        _audit.LogAdminAudit(updated.CorrelationId, actor, action, updated.OldValue, updated.NewValue, updated.Reason, updated.TicketReference);
        return updated;
    }

    private ConfigChangeRequest Get(Guid id) => _items.TryGetValue(id, out var item) ? item : throw new KeyNotFoundException("Config change was not found.");
}

public sealed record ConfigChangeRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
    [Required]
    public string Area { get; init; } = string.Empty;
    public string OldValue { get; init; } = string.Empty;
    [Required]
    public string NewValue { get; init; } = string.Empty;
    [Required]
    public string Maker { get; init; } = string.Empty;
    public string Checker { get; init; } = string.Empty;
    public DateTimeOffset? ApprovedAt { get; init; }
    public DateTimeOffset? EffectiveAt { get; init; }
    [Required]
    public string Reason { get; init; } = string.Empty;
    [Required]
    public string TicketReference { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public MakerCheckerState State { get; init; } = MakerCheckerState.Draft;
}

public enum MakerCheckerState
{
    Draft,
    Submitted,
    Approved,
    Scheduled,
    Active,
    Archived
}
