namespace Evolith.Ums.Domain.Approvals.UserDocument.AccessNotification;

public sealed class AccessNotification : Entity<AccessNotification, AccessNotificationProps>
{
    private AccessNotification(AccessNotificationProps props) : base(props) { }

    public int Step => Props.Step;
    public NotificationChannel Channel => Props.Channel;
    public int DaysRemaining => Props.DaysRemaining;
    public DateTime SentAt => Props.SentAt;

    public static AccessNotification Record(int step, NotificationChannel channel, int daysRemaining)
    {
        var props = new AccessNotificationProps(IdValueObject.Create(), step, channel, daysRemaining);
        return new AccessNotification(props);
    }
}
