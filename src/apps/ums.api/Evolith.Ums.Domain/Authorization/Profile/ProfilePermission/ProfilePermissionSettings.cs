namespace Evolith.Ums.Domain.Authorization.Profile.ProfilePermission;

public class ProfilePermissionSettings
{
    public ExclusiveArcTarget TargetType { get; }
    public IdValueObject TargetId { get; }
    public ActionId ActionId { get; }
    public bool IsAllowed { get; }
    public bool IsDenied { get; }

    private ProfilePermissionSettings(
        ExclusiveArcTarget targetType,
        IdValueObject targetId,
        ActionId actionId,
        bool isAllowed,
        bool isDenied)
    {
        TargetType = targetType;
        TargetId = targetId;
        ActionId = actionId;
        IsAllowed = isAllowed;
        IsDenied = isDenied;
    }

    public static ProfilePermissionSettings Create(
        ExclusiveArcTarget targetType,
        IdValueObject targetId,
        ActionId actionId,
        bool isAllowed,
        bool isDenied)
    {
        return new ProfilePermissionSettings(targetType, targetId, actionId, isAllowed, isDenied);
    }
}
