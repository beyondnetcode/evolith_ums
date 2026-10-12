namespace Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;

public class IdentityProviderProps : IProps
{
    public IdValueObject Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Code Code { get; private set; }
    public Name Name { get; private set; }
    public Description Description { get; private set; }
    public IdpStrategy Strategy { get; private set; }
    public bool IsActive { get; set; }
    public AuditValueObject Audit { get; private set; }

    public IdentityProviderProps(
        IdValueObject id,
        TenantId tenantId,
        Code code,
        Name name,
        Description description,
        IdpStrategy strategy,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        Name = name;
        Description = description;
        Strategy = strategy;
        IsActive = false;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public object Clone()
    {
        return this.MemberwiseClone();
    }
}
