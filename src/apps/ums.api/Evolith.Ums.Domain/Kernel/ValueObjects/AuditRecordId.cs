namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class AuditRecordId : IdValueObject
{
    private AuditRecordId(Guid value) : base(value) { }
    public static new AuditRecordId Create() => new(Guid.NewGuid());
    public static new AuditRecordId Load(Guid value) => new(value);
    public static new AuditRecordId Load(string value) => new(Guid.Parse(value));
}
