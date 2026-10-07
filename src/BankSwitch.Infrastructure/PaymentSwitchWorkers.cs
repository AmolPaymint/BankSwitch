using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

/// <summary>
/// Background worker that drives the post-crash transaction recovery service.
/// Runs on startup and then every <see cref="StandInOptions.RecoveryWorkerIntervalSeconds"/>
/// (default 60s), scanning for transactions stuck in the <c>ForwardedToSink</c> state.
/// For each stuck transaction, it sends a conservative 0420 reversal to the sink.
/// </summary>
public sealed class TransactionRecoveryWorker : BackgroundService
{
    private readonly ITransactionRecoveryService _recovery;
    private readonly StandInOptions _options;
    private readonly ILogger<TransactionRecoveryWorker> _logger;

    public TransactionRecoveryWorker(
        ITransactionRecoveryService recovery,
        StandInOptions options,
        ILogger<TransactionRecoveryWorker> logger)
    {
        _recovery = recovery;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Transaction recovery worker started (interval: {IntervalSeconds}s, stuck threshold: {ThresholdSeconds}s).",
            _options.RecoveryWorkerIntervalSeconds, _options.StuckTransactionThresholdSeconds);

        // Run immediately on startup to catch transactions stuck from a previous crash
        await RunRecoveryAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.RecoveryWorkerIntervalSeconds), stoppingToken).ConfigureAwait(false);
            await RunRecoveryAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunRecoveryAsync(CancellationToken stoppingToken)
    {
        try
        {
            var result = await _recovery.RecoverStuckTransactionsAsync(stoppingToken).ConfigureAwait(false);
            if (result.ScannedCount > 0)
                _logger.LogInformation("Recovery scan: scanned={Scanned} reversed={Reversed} failed={Failed}.",
                    result.ScannedCount, result.ReversedCount, result.FailedCount);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction recovery worker error.");
        }
    }
}

/// <summary>
/// Background worker that sweeps expired pre-authorization holds.
/// Pre-auth holds (ISO 0100) that have not been completed (0220) or voided (0420)
/// within their expiry window are marked as <see cref="PreAuthStatus.Expired"/>.
/// In a full implementation this would also send a 0420 to the sink and release
/// the CMS wallet reserve — here we record the expiry for audit purposes.
/// </summary>
public sealed class PreAuthExpiryWorker : BackgroundService
{
    private readonly IPreAuthStore _preAuthStore;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ILogger<PreAuthExpiryWorker> _logger;

    public PreAuthExpiryWorker(
        IPreAuthStore preAuthStore,
        IAuditLogger audit,
        IClock clock,
        ILogger<PreAuthExpiryWorker> logger)
    {
        _preAuthStore = preAuthStore;
        _audit = audit;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pre-auth expiry worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
            try
            {
                var now = _clock.UtcNow;
                var expired = await _preAuthStore.GetExpiredAsync(now, stoppingToken).ConfigureAwait(false);
                foreach (var hold in expired)
                {
                    var updated = hold with { Status = PreAuthStatus.Expired };
                    await _preAuthStore.UpdateAsync(updated, stoppingToken).ConfigureAwait(false);
                    _audit.LogSystem(hold.OriginalCorrelationId, $"Pre-auth hold expired: RRN={hold.Rrn} amount={hold.AuthorizedAmount} expiredAt={hold.ExpiresAt:O}");
                }
                if (expired.Count > 0)
                    _logger.LogInformation("Pre-auth expiry: expired {Count} hold(s).", expired.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pre-auth expiry worker error.");
            }
        }
    }
}
