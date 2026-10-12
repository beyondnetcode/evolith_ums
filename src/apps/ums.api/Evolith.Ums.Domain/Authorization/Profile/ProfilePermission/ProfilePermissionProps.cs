namespace Evolith.Ums.Domain.Authorization.Profile.ProfilePermission;

public class ProfilePermissionProps : IProps
{
    public IdValueObject Id { get; set; }
    public ProfileId ProfileId { get; set; }
    public TemplateId TemplateId { get; set; }
    public ExclusiveArcTarget TargetType { get; set; }
    public IdValueObject TargetId { get; set; }
    public ActionId ActionId { get; set; }
    public bool IsAllowed { get; set; }
    public bool IsDenied { get; set; }
    public bool IsActive { get; set; }
    public bool IsOverride { get; set; }
    public AuditValueObject Audit { get; private set; }

    public ProfilePermissionProps(
        IdValueObject id,
        ProfileId profileId,
        TemplateId templateId,
        ExclusiveArcTarget targetType,
        IdValueObject targetId,
        ActionId actionId,
        bool isAllowed,
        bool isDenied,
        bool isActive,
        bool isOverride,
        ActorId createdBy)
    {
        Id = id;
        ProfileId = profileId;
        TemplateId = templateId;
        TargetType = targetType;
        TargetId = targetId;
        ActionId = actionId;
        IsAllowed = isAllowed;
        IsDenied = isDenied;
        IsActive = isActive;
        IsOverride = isOverride;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public object Clone() => MemberwiseClone();
}
