namespace Evolith.Ums.Domain.Enums;

public class UserCategory : DomainEnumeration
{
    public static readonly UserCategory Internal = new(1, nameof(Internal));
    public static readonly UserCategory External = new(2, nameof(External));
    public static readonly UserCategory B2B = new(3, nameof(B2B));
    public static readonly UserCategory Partner = new(4, nameof(Partner));
    public static readonly UserCategory ServiceAccount = new(5, nameof(ServiceAccount));

    private UserCategory(int id, string name) : base(id, name) { }
}
