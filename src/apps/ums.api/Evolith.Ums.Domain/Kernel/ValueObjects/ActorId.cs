namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class ActorId : StringValueObject
{
    private ActorId(string value) : base(value) { }
    public static ActorId Create(string value) => new ActorId(value?.Trim() ?? string.Empty);
    public static ActorId Default() => new ActorId(string.Empty);
    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new GenericStringValidator(this, nameof(ActorId)));
    }
}
