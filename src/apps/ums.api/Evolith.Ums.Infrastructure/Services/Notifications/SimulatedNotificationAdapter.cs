using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;
using Microsoft.Extensions.Logging;

namespace Evolith.Ums.Infrastructure.Services.Notifications;

/// <summary>
/// Simulated notification adapter for development. Logs messages to the console
/// instead of dispatching to a real channel. Replace with SmtpNotificationAdapter,
/// SendGridNotificationAdapter, etc. by swapping the DI registration.
/// </summary>
public sealed class SimulatedNotificationAdapter : INotificationService
{
    private readonly ILogger<SimulatedNotificationAdapter> _logger;

    public SimulatedNotificationAdapter(ILogger<SimulatedNotificationAdapter> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(UmsNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[SIMULATED NOTIFICATION] Channel={Channel} | To={Recipient} | Subject={Subject}\n{Body}",
            notification.Channel,
            notification.Recipient,
            notification.Subject,
            notification.Body);

        return Task.CompletedTask;
    }

    public async Task SendBulkAsync(IEnumerable<UmsNotification> notifications, CancellationToken cancellationToken = default)
    {
        foreach (var notification in notifications)
            await SendAsync(notification, cancellationToken);
    }
}
