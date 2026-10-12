namespace Evolith.Ums.Domain.Enums;

public class BackgroundStyle : DomainEnumeration
{
    public static readonly BackgroundStyle Gradient = new(1, nameof(Gradient));
    public static readonly BackgroundStyle SolidColor = new(2, nameof(SolidColor));
    public static readonly BackgroundStyle Image = new(3, nameof(Image));

    private BackgroundStyle(int id, string name) : base(id, name) { }
}
