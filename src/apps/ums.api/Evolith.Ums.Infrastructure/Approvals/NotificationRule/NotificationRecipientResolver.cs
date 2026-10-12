using Evolith.Ums.Application.Approvals.NotificationRule.Services;
using BeyondNetCode.Shell.Factory.Interfaces;

namespace Evolith.Ums.Infrastructure.Approvals.NotificationRule;

internal sealed class NotificationRecipientResolver : INotificationRecipientResolver
{
    private readonly IFactory _factory;

    public NotificationRecipientResolver(IFactory factory)
    {
        _factory = factory;
    }

    public Result<string> Normalize(string channel, string recipient)
    {
        var strategy = _factory.Create<NotificationRecipientStrategyCriteria, INotificationRecipientStrategy>(
                new NotificationRecipientStrategyCriteria(channel))
            .SingleOrDefault();

        if (strategy is null)
        {
            return Result<string>.Failure($"Notification channel '{channel}' is not supported.");
        }

        return strategy.Normalize(recipient);
    }
}
