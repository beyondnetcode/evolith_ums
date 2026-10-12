namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class TemplateId : IdValueObject
{
    private TemplateId(Guid value) : base(value) { }
    public static new TemplateId Create() => new TemplateId(Guid.NewGuid());
    public static new TemplateId Load(Guid value) => new TemplateId(value);
    public static new TemplateId Load(string value) => new TemplateId(Guid.Parse(value));
}
