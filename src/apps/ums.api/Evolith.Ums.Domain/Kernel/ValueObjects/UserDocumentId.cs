namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class UserDocumentId : IdValueObject
{
    private UserDocumentId(Guid value) : base(value) { }
    public static new UserDocumentId Create() => new UserDocumentId(Guid.NewGuid());
    public static new UserDocumentId Load(Guid value) => new UserDocumentId(value);
    public static new UserDocumentId Load(string value) => new UserDocumentId(Guid.Parse(value));
}
