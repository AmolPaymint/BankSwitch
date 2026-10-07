using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

/// <summary>
/// ISO 8583 TPS load test harness for switch certification.
///
/// Architecture:
///   - N worker tasks (one per virtual terminal) each sustain targetTps/N transactions/second.
///   - Each worker opens a persistent TCP connection, sends ISO 8583 purchase requests,
///     and records end-to-end latency (send → response field 39).
///   - Latency samples are collected lock-free in a ConcurrentBag, then post-processed
///     for percentile computation using a sorted-array approach.
///   - A rate-limiter per worker uses Stopwatch-based busy-wait to achieve precise TPS.
///
/// The harness uses the existing Iso8583AsciiBitmapFormatter so the generated messages
/// are processed by the real switch pipeline (MAC validation, routing, CMS, etc.).
/// </summary>
public sealed class TpsLoadTestHarness : ITpsLoadTestHarness
{
    private readonly Iso8583AsciiBitmapFormatter _formatter;
    private readonly ILogger<TpsLoadTestHarness> _logger;

    public TpsLoadTestHarness(Iso8583AsciiBitmapFormatter formatter, ILogger<TpsLoadTestHarness> logger)
    {
        _formatter = formatter;
        _logger = logger;
    }

    public async Task<TpsLoadTestResult> RunAsync(
        string host,
        int port,
        string sourceNodeId,
        int targetTps,
        int durationSeconds,
        int concurrency,
        TpsCertificationTarget target,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "TPS load test starting: host={Host}:{Port} targetTps={Tps} duration={Dur}s concurrency={Conc}",
            host, port, targetTps, durationSeconds, concurrency);

