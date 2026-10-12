using BeyondNetCode.Shell.Factory.Impl;
using Evolith.Ums.Domain.Enums;

namespace Evolith.Ums.Infrastructure.Approvals.NotificationRule;

internal sealed class NotificationRecipientStrategyFactorySetup : AbstractFactorySetupSource
{
    public NotificationRecipientStrategyFactorySetup()
    {
        For<NotificationRecipientStrategyCriteria, INotificationRecipientStrategy>()
            .Create<EmailNotificationRecipientStrategy>()
            .When(criteria => string.Equals(criteria.Channel, NotificationChannel.Email.Name, StringComparison.OrdinalIgnoreCase));

        For<NotificationRecipientStrategyCriteria, INotificationRecipientStrategy>()
            .Create<SmsNotificationRecipientStrategy>()
            .When(criteria => string.Equals(criteria.Channel, NotificationChannel.Sms.Name, StringComparison.OrdinalIgnoreCase));

        For<NotificationRecipientStrategyCriteria, INotificationRecipientStrategy>()
            .Create<InAppNotificationRecipientStrategy>()
            .When(criteria => string.Equals(criteria.Channel, NotificationChannel.InApp.Name, StringComparison.OrdinalIgnoreCase));
    }
}
