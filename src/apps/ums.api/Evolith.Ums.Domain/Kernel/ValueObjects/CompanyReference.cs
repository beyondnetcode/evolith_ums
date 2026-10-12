namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class CompanyReference : StringValueObject
{
    private CompanyReference(string value) : base(value) { }
    public static CompanyReference Create(string value) => new CompanyReference(value?.Trim() ?? string.Empty);
    public static CompanyReference Default() => new CompanyReference(string.Empty);
    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new GenericStringValidator(this, nameof(CompanyReference)));
    }
}
