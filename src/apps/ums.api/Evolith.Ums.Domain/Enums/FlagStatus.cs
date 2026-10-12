namespace Evolith.Ums.Domain.Enums;

public class FlagStatus : DomainEnumeration
{
    public static readonly FlagStatus Active   = new(1, nameof(Active));
    public static readonly FlagStatus Inactive = new(2, nameof(Inactive));
    public static readonly FlagStatus Archived = new(3, nameof(Archived));

    private FlagStatus(int id, string name) : base(id, name) { }
}
