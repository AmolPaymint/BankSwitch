using BankSwitch.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

/// <summary>
/// Daily reconciliation worker. Runs every N minutes (default 30) and triggers
/// a full four-way reconciliation after the configured run time (default 23:00 UTC,
/// after clearing cut-off). This replaces the previous LogReconciliation() stub
/// with real transaction/ledger/GL/clearing matching.
/// </summary>
public sealed class ReconciliationBackgroundWorker : BackgroundService
{
    private readonly IReconciliationEngine _engine;
    private readonly ReconciliationOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<ReconciliationBackgroundWorker> _logger;

    public ReconciliationBackgroundWorker(IReconciliationEngine engine, ReconciliationOptions options, IClock clock, ILogger<ReconciliationBackgroundWorker> logger)
    {
        _engine = engine;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reconciliation worker started (interval: {Interval}min, run time: {RunTime} UTC).",
            _options.WorkerIntervalMinutes, _options.RunTime);

        DateOnly? lastProcessedDate = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_options.WorkerIntervalMinutes), stoppingToken).ConfigureAwait(false);

                var now = _clock.UtcNow;
                var businessDate = DateOnly.FromDateTime(now.UtcDateTime);
                var todayRunTime = now.Date + _options.RunTime;

                if (now.UtcDateTime >= todayRunTime && lastProcessedDate != businessDate)
                {
                    _logger.LogInformation("Reconciliation: starting daily run for {Date}.", businessDate);
                    var result = await _engine.ReconcileAsync(businessDate, "Scheduled", stoppingToken).ConfigureAwait(false);
                    lastProcessedDate = businessDate;

                    _logger.LogInformation(
                        "Reconciliation complete for {Date}: switch={Switch} matched={Matched} breaks={Breaks} status={Status}.",
                        businessDate, result.TransactionLogCount, result.MatchedCount, result.BreakCount, result.Status);

                    if (result.BreakCount > 0)
                        _logger.LogWarning("Reconciliation BREAKS FOUND: {Count} break(s) for {Date}. Review via Admin portal at /api/reconciliation/runs.", result.BreakCount, businessDate);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reconciliation background worker error.");
            }
        }
    }
}

/// <summary>
/// Monitors chargeback cases for missed deadlines.
/// Runs daily and flags cases whose representment / pre-arbitration / arbitration
/// deadline has passed without the required response being submitted.
/// </summary>
public sealed class ChargebackDeadlineMonitor : BackgroundService
{
    private readonly IChargebackService _service;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ILogger<ChargebackDeadlineMonitor> _logger;

    public ChargebackDeadlineMonitor(IChargebackService service, IAuditLogger audit, IClock clock, ILogger<ChargebackDeadlineMonitor> logger)
    {
        _service = service;
        _audit = audit;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Chargeback deadline monitor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(12), stoppingToken).ConfigureAwait(false);

                var overdue = await _service.GetOverdueCasesAsync(stoppingToken).ConfigureAwait(false);
                if (overdue.Count > 0)
                {
                    _logger.LogWarning("Chargeback deadline monitor: {Count} overdue case(s). Review immediately.", overdue.Count);
                    foreach (var c in overdue)
                    {
                        _audit.LogSystem(c.CaseReference, $"CHARGEBACK DEADLINE MISSED: case={c.CaseReference} stage={c.Stage} network={c.Network} reprDeadline={c.RepresentmentDeadline}");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chargeback deadline monitor error.");
            }
        }
    }
}
