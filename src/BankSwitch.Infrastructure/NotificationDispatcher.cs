using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

public sealed class InProcessNotificationDispatcher : INotificationDispatcher
{
    private readonly IOperationalControlRepository _repository;
    private readonly ILogger<InProcessNotificationDispatcher> _logger;

    public InProcessNotificationDispatcher(IOperationalControlRepository repository, ILogger<InProcessNotificationDispatcher> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task DispatchPendingAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        var pending = await _repository.GetPendingNotificationsAsync(take, cancellationToken).ConfigureAwait(false);
        foreach (var message in pending)
        {
            try
            {
                _logger.LogInformation("Notification queued for dispatch. Id={NotificationId} Channel={Channel} Recipient={Recipient} Template={Template}", message.Id, message.Channel, message.Recipient, message.TemplateCode);
               // await _repository.UpdateNotificationAsync(message with { Status = NotificationStatus.Sent, SentAt = DateTimeOffset.UtcNow, Attempts = message.Attempts + 1 }, cancellationToken).ConfigureAwait(false);
                await _repository.UpdateNotificationAsync(message with { Status = Domain.NotificationStatus.Sent, SentAt = DateTimeOffset.UtcNow, Attempts = message.Attempts + 1 }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification dispatch failed. Id={NotificationId}", message.Id);
               // await _repository.UpdateNotificationAsync(message with { Status = NotificationStatus.Failed, Attempts = message.Attempts + 1 }, cancellationToken).ConfigureAwait(false);
                await _repository.UpdateNotificationAsync(message with { Status = Domain.NotificationStatus.Failed, Attempts = message.Attempts + 1 }, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
