namespace Evolith.Ums.Domain.Enums;

public class FlagType : DomainEnumeration
{
    public static readonly FlagType Boolean    = new(1, nameof(Boolean));
    public static readonly FlagType Variant    = new(2, nameof(Variant));
    public static readonly FlagType Percentage = new(3, nameof(Percentage));

    private FlagType(int id, string name) : base(id, name) { }
}
