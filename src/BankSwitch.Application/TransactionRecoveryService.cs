using BankSwitch.Domain;

namespace BankSwitch.Application;

/// <summary>
/// Durable post-crash / unknown-outcome recovery service.
///
/// v44.7 closes the historical recovery gap by consuming a recovery snapshot that is
/// committed before the request is written to the upstream socket. The snapshot carries an
/// encrypted pre-built 0420 reversal payload and the exact sink node id. Therefore recovery
/// does not depend on TransactionLog having been written after a response is received.
/// </summary>
public sealed class TransactionRecoveryService : ITransactionRecoveryService
{
    private readonly ITransactionRecoverySnapshotRepository _snapshots;
    private readonly ITransactionStateMachine _stateMachine;
    private readonly INodeRepository _nodes;
    private readonly ISinkClient _sinkClient;
    private readonly ISensitiveDataProtector _protector;
    private readonly Iso8583AsciiBitmapFormatter _formatter;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly StandInOptions _options;
    private readonly ReversalOptions _reversalOptions;

    public TransactionRecoveryService(
        ITransactionRecoverySnapshotRepository snapshots,
        ITransactionStateMachine stateMachine,
        INodeRepository nodes,
        ISinkClient sinkClient,
        ISensitiveDataProtector protector,
        Iso8583AsciiBitmapFormatter formatter,
        IAuditLogger audit,
        IClock clock,
        StandInOptions options,
        ReversalOptions reversalOptions)
    {
        _snapshots = snapshots;
        _stateMachine = stateMachine;
        _nodes = nodes;
        _sinkClient = sinkClient;
        _protector = protector;
        _formatter = formatter;
        _audit = audit;
        _clock = clock;
        _options = options;
        _reversalOptions = reversalOptions;
    }

    public async Task<RecoveryRunResult> RecoverStuckTransactionsAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var cutoff = now.AddSeconds(-_options.StuckTransactionThresholdSeconds);
        var due = await _snapshots.GetDueAsync(cutoff, now, _reversalOptions.MaxAttempts, maxRows: 100, cancellationToken).ConfigureAwait(false);
        var reversed = new List<string>();
        var failed = 0;

        _audit.LogSystem("RECOVERY", $"Durable transaction recovery scan: due={due.Count}, cutoff={cutoff:O}.");

        foreach (var snapshot in due)
        {
            try
            {
                var outcome = await AttemptRecoveryReversalAsync(snapshot, cancellationToken).ConfigureAwait(false);
                if (outcome) reversed.Add(snapshot.CorrelationId); else failed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                await ScheduleOrFailAsync(snapshot, $"{ex.GetType().Name}: {ex.Message}", cancellationToken).ConfigureAwait(false);
                _audit.LogSystem(snapshot.CorrelationId, "Durable recovery reversal failed.", ex);
            }
        }

        return new RecoveryRunResult(due.Count, reversed.Count, failed, _clock.UtcNow, reversed);
    }

    private async Task<bool> AttemptRecoveryReversalAsync(TransactionRecoverySnapshot snapshot, CancellationToken cancellationToken)
    {
        var sink = await _nodes.GetSinkNodeAsync(snapshot.SinkNodeId, cancellationToken).ConfigureAwait(false);
        if (sink is null || !sink.IsActive)
        {
            await ScheduleOrFailAsync(snapshot, $"Recovery sink {snapshot.SinkNodeId} is unavailable.", cancellationToken).ConfigureAwait(false);
            return false;
        }

        string protectedBase64;
        try
        {
            protectedBase64 = _protector.Unprotect(snapshot.ProtectedReversalPayload, "TXN-RECOVERY-REVERSAL");
        }
        catch (Exception ex)
        {
            await _snapshots.MarkFailedAsync(snapshot.CorrelationId, "Recovery payload could not be decrypted.", cancellationToken).ConfigureAwait(false);
            await _stateMachine.TransitionAsync(snapshot.CorrelationId, snapshot.Stan, snapshot.SourceNodeId, TransactionLifecycleState.Failed,
                "Recovery payload decryption failed", 0, cancellationToken).ConfigureAwait(false);
            _audit.LogSecurity(snapshot.CorrelationId, "RecoveryPayloadDecryptFailed", ex.Message);
            return false;
        }

        var payloadBytes = Convert.FromBase64String(protectedBase64);
        var reversal = _formatter.Parse(payloadBytes);
        reversal.CorrelationId = $"REC-{snapshot.CorrelationId}-{snapshot.AttemptCount + 1}";

        var response = await _sinkClient.SendAsync(reversal, sink, cancellationToken).ConfigureAwait(false);
        response.TryGetField(39, out var responseCode);
        responseCode ??= string.Empty;

        if (responseCode is "00" or "08" or "10")
        {
            await _snapshots.MarkReversedAsync(snapshot.CorrelationId, responseCode, cancellationToken).ConfigureAwait(false);
            await _stateMachine.TransitionAsync(snapshot.CorrelationId, snapshot.Stan, snapshot.SourceNodeId,
                TransactionLifecycleState.Reversed, $"Durable recovery reversal accepted by sink={sink.NodeId}, response={responseCode}", 0, cancellationToken).ConfigureAwait(false);
            _audit.LogSystem(snapshot.CorrelationId, $"Recovery reversal accepted. sink={sink.NodeId} response={responseCode}");
            return true;
        }

        var reason = string.IsNullOrWhiteSpace(responseCode)
            ? "Recovery reversal returned no response code."
            : $"Recovery reversal rejected/failed with response code {responseCode}.";
        await ScheduleOrFailAsync(snapshot, reason, cancellationToken).ConfigureAwait(false);
        return false;
    }

    private async Task ScheduleOrFailAsync(TransactionRecoverySnapshot snapshot, string reason, CancellationToken cancellationToken)
    {
        var nextAttempt = snapshot.AttemptCount + 1;
        if (nextAttempt >= _reversalOptions.MaxAttempts)
        {
            await _snapshots.MarkFailedAsync(snapshot.CorrelationId, reason, cancellationToken).ConfigureAwait(false);
            await _stateMachine.TransitionAsync(snapshot.CorrelationId, snapshot.Stan, snapshot.SourceNodeId,
                TransactionLifecycleState.Failed, $"Recovery exhausted after {nextAttempt} attempts: {reason}", 0, cancellationToken).ConfigureAwait(false);
            _audit.LogSystem(snapshot.CorrelationId, $"ALERT: durable recovery exhausted after {nextAttempt} attempts. {reason}");
            return;
        }

        var next = _clock.UtcNow.Add(_reversalOptions.GetBackoff(nextAttempt));
        await _snapshots.ScheduleRetryAsync(snapshot.CorrelationId, reason, next, cancellationToken).ConfigureAwait(false);
        _audit.LogSystem(snapshot.CorrelationId, $"Recovery reversal scheduled retry #{nextAttempt + 1} at {next:O}. Reason={reason}");
    }
}
