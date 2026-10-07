using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class TransactionStateMachine : ITransactionStateMachine
{
    private readonly ITransactionStateRepository _repository;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;

    public TransactionStateMachine(ITransactionStateRepository repository, IClock clock, IAuditLogger audit)
    {
        _repository = repository;
        _clock = clock;
        _audit = audit;
    }

    public async Task TransitionAsync(
        string correlationId,
        string stan,
        string sourceNodeId,
        TransactionLifecycleState newState,
        string reason,
        long latencyFromReceivedMs = 0,
        CancellationToken cancellationToken = default)
    {
        var previousState = await GetCurrentStateAsync(correlationId, cancellationToken).ConfigureAwait(false)
            ?? TransactionLifecycleState.Received;

        var record = new TransactionStateRecord
        {
            CorrelationId = correlationId,
            Stan = stan,
            SourceNodeId = sourceNodeId,
            PreviousState = previousState,
            NewState = newState,
            Reason = reason,
            LatencyFromReceivedMs = latencyFromReceivedMs,
            OccurredAt = _clock.UtcNow
        };

        await _repository.RecordTransitionAsync(record, cancellationToken).ConfigureAwait(false);

        // Audit terminal/warning states for alerting pipelines
        if (newState is TransactionLifecycleState.TimedOut
            or TransactionLifecycleState.Failed
            or TransactionLifecycleState.Reversed)
        {
            _audit.LogSystem(correlationId, $"Transaction state machine: {previousState}→{newState}. STAN={stan} Source={sourceNodeId}. Reason: {reason}");
        }
    }

    public async Task<TransactionLifecycleState?> GetCurrentStateAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        var latest = await _repository.GetLatestStateAsync(correlationId, cancellationToken).ConfigureAwait(false);
        return latest?.NewState;
    }
}
