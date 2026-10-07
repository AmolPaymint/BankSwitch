using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class ReversalService
{
    private readonly IReversalRepository _reversals;
    private readonly INodeRepository _nodes;
    private readonly ISinkClient _sinkClient;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ReversalOptions _options;

    public ReversalService(
        IReversalRepository reversals,
        INodeRepository nodes,
        ISinkClient sinkClient,
        IAuditLogger audit,
        IClock clock,
        ReversalOptions options)
    {
        _reversals = reversals;
        _nodes = nodes;
        _sinkClient = sinkClient;
        _audit = audit;
        _clock = clock;
        _options = options;
    }

    public async Task<IsoMessage> ProcessManualReversalAsync(IsoMessage request, CancellationToken cancellationToken = default)
    {
        if (!IsoFieldHelper.TryGetString(request, 90, out var originalDataElement))
        {
            return ResponseBuilder.Decline(request, "12");
        }

        var workItem = await _reversals.TryStartReversalAsync(originalDataElement, request.CorrelationId, cancellationToken).ConfigureAwait(false);
        if (workItem is null)
        {
            return ResponseBuilder.Decline(request, "25");
        }

        return await SendReversalAsync(workItem, cancellationToken).ConfigureAwait(false);
    }

    public async Task ProcessDueAutoReversalsAsync(CancellationToken cancellationToken = default)
    {
        var due = await _reversals.GetDueReversalsAsync(_clock.UtcNow, _options.MaxAttempts, cancellationToken).ConfigureAwait(false);
        foreach (var item in due)
        {
            try
            {
                await SendReversalAsync(item, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _audit.LogSystem(item.CorrelationId, $"Auto-reversal worker error for {item.OriginalTransactionId}", ex);
                await ScheduleOrFailAsync(item, ex.Message, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<IsoMessage> SendReversalAsync(ReversalWorkItem item, CancellationToken cancellationToken)
    {
        var sink = await _nodes.GetSinkNodeAsync(item.SinkNodeId, cancellationToken).ConfigureAwait(false);
        if (sink is null || !sink.IsActive)
        {
            await ScheduleOrFailAsync(item, "Sink node unavailable for reversal.", cancellationToken).ConfigureAwait(false);
            return ResponseBuilder.Decline(item.ReversalMessage, "91");
        }

        var response = await _sinkClient.SendAsync(item.ReversalMessage, sink, cancellationToken).ConfigureAwait(false);
        var responseCode = response.TryGetField(39, out var rc) ? rc : string.Empty;
        if (responseCode == "00")
        {
            await _reversals.MarkAcceptedAsync(item, responseCode, cancellationToken).ConfigureAwait(false);
            _audit.LogSystem(item.CorrelationId, $"Reversal accepted for original {item.OriginalTransactionId}");
            return response;
        }

        if (!string.IsNullOrWhiteSpace(responseCode))
        {
            await _reversals.MarkRejectedAsync(item, responseCode, cancellationToken).ConfigureAwait(false);
            _audit.LogSystem(item.CorrelationId, $"Reversal rejected for original {item.OriginalTransactionId}; response={responseCode}");
            return response;
        }

        await ScheduleOrFailAsync(item, "No reversal response code returned.", cancellationToken).ConfigureAwait(false);
        return ResponseBuilder.Decline(item.ReversalMessage, "68");
    }

    private async Task ScheduleOrFailAsync(ReversalWorkItem item, string reason, CancellationToken cancellationToken)
    {
        if (item.AttemptCount >= _options.MaxAttempts)
        {
            await _reversals.MarkFailedAsync(item, reason, cancellationToken).ConfigureAwait(false);
            _audit.LogSystem(item.CorrelationId, $"ALERT: Reversal failed after maximum attempts for {item.OriginalTransactionId}. Reason={reason}");
            return;
        }

        var next = _clock.UtcNow.Add(_options.GetBackoff(item.AttemptCount + 1));
        await _reversals.ScheduleRetryAsync(item, reason, next, cancellationToken).ConfigureAwait(false);
    }
}

public sealed record ReversalOptions
{
    public int MaxAttempts { get; init; } = 5;
    public TimeSpan WorkerInterval { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan GetBackoff(int nextAttemptNumber) => nextAttemptNumber switch
    {
        <= 1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(15),
        _ => TimeSpan.FromHours(1)
    };
}
