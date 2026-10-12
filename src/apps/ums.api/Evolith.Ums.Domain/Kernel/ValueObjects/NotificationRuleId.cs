namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class NotificationRuleId : IdValueObject
{
    private NotificationRuleId(Guid value) : base(value) { }
    public static new NotificationRuleId Create() => new NotificationRuleId(Guid.NewGuid());
    public static new NotificationRuleId Load(Guid value) => new NotificationRuleId(value);
    public static new NotificationRuleId Load(string value) => new NotificationRuleId(Guid.Parse(value));
}
