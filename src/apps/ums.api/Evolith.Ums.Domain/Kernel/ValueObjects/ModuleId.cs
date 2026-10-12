namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class ModuleId : IdValueObject
{
    private ModuleId(Guid value) : base(value) { }
    public static new ModuleId Create() => new ModuleId(Guid.NewGuid());
    public static new ModuleId Load(Guid value) => new ModuleId(value);
    public static new ModuleId Load(string value) => new ModuleId(Guid.Parse(value));
}
