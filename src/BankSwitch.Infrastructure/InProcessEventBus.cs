using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

/// <summary>
/// In-process event bus that resolves all registered <see cref="IEventHandler{T}"/>
/// implementations from the DI container and awaits each one sequentially.
///
/// Why sequential and not parallel?
/// The primary handlers (GL posting, audit writes) must not race with each other
/// or with the caller. Sequential execution gives predictable ordering and ensures
/// that failures in one handler surface immediately rather than being swallowed
/// in a parallel fire-and-forget.
///
/// Failures are caught per-handler and logged; a failing handler does not prevent
/// subsequent handlers from running, but the error is logged at Warning level so
/// the caller is aware.
/// </summary>
public sealed class InProcessEventBus : IEventBus
{
    private readonly IServiceProvider _services;
    private readonly ILogger<InProcessEventBus> _logger;

    public InProcessEventBus(IServiceProvider services, ILogger<InProcessEventBus> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent
    {
        // Resolve all handlers for this specific event type from DI
        var handlers = _services.GetServices<IEventHandler<T>>().ToList();

        if (handlers.Count == 0)
        {
            _logger.LogTrace("EventBus: no handlers registered for {EventType}.", typeof(T).Name);
            return;
        }

        _logger.LogTrace("EventBus: publishing {EventType} (id={EventId}) to {Count} handler(s).",
            typeof(T).Name, domainEvent.EventId, handlers.Count);

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(domainEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // propagate shutdown cancellation
            }
            catch (Exception ex)
            {
                // A failing handler is logged but does not abort the remaining handlers.
                // This follows the outbox/retry pattern: a background worker can re-publish
                // missed events in a future enhancement.
                _logger.LogWarning(ex,
                    "EventBus: handler {Handler} failed for {EventType} (id={EventId}). Other handlers will still run.",
                    handler.GetType().Name, typeof(T).Name, domainEvent.EventId);
            }
        }
    }
}

/// <summary>
/// No-op event bus for unit tests that don't need event handler side-effects.
/// Inject this instead of the real bus when testing service logic in isolation.
/// </summary>
public sealed class NullEventBus : IEventBus
{
    public static readonly NullEventBus Instance = new();
    public Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent
        => Task.CompletedTask;
}
