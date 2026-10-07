using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

public sealed class EnterpriseHeartbeatHostedService : BackgroundService
{
    private readonly IEnterpriseProductionService _enterprise;
    private readonly EnterpriseProductionOptions _options;
    private readonly ILogger<EnterpriseHeartbeatHostedService> _logger;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    public EnterpriseHeartbeatHostedService(IEnterpriseProductionService enterprise, EnterpriseProductionOptions options, ILogger<EnterpriseHeartbeatHostedService> logger)
    {
        _enterprise = enterprise;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _enterprise.RegisterHeartbeatAsync(new RegisterHeartbeatRequest(
                    Environment.GetEnvironmentVariable("BANKSWITCH_NODE_NAME") ?? Environment.MachineName,
                    Environment.GetEnvironmentVariable("BANKSWITCH_INSTANCE_ID") ?? _instanceId,
                    Enum.TryParse<ClusterNodeRole>(Environment.GetEnvironmentVariable("BANKSWITCH_NODE_ROLE"), true, out var role) ? role : ClusterNodeRole.Active,
                    ClusterNodeHealthStatus.Healthy,
                    Environment.GetEnvironmentVariable("BANKSWITCH_REGION") ?? string.Empty,
                    Environment.GetEnvironmentVariable("BANKSWITCH_AZ") ?? string.Empty,
                    0,
                    0m,
                    0m), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish enterprise cluster heartbeat.");
            }
            await Task.Delay(_options.HeartbeatInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}

public sealed class EnterpriseSiemDispatcherHostedService : BackgroundService
{
    private readonly IEnterpriseProductionService _enterprise;
    private readonly EnterpriseProductionOptions _options;
    private readonly ILogger<EnterpriseSiemDispatcherHostedService> _logger;

    public EnterpriseSiemDispatcherHostedService(IEnterpriseProductionService enterprise, EnterpriseProductionOptions options, ILogger<EnterpriseSiemDispatcherHostedService> logger)
    {
        _enterprise = enterprise;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delivered = await _enterprise.DispatchPendingSiemEventsAsync(100, stoppingToken).ConfigureAwait(false);
                if (delivered.Count > 0) _logger.LogInformation("Dispatched {Count} SIEM security events.", delivered.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch SIEM events.");
            }
            await Task.Delay(_options.SiemWorkerInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}

public sealed class EnterpriseWarehouseExportHostedService : BackgroundService
{
    private readonly IEnterpriseProductionService _enterprise;
    private readonly EnterpriseProductionOptions _options;
    private readonly ILogger<EnterpriseWarehouseExportHostedService> _logger;

    public EnterpriseWarehouseExportHostedService(IEnterpriseProductionService enterprise, EnterpriseProductionOptions options, ILogger<EnterpriseWarehouseExportHostedService> logger)
    {
        _enterprise = enterprise;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await _enterprise.ProcessDueWarehouseExportJobsAsync(10, stoppingToken).ConfigureAwait(false);
                if (processed > 0) _logger.LogInformation("Processed {Count} data warehouse export jobs.", processed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process data warehouse export jobs.");
            }
            await Task.Delay(_options.WarehouseWorkerInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
