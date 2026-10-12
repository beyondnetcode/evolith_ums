namespace Evolith.Ums.Domain.Enums;

public class DocumentCriticity : DomainEnumeration
{
    public static readonly DocumentCriticity Low = new(1, nameof(Low));
    public static readonly DocumentCriticity Medium = new(2, nameof(Medium));
    public static readonly DocumentCriticity High = new(3, nameof(High));
    public static readonly DocumentCriticity Critical = new(4, nameof(Critical));

    private DocumentCriticity(int id, string name) : base(id, name) { }
}
