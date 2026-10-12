namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class Version : StringValueObject
{
    private Version(string value) : base(value) { }
    public static Version Create(string value) => new Version(value?.Trim() ?? string.Empty);
    public static Version Default() => new Version(string.Empty);
    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new GenericStringValidator(this, nameof(Version)));
    }
}
