namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class IdentityProviderId : IdValueObject
{
    private IdentityProviderId(Guid value) : base(value) { }
    public static new IdentityProviderId Create() => new IdentityProviderId(Guid.NewGuid());
    public static new IdentityProviderId Load(Guid value) => new IdentityProviderId(value);
    public static new IdentityProviderId Load(string value) => new IdentityProviderId(Guid.Parse(value));
}
