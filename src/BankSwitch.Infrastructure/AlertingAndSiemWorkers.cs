using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

/// <summary>
/// Alerting background worker. Runs on a configurable tick (default 30s),
/// records the current queue depth into the metric ring buffer, then
/// calls <see cref="IAlertingService.EvaluateAndFireAsync"/> to check all
/// active alert rules against recent metric samples.
/// </summary>
public sealed class AlertingBackgroundWorker : BackgroundService
{
    private readonly IAlertingService _alerting;
    private readonly IMetricCollector _metrics;
    private readonly ITransactionQueue _queue;
    private readonly BankSwitchInstrumentation _instrumentation;
    private readonly AlertingOptions _options;
    private readonly ILogger<AlertingBackgroundWorker> _logger;

    public AlertingBackgroundWorker(
        IAlertingService alerting,
        IMetricCollector metrics,
        ITransactionQueue queue,
        BankSwitchInstrumentation instrumentation,
        AlertingOptions options,
        ILogger<AlertingBackgroundWorker> logger)
    {
        _alerting = alerting;
        _metrics = metrics;
        _queue = queue;
        _instrumentation = instrumentation;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Alerting background worker started (interval: {IntervalSeconds}s).", _options.EvaluationIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.EvaluationIntervalSeconds), stoppingToken).ConfigureAwait(false);

                // Record queue depth into ring buffer for the alerting evaluator
                var queueDepth = _queue.ApproximateCount;
                _metrics.Record("switch", MetricNames.QueueDepth, queueDepth);
                _instrumentation.UpdateQueueDepth(queueDepth);

                // Evaluate all active rules
                var fired = await _alerting.EvaluateAndFireAsync(stoppingToken).ConfigureAwait(false);
                if (fired.Count > 0)
                    _logger.LogWarning("Alerting: {Count} alert(s) fired this tick: {Titles}",
                        fired.Count, string.Join("; ", fired.Select(a => $"[{a.Severity}] {a.Title}")));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Alerting background worker error.");
            }
        }

        _logger.LogInformation("Alerting background worker stopped.");
    }
}

/// <summary>
/// Upgraded SIEM dispatcher: replaces the previous no-op implementation that
/// only marked events "Delivered" in the database.
/// Now actually delivers events to the configured <see cref="ISiemForwarder"/>
/// (Splunk HEC, Sentinel, Webhook, or LogOnly) in batches.
/// </summary>
public sealed class SiemDispatchWorker : BackgroundService
{
    private readonly IEnterpriseProductionService _enterprise;
    private readonly ISiemForwarder _siemForwarder;
    private readonly EnterpriseProductionOptions _options;
    private readonly ILogger<SiemDispatchWorker> _logger;

    public SiemDispatchWorker(
        IEnterpriseProductionService enterprise,
        ISiemForwarder siemForwarder,
        EnterpriseProductionOptions options,
        ILogger<SiemDispatchWorker> logger)
    {
        _enterprise = enterprise;
        _siemForwarder = siemForwarder;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SIEM dispatch worker started (forwarder: {ForwarderName}, interval: {IntervalSeconds}s).",
            _siemForwarder.ForwarderName, _options.SiemWorkerInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pending = await _enterprise.GetPendingSiemEventsAsync(100, stoppingToken).ConfigureAwait(false);
                if (pending.Count > 0)
                {
                    var result = await _siemForwarder.ForwardBatchAsync(pending, stoppingToken).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        // Mark all events as delivered in the enterprise repository
                        await _enterprise.MarkSiemEventsDeliveredAsync(pending.Select(e => e.Id).ToList(), stoppingToken).ConfigureAwait(false);
                        _logger.LogInformation("SIEM: Delivered {Count} events via {Forwarder}.", pending.Count, _siemForwarder.ForwarderName);
                    }
                    else
                    {
                        _logger.LogWarning("SIEM delivery failed via {Forwarder}: HTTP {StatusCode} {Error}",
                            _siemForwarder.ForwarderName, result.HttpStatusCode, result.Error);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SIEM dispatch worker error.");
            }

            await Task.Delay(_options.SiemWorkerInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
