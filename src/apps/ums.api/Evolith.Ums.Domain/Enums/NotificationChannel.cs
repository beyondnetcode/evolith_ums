namespace Evolith.Ums.Domain.Enums;

public class NotificationChannel : DomainEnumeration
{
    public static readonly NotificationChannel Email = new(1, nameof(Email));
    public static readonly NotificationChannel InApp = new(2, nameof(InApp));
    public static readonly NotificationChannel Sms = new(3, nameof(Sms));

    private NotificationChannel(int id, string name) : base(id, name) { }
}
