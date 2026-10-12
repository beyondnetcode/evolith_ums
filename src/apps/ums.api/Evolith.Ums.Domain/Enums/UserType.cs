namespace Evolith.Ums.Domain.Enums;

public class UserType : DomainEnumeration
{
    public static readonly UserType Regular = new(1, nameof(Regular));
    public static readonly UserType InternalAdmin = new(2, nameof(InternalAdmin));

    private UserType(int id, string name) : base(id, name) { }
}