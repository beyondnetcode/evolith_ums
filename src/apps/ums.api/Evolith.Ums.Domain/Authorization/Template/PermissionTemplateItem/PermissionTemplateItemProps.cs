namespace Evolith.Ums.Domain.Authorization.Template.PermissionTemplateItem;

public class PermissionTemplateItemProps : IProps
{
    public IdValueObject Id { get; set; }
    public TemplateId TemplateId { get; set; }
    public ExclusiveArcTarget TargetType { get; set; }
    public IdValueObject TargetId { get; set; }
    public ActionId ActionId { get; set; }
    public bool IsAllowed { get; set; }
    public bool IsDenied { get; set; }
    public bool IsActive { get; set; }
    public AuditValueObject Audit { get; private set; }

    public PermissionTemplateItemProps(
        IdValueObject id,
        TemplateId templateId,
        ExclusiveArcTarget targetType,
        IdValueObject targetId,
        ActionId actionId,
        bool isAllowed,
        bool isDenied,
        bool isActive,
        ActorId createdBy)
    {
        Id = id;
        TemplateId = templateId;
        TargetType = targetType;
        TargetId = targetId;
        ActionId = actionId;
        IsAllowed = isAllowed;
        IsDenied = isDenied;
        IsActive = isActive;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public object Clone() => MemberwiseClone();
}
