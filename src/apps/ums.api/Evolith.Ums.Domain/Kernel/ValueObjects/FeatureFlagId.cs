namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class FeatureFlagId : IdValueObject
{
    private FeatureFlagId(Guid value) : base(value) { }
    public static new FeatureFlagId Create() => new(Guid.NewGuid());
    public static new FeatureFlagId Load(Guid value) => new(value);
    public static new FeatureFlagId Load(string value) => new(Guid.Parse(value));
}
