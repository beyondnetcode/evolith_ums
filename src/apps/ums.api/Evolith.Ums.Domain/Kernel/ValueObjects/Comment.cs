namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class Comment : StringValueObject
{
    private Comment(string value) : base(value) { }
    public static Comment Create(string value) => new Comment(value?.Trim() ?? string.Empty);
    public static Comment Default() => new Comment(string.Empty);

    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new GenericStringValidator(this, nameof(Comment)));
    }
}
