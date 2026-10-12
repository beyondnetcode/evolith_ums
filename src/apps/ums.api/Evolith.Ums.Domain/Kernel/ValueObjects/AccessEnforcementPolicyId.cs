namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class AccessEnforcementPolicyId : IdValueObject
{
    private AccessEnforcementPolicyId(Guid value) : base(value) { }
    public static new AccessEnforcementPolicyId Create() => new AccessEnforcementPolicyId(Guid.NewGuid());
    public static new AccessEnforcementPolicyId Load(Guid value) => new AccessEnforcementPolicyId(value);
    public static new AccessEnforcementPolicyId Load(string value) => new AccessEnforcementPolicyId(Guid.Parse(value));
}
