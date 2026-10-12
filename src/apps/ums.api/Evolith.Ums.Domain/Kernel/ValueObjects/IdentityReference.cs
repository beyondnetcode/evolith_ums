namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class IdentityReference : StringValueObject
{
    private IdentityReference(string value) : base(value) { }

    public static IdentityReference Create(string value) => new IdentityReference(value?.Trim() ?? string.Empty);

    public static IdentityReference Empty() => new IdentityReference(string.Empty);

    public bool IsEmpty() => string.IsNullOrWhiteSpace(GetValue());
}
