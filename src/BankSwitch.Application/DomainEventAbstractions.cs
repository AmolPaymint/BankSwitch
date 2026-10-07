using BankSwitch.Domain;

namespace BankSwitch.Application;

// ============================================================
// Event Bus interfaces
// ============================================================

/// <summary>
/// In-process domain event bus. Publishes <see cref="IDomainEvent"/> instances to
/// all registered <see cref="IEventHandler{T}"/> implementations asynchronously
/// (but within the same process — not a message broker).
///
/// Design choices:
/// - <strong>Synchronous-looking async</strong>: handlers are awaited sequentially.
///   This gives predictable ordering and makes failures visible to the caller
///   (important for GL posting and audit trails).
/// - <strong>Not a distributed bus</strong>: no cross-process delivery. For
///   multi-node delivery, replace with RabbitMQ / Azure Service Bus adapters
///   behind this interface.
/// - <strong>Replaceability</strong>: every service depends only on this interface,
///   so the implementation can be swapped without touching business logic.
/// </summary>
public interface IEventBus
{
    /// <summary>Publish a domain event to all registered handlers.</summary>
    Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent;
}

/// <summary>
/// Typed event handler. Implement this to react to a specific domain event.
/// Handlers are registered in DI and discovered by the event bus on first publish.
/// </summary>
public interface IEventHandler<in T> where T : IDomainEvent
{
    Task HandleAsync(T domainEvent, CancellationToken cancellationToken = default);
}

// ============================================================
// Built-in handlers
// ============================================================

/// <summary>
/// Writes structured audit log entries for all domain events.
/// Registered as a catch-all handler via the generic <c>AuditEventHandler&lt;T&gt;</c>
/// wrapper registered for each event type.
/// </summary>
public sealed class AuditDomainEventHandler<T> : IEventHandler<T> where T : IDomainEvent
{
    private readonly IAuditLogger _audit;
    public AuditDomainEventHandler(IAuditLogger audit) => _audit = audit;

    public Task HandleAsync(T domainEvent, CancellationToken cancellationToken = default)
    {
        _audit.LogSystem(domainEvent.CorrelationId,
            $"DomainEvent:{typeof(T).Name} eventId={domainEvent.EventId} at={domainEvent.OccurredAt:O}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Handler that triggers GL journal posting when an authorization is approved.
/// Decouples <c>CorePrepaidCmsService</c> from <c>IFinancialOperationsService</c>
/// — the service publishes <see cref="AuthorizationApprovedEvent"/> and this handler
/// drives the ledger update.
/// </summary>
public sealed class AuthorizationApprovedGlHandler : IEventHandler<AuthorizationApprovedEvent>
{
    private readonly IFinancialOperationsService _gl;
    public AuthorizationApprovedGlHandler(IFinancialOperationsService gl) => _gl = gl;

    public Task HandleAsync(AuthorizationApprovedEvent evt, CancellationToken cancellationToken = default)
        => _gl.PostAuthorizationGlAsync(evt.AuthorizedAmount, evt.FeeAmount, evt.CurrencyCode, evt.Rrn, evt.CorrelationId, cancellationToken);
}

/// <summary>
/// Handler that triggers GL journal posting when a wallet top-up completes.
/// </summary>
public sealed class WalletTopUpGlHandler : IEventHandler<WalletTopUpCompletedEvent>
{
    private readonly IFinancialOperationsService _gl;
    public WalletTopUpGlHandler(IFinancialOperationsService gl) => _gl = gl;

    public Task HandleAsync(WalletTopUpCompletedEvent evt, CancellationToken cancellationToken = default)
        => _gl.PostTopUpGlAsync(evt.LoadAmount, evt.FeeAmount, evt.CurrencyCode, evt.Reference, evt.CorrelationId, cancellationToken);
}
