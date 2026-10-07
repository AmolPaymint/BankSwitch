using BankSwitch.Admin.Hubs;
using BankSwitch.Application;
using Microsoft.AspNetCore.SignalR;

namespace BankSwitch.Admin.Services;

/// <summary>
/// Publishes bounded, role-safe operational updates to the Command Center.
/// A failed downstream probe never terminates the hosted service; the next
/// polling interval retries and clients can fall back to REST refresh.
/// </summary>
public sealed class CommandCenterRealtimeBroadcaster : BackgroundService
{
    private readonly IHubContext<CommandCenterHub> _hub;
    private readonly IMonitoringService _monitoring;
    private readonly IOperationsCommandCenterService _operations;
    private readonly IAlertingService _alerts;
    private readonly ILogger<CommandCenterRealtimeBroadcaster> _logger;
    private readonly TimeSpan _interval;

    public CommandCenterRealtimeBroadcaster(
        IHubContext<CommandCenterHub> hub,
        IMonitoringService monitoring,
        IOperationsCommandCenterService operations,
        IAlertingService alerts,
        IConfiguration configuration,
        ILogger<CommandCenterRealtimeBroadcaster> logger)
    {
        _hub = hub;
        _monitoring = monitoring;
        _operations = operations;
        _alerts = alerts;
        _logger = logger;
        var seconds = Math.Clamp(configuration.GetValue("CommandCenter:RealtimeIntervalSeconds", 5), 2, 60);
        _interval = TimeSpan.FromSeconds(seconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Command Center realtime broadcast cycle failed; REST fallback remains available.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                break;
        }
    }

    private async Task PublishAsync(CancellationToken ct)
    {
        var metricsTask = _monitoring.GetRealTimeMetricsAsync(ct);
        var healthTask = _monitoring.GetApplicationHealthAsync(ct);
        var devicesTask = _monitoring.GetDeviceHealthAsync(ct);
        var dashboardTask = _operations.GetDashboardAsync(ct);
        await Task.WhenAll(metricsTask, healthTask, devicesTask, dashboardTask).ConfigureAwait(false);

        await _hub.Clients.All.SendAsync("overview", new
        {
            generatedAt = DateTimeOffset.UtcNow,
            metrics = await metricsTask.ConfigureAwait(false),
            health = await healthTask.ConfigureAwait(false),
            devices = await devicesTask.ConfigureAwait(false),
            operations = await dashboardTask.ConfigureAwait(false)
        }, ct).ConfigureAwait(false);

        var opsHealthTask = _operations.GetHealthAsync(ct);
        var incidentTask = _operations.GetIncidentsAsync(null, ct);
        await Task.WhenAll(opsHealthTask, incidentTask).ConfigureAwait(false);
        await _hub.Clients.Group("operations").SendAsync("operations", new
        {
            generatedAt = DateTimeOffset.UtcNow,
            dashboard = await dashboardTask.ConfigureAwait(false),
            health = await opsHealthTask.ConfigureAwait(false),
            incidents = await incidentTask.ConfigureAwait(false)
        }, ct).ConfigureAwait(false);

        var alertEvents = await _alerts.GetAlertEventsAsync(
            new AlertEventFilter(null, null, null, null, 25), ct).ConfigureAwait(false);
        await _hub.Clients.Group("operations").SendAsync("alerts", alertEvents, ct).ConfigureAwait(false);
    }
}
