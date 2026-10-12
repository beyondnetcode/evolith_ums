namespace Evolith.Ums.Domain.Approvals.AccessEnforcementPolicy;

public sealed class AccessEnforcementPolicy : AggregateRoot<AccessEnforcementPolicy, AccessEnforcementPolicyProps>
{
    private AccessEnforcementPolicy(AccessEnforcementPolicyProps props) : base(props)
    {
    }

    public TenantId TenantId => Props.TenantId;
    public ProfileId? ProfileId => Props.ProfileId;
    public RoleId? RoleId => Props.RoleId;
    public AccessEnforcementAction EnforcementAction => Props.EnforcementAction;
    public bool IsActive => Props.IsActive;
    public int GracePeriodDays => Props.GracePeriodDays;

    public AccessEnforcementPolicyId GetId() => AccessEnforcementPolicyId.Load(Props.Id.GetValue());

    public static Result<AccessEnforcementPolicy> Create(
        TenantId tenantId,
        ProfileId? profileId,
        RoleId? roleId,
        AccessEnforcementAction enforcementAction,
        ActorId createdBy,
        int gracePeriodDays = 0)
    {
        if (profileId is null && roleId is null)
        {
            return Result<AccessEnforcementPolicy>.Failure(DomainErrors.Approvals.PolicyRequiresProfileOrRole);
        }

        // G-120: el periodo de gracia no puede ser negativo (0 = enforcement inmediato).
        if (gracePeriodDays < 0)
        {
            return Result<AccessEnforcementPolicy>.Failure(DomainErrors.Approvals.GracePeriodInvalid);
        }

        var props = new AccessEnforcementPolicyProps(IdValueObject.Create(), tenantId, profileId, roleId, enforcementAction, true, gracePeriodDays, createdBy);
        var policy = new AccessEnforcementPolicy(props);

        if (!policy.IsValid())
        {
            return Result<AccessEnforcementPolicy>.Failure(policy.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<AccessEnforcementPolicy>.Success(policy);
    }

    public Result Deactivate(ActorId updatedBy)
    {
        if (!IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Approvals.PolicyAlreadyInactive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.IsActive = false;
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result UpdateAction(AccessEnforcementAction newAction, ActorId updatedBy)
    {
        // Irreversibilidad (G-051): Deactivate es una transicion terminal — el agregado no
        // expone reactivacion — por lo que una politica desactivada queda congelada. Mutar
        // su accion de enforcement sobre un estado terminal es una transicion invalida:
        // se rechaza fail-closed, coherente con el patron de estados terminales de los
        // agregados de Configuration (FlagArchivedCannotChange, AppConfigAlreadyArchived).
        if (!IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Approvals.PolicyInactiveCannotUpdate));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.EnforcementAction = newAction;
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }
}