        var startedAt = DateTimeOffset.UtcNow;
        var latencies = new ConcurrentBag<double>();
        var counters = new LoadCounters();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(durationSeconds + 5)); // hard deadline

        var tpsPerWorker = Math.Max(1, targetTps / concurrency);
        var intervalMicros = 1_000_000L / tpsPerWorker; // microseconds between requests

        var workers = Enumerable.Range(0, concurrency).Select(idx => Task.Run(async () =>
        {
            await RunWorkerAsync(host, port, sourceNodeId, idx, tpsPerWorker, durationSeconds,
                intervalMicros, latencies, counters, cts.Token).ConfigureAwait(false);
        }, cts.Token)).ToArray();

        await Task.WhenAll(workers).ConfigureAwait(false);

        var completedAt = DateTimeOffset.UtcNow;
        var elapsedSeconds = (completedAt - startedAt).TotalSeconds;
        var allLatencies = latencies.ToArray();
        Array.Sort(allLatencies);

        var errors = Volatile.Read(ref counters.Errors);
        var timeouts = Volatile.Read(ref counters.Timeouts);
        var successes = Volatile.Read(ref counters.Successes);
        var total = allLatencies.Length + errors + timeouts;
        var errorRate = total == 0 ? 0 : (errors + timeouts) * 100.0 / total;
        var actualTps = total == 0 ? 0 : total / elapsedSeconds;

        var result = BuildResult(host, port, targetTps, durationSeconds, allLatencies,
            successes, errors, timeouts, actualTps, errorRate, target, startedAt, completedAt);

        _logger.LogInformation(
            "TPS load test complete: actual={ActualTps:F1}tps p50={P50:F0}ms p95={P95:F0}ms p99={P99:F0}ms errors={Errors}%",
            result.ActualTps, result.P50Ms, result.P95Ms, result.P99Ms, result.ErrorRatePercent);

        return result;
    }

    public Task<TpsLoadTestResult> RunCertificationAsync(
        string host, int port, string sourceNodeId,
        TpsCertificationTarget target, CancellationToken cancellationToken = default)
        => RunAsync(host, port, sourceNodeId,
            target.MinimumTps, 60, Math.Max(1, target.MinimumTps / 100),
            target, cancellationToken);

    // ---------------------------------------------------------------
    // Worker loop
    // ---------------------------------------------------------------

    private async Task RunWorkerAsync(
        string host, int port, string sourceNodeId, int workerIdx,
        int tpsPerWorker, int durationSeconds, long intervalMicros,
        ConcurrentBag<double> latencies,
        LoadCounters counters,
        CancellationToken cancellationToken)
    {
        var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * durationSeconds;
        long stan = (long)workerIdx * 1_000_000;

        try
        {
            using var tcp = new TcpClient { NoDelay = true, SendTimeout = 5000, ReceiveTimeout = 5000 };
            await tcp.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            await using var stream = tcp.GetStream();

            while (Stopwatch.GetTimestamp() < deadline && !cancellationToken.IsCancellationRequested)
            {
                var requestStart = Stopwatch.GetTimestamp();
                stan++;

                try
                {
                    var msg = BuildPurchaseMessage(sourceNodeId, stan);
                    var bytes = _formatter.Format(msg);
                    var header = new byte[] { (byte)(bytes.Length >> 8), (byte)(bytes.Length & 0xFF) };

                    await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
                    await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);

                    // Read response header (2 bytes length)
                    var respHeader = new byte[2];
                    await stream.ReadExactlyAsync(respHeader, cancellationToken).ConfigureAwait(false);
                    var respLen = (respHeader[0] << 8) | respHeader[1];
                    var respBytes = new byte[respLen];
                    await stream.ReadExactlyAsync(respBytes, cancellationToken).ConfigureAwait(false);

                    var elapsed = (double)(Stopwatch.GetTimestamp() - requestStart) / Stopwatch.Frequency * 1000;
                    latencies.Add(elapsed);
                    Interlocked.Increment(ref counters.Successes);
                }
                catch (OperationCanceledException) { break; }
                catch (TimeoutException) { Interlocked.Increment(ref counters.Timeouts); }
                catch { Interlocked.Increment(ref counters.Errors); }

                // Rate limiting: busy-wait for the remaining interval
                var elapsed2 = Stopwatch.GetTimestamp() - requestStart;
                var targetTicks = intervalMicros * Stopwatch.Frequency / 1_000_000;
                while (Stopwatch.GetTimestamp() - requestStart < targetTicks && !cancellationToken.IsCancellationRequested)
                    Thread.SpinWait(10);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogDebug("Worker {Idx} connection failed: {Error}", workerIdx, ex.Message);
            Interlocked.Add(ref counters.Errors, tpsPerWorker); // count all remaining as errors
        }
    }


    private sealed class LoadCounters
    {
        public int Errors;
        public int Timeouts;
        public int Successes;
    }

    private IsoMessage BuildPurchaseMessage(string sourceNodeId, long stan)
    {
        var msg = new IsoMessage("0200");
        msg.CorrelationId = Guid.NewGuid().ToString("N");
        msg.SetField(2, "5399838383838381");              // Test PAN (Luhn valid)
        msg.SetField(3, "000000");                        // Processing code: purchase
        msg.SetField(4, "000000001000");                  // Amount: 10.00 in minor units
        msg.SetField(11, (stan % 1_000_000).ToString("D6")); // STAN
        msg.SetField(12, DateTime.UtcNow.ToString("HHmmss"));
        msg.SetField(13, DateTime.UtcNow.ToString("MMdd"));
        msg.SetField(22, "051");                          // POS entry mode: chip + PIN
        msg.SetField(37, stan.ToString("D12"));           // RRN
        msg.SetField(41, sourceNodeId[..Math.Min(16, sourceNodeId.Length)].PadRight(16));
        msg.SetField(42, "LOADTEST001    ");              // Merchant ID
        msg.SetField(49, "566");                          // Currency: NGN
        msg.SetField(123, "00100010000000");              // POS data code
        return msg;
    }

    // ---------------------------------------------------------------
    // Percentile computation and result building
    // ---------------------------------------------------------------

    private static TpsLoadTestResult BuildResult(
        string host, int port, int targetTps, int durationSeconds,
        double[] sortedLatencies, int successes, int errors, int timeouts,
        double actualTps, double errorRate,
        TpsCertificationTarget target,
        DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        var n = sortedLatencies.Length;
        var p50 = n > 0 ? sortedLatencies[(int)(n * 0.50)] : 0;
        var p95 = n > 0 ? sortedLatencies[(int)(n * 0.95)] : 0;
        var p99 = n > 0 ? sortedLatencies[(int)(n * 0.99)] : 0;
        var p999 = n > 0 ? sortedLatencies[(int)(n * 0.999)] : 0;
        var mean = n > 0 ? sortedLatencies.Average() : 0;
        var min = n > 0 ? sortedLatencies[0] : 0;
        var max = n > 0 ? sortedLatencies[^1] : 0;

        // Build histogram buckets
        var bucketBounds = new[] { 10.0, 20.0, 50.0, 100.0, 200.0, 500.0, 1000.0, 2000.0, double.MaxValue };
        var bucketLabels = new[] { "<10ms", "<20ms", "<50ms", "<100ms", "<200ms", "<500ms", "<1s", "<2s", "≥2s" };
        var buckets = bucketBounds.Select((upper, i) =>
        {
            var count = sortedLatencies.Count(l => (i == 0 || l >= bucketBounds[i - 1]) && l < upper);
            return new LatencyBucket(bucketLabels[i], upper == double.MaxValue ? 9999 : upper, count,
                n > 0 ? count * 100.0 / n : 0);
        }).ToList();

        var total = successes + errors + timeouts;
        var successRate = total > 0 ? successes * 100.0 / total : 0;

        // Certification evaluation
        var passes =
            actualTps >= target.MinimumTps &&
            p95 <= target.MaxP95Ms &&
            p99 <= target.MaxP99Ms &&
            errorRate <= target.MaxErrorRatePercent &&
            successRate >= target.MinSuccessRatePercent;

        var summary = passes
            ? $"✅ CERTIFIED: {actualTps:F0}tps ≥ {target.MinimumTps}tps | P95={p95:F0}ms ≤ {target.MaxP95Ms}ms | P99={p99:F0}ms ≤ {target.MaxP99Ms}ms | errors={errorRate:F3}% ≤ {target.MaxErrorRatePercent}%"
            : $"❌ NOT CERTIFIED: {(actualTps < target.MinimumTps ? $"TPS {actualTps:F0}<{target.MinimumTps} " : string.Empty)}" +
              $"{(p95 > target.MaxP95Ms ? $"P95 {p95:F0}>{target.MaxP95Ms}ms " : string.Empty)}" +
              $"{(p99 > target.MaxP99Ms ? $"P99 {p99:F0}>{target.MaxP99Ms}ms " : string.Empty)}" +
              $"{(errorRate > target.MaxErrorRatePercent ? $"errors {errorRate:F3}%>{target.MaxErrorRatePercent}% " : string.Empty)}";

        return new TpsLoadTestResult
        {
            TargetHost = host,
            TargetPort = port,
            TargetTps = targetTps,
            DurationSeconds = durationSeconds,
            TotalRequests = total,
            SuccessfulRequests = successes,
            FailedRequests = errors,
            TimeoutRequests = timeouts,
            ActualTps = actualTps,
            ErrorRatePercent = errorRate,
            P50Ms = p50, P95Ms = p95, P99Ms = p99, P999Ms = p999,
            MinMs = min, MaxMs = max, MeanMs = mean,
            Histogram = buckets,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            PassesCertification = passes,
            CertificationSummary = summary
        };
    }
}
