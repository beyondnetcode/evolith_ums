namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class IdpConfigurationId : IdValueObject
{
    private IdpConfigurationId(Guid value) : base(value) { }
    public static new IdpConfigurationId Create() => new(Guid.NewGuid());
    public static new IdpConfigurationId Load(Guid value) => new(value);
    public static new IdpConfigurationId Load(string value) => new(Guid.Parse(value));
}
