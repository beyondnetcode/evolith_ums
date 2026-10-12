namespace Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;

public sealed class IdentityProvider : Entity<IdentityProvider, IdentityProviderProps>
{
    private IdentityProvider(IdentityProviderProps props) : base(props)
    {
    }

    public TenantId TenantId => Props.TenantId;
    public Code Code => Props.Code;
    public Name Name => Props.Name;
    public Description Description => Props.Description;
    public IdpStrategy Strategy => Props.Strategy;
    public bool IsActive => Props.IsActive;

    public IdentityProviderId GetId() => IdentityProviderId.Load(Props.Id.GetValue());

    public static Result<IdentityProvider> Create(
        TenantId tenantId,
        Code code,
        Name name,
        Description description,
        IdpStrategy strategy,
        ActorId createdBy)
    {
        var props = new IdentityProviderProps(IdValueObject.Create(), tenantId, code, name, description, strategy, createdBy);
        var identityProvider = new IdentityProvider(props);

        if (!identityProvider.IsValid())
        {
            return Result<IdentityProvider>.Failure(identityProvider.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<IdentityProvider>.Success(identityProvider);
    }

    internal Result CanActivate()
    {
        if (IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Tenant.IdpAlreadyActive));
        }

        return IsValid()
            ? Result.Success()
            : Result.Failure(BrokenRules.GetBrokenRulesAsString());
    }

    internal Result CanDeactivate()
    {
        if (!IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Tenant.IdpAlreadyInactive));
        }

        return IsValid()
            ? Result.Success()
            : Result.Failure(BrokenRules.GetBrokenRulesAsString());
    }

    internal void ActivateInternal()
    {
        Props.IsActive = true;
    }

    internal void DeactivateInternal()
    {
        Props.IsActive = false;
    }
}
