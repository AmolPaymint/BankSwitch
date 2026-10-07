using BankSwitch.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

public sealed class AutoReversalBackgroundWorker : BackgroundService
{
    private readonly ReversalService _reversalService;
    private readonly ReversalOptions _options;
    private readonly ILogger<AutoReversalBackgroundWorker> _logger;

    public AutoReversalBackgroundWorker(ReversalService reversalService, ReversalOptions options, ILogger<AutoReversalBackgroundWorker> logger)
    {
        _reversalService = reversalService;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _reversalService.ProcessDueAutoReversalsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-reversal background worker failure.");
            }

            await Task.Delay(_options.WorkerInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}

public sealed class SlaMetrics
{
    private readonly object _sync = new();
    private readonly List<long> _latencies = new(capacity: 2048);
    private long _timeouts;
    private long _declines;
    private long _reversals;
    private readonly Dictionary<string, long> _responseCodes = new(StringComparer.Ordinal);

    public void RecordLatency(long milliseconds)
    {
        lock (_sync)
        {
            _latencies.Add(milliseconds);
            if (_latencies.Count > 100_000) _latencies.RemoveRange(0, 50_000);
        }
    }

    public void RecordResponseCode(string code)
    {
        lock (_sync)
        {
            _responseCodes.TryGetValue(code, out var count);
            _responseCodes[code] = count + 1;
            if (code == "68") _timeouts++;
            if (code != "00") _declines++;
        }
    }

    public void RecordReversal() => Interlocked.Increment(ref _reversals);

    public SlaSnapshot Snapshot()
    {
        lock (_sync)
        {
            var values = _latencies.OrderBy(x => x).ToArray();
            return new SlaSnapshot(
                P50: Percentile(values, 0.50),
                P95: Percentile(values, 0.95),
                P99: Percentile(values, 0.99),
                TimeoutCount: _timeouts,
                DeclineCount: _declines,
                ReversalCount: _reversals,
                ResponseCodeCounts: new Dictionary<string, long>(_responseCodes));
        }
    }

    private static long Percentile(long[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0) return 0;
        var index = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        index = Math.Clamp(index, 0, sortedValues.Length - 1);
        return sortedValues[index];
    }
}

public sealed record SlaSnapshot(long P50, long P95, long P99, long TimeoutCount, long DeclineCount, long ReversalCount, IReadOnlyDictionary<string, long> ResponseCodeCounts);
