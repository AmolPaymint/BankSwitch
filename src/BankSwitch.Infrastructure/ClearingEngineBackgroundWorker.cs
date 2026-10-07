using BankSwitch.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

/// <summary>
/// Nightly background worker that drives the clearing engine.
/// Wakes every N minutes (default 15) and checks whether the daily cut-off time has passed.
/// When it has, generates clearing batches for the business date and persists them ready for transmission.
/// Marking batches as transmitted (and recording network acknowledgements) is done via the Admin API.
/// </summary>
public sealed class ClearingEngineBackgroundWorker : BackgroundService
{
    private readonly IClearingEngineService _clearingEngine;
    private readonly ClearingEngineOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<ClearingEngineBackgroundWorker> _logger;

    public ClearingEngineBackgroundWorker(
        IClearingEngineService clearingEngine,
        ClearingEngineOptions options,
        IClock clock,
        ILogger<ClearingEngineBackgroundWorker> logger)
    {
        _clearingEngine = clearingEngine;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Clearing engine worker started. Cut-off: {CutOff} UTC, interval: {Interval} min.",
            _options.CutOffTime, _options.WorkerIntervalMinutes);

        DateOnly? lastProcessedDate = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_options.WorkerIntervalMinutes), stoppingToken).ConfigureAwait(false);

                var now = _clock.UtcNow;
                var businessDate = DateOnly.FromDateTime(now.DateTime);
                var todayCutOff = now.Date + _options.CutOffTime;

                if (now.DateTime >= todayCutOff && lastProcessedDate != businessDate)
                {
                    _logger.LogInformation("Clearing cut-off passed for {Date}. Generating clearing batches.", businessDate);
                    var result = await _clearingEngine.GenerateClearingBatchesAsync(businessDate, stoppingToken).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        _logger.LogInformation("Clearing: {Count} batch(es) generated for {Date}.", result.Value!.Count, businessDate);
                        lastProcessedDate = businessDate;
                    }
                    else
                    {
                        _logger.LogError("Clearing batch generation failed for {Date}: {Message}", businessDate, result.Message);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Clearing engine worker error.");
            }
        }

        _logger.LogInformation("Clearing engine worker stopped.");
    }
}
