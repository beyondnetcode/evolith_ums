namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class DocumentTypeId : IdValueObject
{
    private DocumentTypeId(Guid value) : base(value) { }
    public static new DocumentTypeId Create() => new DocumentTypeId(Guid.NewGuid());
    public static new DocumentTypeId Load(Guid value) => new DocumentTypeId(value);
    public static new DocumentTypeId Load(string value) => new DocumentTypeId(Guid.Parse(value));
}
