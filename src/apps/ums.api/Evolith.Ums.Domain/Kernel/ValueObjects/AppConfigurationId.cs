namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class AppConfigurationId : IdValueObject
{
    private AppConfigurationId(Guid value) : base(value) { }
    public static new AppConfigurationId Create() => new(Guid.NewGuid());
    public static new AppConfigurationId Load(Guid value) => new(value);
    public static new AppConfigurationId Load(string value) => new(Guid.Parse(value));
}
