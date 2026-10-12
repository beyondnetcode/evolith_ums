namespace Evolith.Ums.Domain.Enums;

public class LogoFormat : DomainEnumeration
{
    public static readonly LogoFormat Png = new(1, nameof(Png));
    public static readonly LogoFormat Svg = new(2, nameof(Svg));

    private LogoFormat(int id, string name) : base(id, name) { }
}
