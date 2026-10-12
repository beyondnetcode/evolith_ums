namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class TemplateAssignmentRuleId : IdValueObject
{
    private TemplateAssignmentRuleId(Guid value) : base(value) { }
    public static new TemplateAssignmentRuleId Create() => new(Guid.NewGuid());
    public static new TemplateAssignmentRuleId Load(Guid value) => new(value);
    public static new TemplateAssignmentRuleId Load(string value) => new(Guid.Parse(value));
}
