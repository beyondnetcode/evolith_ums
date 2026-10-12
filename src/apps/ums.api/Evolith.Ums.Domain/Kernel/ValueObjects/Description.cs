namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class Description : StringValueObject
{
    private Description(string value) : base(value) { }
    public static Description Create(string value) => new Description(value?.Trim() ?? string.Empty);
    public static Description Default() => new Description(string.Empty);
    public override void AddValidators()
    {
        base.AddValidators();
        // Description is inherently optional at the domain level.
        // Use-case validators (FluentValidation) enforce NotEmpty() where required.
        ValidatorRules.Add(new GenericStringValidator(this, nameof(Description), isRequired: false));
    }
}
