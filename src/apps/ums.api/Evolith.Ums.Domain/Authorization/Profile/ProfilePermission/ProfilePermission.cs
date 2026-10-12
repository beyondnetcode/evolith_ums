namespace Evolith.Ums.Domain.Authorization.Profile.ProfilePermission;

public sealed class ProfilePermission : Entity<ProfilePermission, ProfilePermissionProps>
{
    private ProfilePermission(ProfilePermissionProps props) : base(props)
    {
    }

    public ProfileId ProfileId => Props.ProfileId;
    public TemplateId TemplateId => Props.TemplateId;
    public ExclusiveArcTarget TargetType => Props.TargetType;
    public IdValueObject TargetId => Props.TargetId;
    public ActionId ActionId => Props.ActionId;
    public bool IsAllowed => Props.IsAllowed;
    public bool IsDenied => Props.IsDenied;
    public bool IsActive => Props.IsActive;
    public bool IsOverride => Props.IsOverride;

    public ProfilePermissionId GetId() => ProfilePermissionId.Load(Props.Id.GetValue());

    public static Result<ProfilePermission> Create(
        ProfileId profileId,
        TemplateId templateId,
        ProfilePermissionSettings settings,
        ActorId createdBy)
    {
        var props = new ProfilePermissionProps(
            IdValueObject.Create(),
            profileId,
            templateId,
            settings.TargetType,
            settings.TargetId,
            settings.ActionId,
            settings.IsAllowed,
            settings.IsDenied,
            true,
            false,
            createdBy);

        var permission = new ProfilePermission(props);

        if (!permission.IsValid())
        {
            return Result<ProfilePermission>.Failure(permission.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<ProfilePermission>.Success(permission);
    }

    public Result OverrideAllow(ActorId updatedBy)
    {
        Props.IsAllowed = true;
        Props.IsDenied = false;
        Props.IsOverride = true;
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result OverrideDeny(ActorId updatedBy)
    {
        Props.IsAllowed = false;
        Props.IsDenied = true;
        Props.IsOverride = true;
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result OverrideNeutral(ActorId updatedBy)
    {
        Props.IsAllowed = false;
        Props.IsDenied = false;
        Props.IsOverride = true;
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Deactivate(ActorId updatedBy)
    {
        Props.IsActive = false;
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Activate(ActorId updatedBy)
    {
        Props.IsActive = true;
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }
}
