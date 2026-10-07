using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Engine;

public sealed class SwitchWorker : BackgroundService
{
    private readonly TransactionProcessor _processor;
    private readonly ILogger<SwitchWorker> _logger;

    public SwitchWorker(TransactionProcessor processor, ILogger<SwitchWorker> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BankSwitch engine service started. ISO source gateway and auto-reversal workers are active.");
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
        }
    }

    public Task<IsoMessage> ProcessForGatewayAsync(IsoMessage message, string sourceNodeId, CancellationToken cancellationToken) =>
        _processor.ProcessAsync(message, sourceNodeId, cancellationToken);
}
