namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class PasswordHash : StringValueObject
{
    private PasswordHash(string value) : base(value) { }

    public static PasswordHash Create(string value) => new PasswordHash(value?.Trim() ?? string.Empty);

    public static PasswordHash Empty() => new PasswordHash(string.Empty);

    public bool IsEmpty() => string.IsNullOrWhiteSpace(GetValue());
}
