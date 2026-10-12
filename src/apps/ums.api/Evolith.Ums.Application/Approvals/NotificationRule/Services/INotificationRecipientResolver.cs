namespace Evolith.Ums.Application.Approvals.NotificationRule.Services;

public interface INotificationRecipientResolver
{
    Result<string> Normalize(string channel, string recipient);
}
