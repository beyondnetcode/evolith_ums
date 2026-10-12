namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class PasswordCredentialId : IdValueObject
{
    private PasswordCredentialId(Guid value) : base(value) { }
    public static new PasswordCredentialId Create() => new PasswordCredentialId(Guid.NewGuid());
    public static new PasswordCredentialId Load(Guid value) => new PasswordCredentialId(value);
    public static new PasswordCredentialId Load(string value) => new PasswordCredentialId(Guid.Parse(value));
}
