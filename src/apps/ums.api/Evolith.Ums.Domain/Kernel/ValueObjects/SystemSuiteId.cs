namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class SystemSuiteId : IdValueObject
{
    private SystemSuiteId(Guid value) : base(value) { }
    public static new SystemSuiteId Create() => new SystemSuiteId(Guid.NewGuid());
    public static new SystemSuiteId Load(Guid value) => new SystemSuiteId(value);
    public static new SystemSuiteId Load(string value) => new SystemSuiteId(Guid.Parse(value));
}
