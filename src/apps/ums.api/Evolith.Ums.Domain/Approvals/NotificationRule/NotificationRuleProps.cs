namespace Evolith.Ums.Domain.Approvals.NotificationRule;

public class NotificationRuleProps : IProps
{
    public IdValueObject Id { get; set; }
    public TenantId TenantId { get; set; }
    public NotificationChannel Channel { get; set; }
    public TextValueObject Recipient { get; set; }
    public bool IsActive { get; set; }
    public AuditValueObject Audit { get; private set; }

    public NotificationRuleProps(
        IdValueObject id,
        TenantId tenantId,
        NotificationChannel channel,
        TextValueObject recipient,
        bool isActive,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        Channel = channel;
        Recipient = recipient;
        IsActive = isActive;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public object Clone() => MemberwiseClone();
}
