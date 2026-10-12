namespace Evolith.Ums.Domain.Approvals.UserDocument.AccessNotification;

public class AccessNotificationProps : IProps
{
    public IdValueObject Id { get; set; }
    public int Step { get; set; }
    public NotificationChannel Channel { get; set; }
    public int DaysRemaining { get; set; }
    public DateTime SentAt { get; set; }

    public AccessNotificationProps(IdValueObject id, int step, NotificationChannel channel, int daysRemaining)
    {
        Id = id;
        Step = step;
        Channel = channel;
        DaysRemaining = daysRemaining;
        SentAt = DateTime.UtcNow;
    }

    public object Clone() => MemberwiseClone();
}
