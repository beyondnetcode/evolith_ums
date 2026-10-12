namespace Evolith.Ums.Domain.Enums;

public class IdpConfigStatus : DomainEnumeration
{
    public static readonly IdpConfigStatus Draft    = new(1, nameof(Draft));
    public static readonly IdpConfigStatus Active   = new(2, nameof(Active));
    public static readonly IdpConfigStatus Inactive = new(3, nameof(Inactive));
    public static readonly IdpConfigStatus Archived = new(4, nameof(Archived));

    private IdpConfigStatus(int id, string name) : base(id, name) { }
}
