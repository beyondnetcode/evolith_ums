namespace Evolith.Ums.Domain.Enums;

public class ProfileScope : DomainEnumeration
{
    public static readonly ProfileScope OrgWide = new(1, nameof(OrgWide));
    public static readonly ProfileScope BranchScoped = new(2, nameof(BranchScoped));

    private ProfileScope(int id, string name) : base(id, name) { }
}
