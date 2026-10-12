namespace Evolith.Ums.Application.Authorization.Template.Commands;

public sealed class SetTemplateItemEffectCommandValidator : AbstractValidator<SetTemplateItemEffectCommand>
{
    private static readonly string[] ValidEffects = ["Allow", "Deny", "Neutral"];

    public SetTemplateItemEffectCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Effect)
            .NotEmpty()
            .Must(v => ValidEffects.Contains(v, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Effect must be one of: Allow, Deny, Neutral.");
    }
}
