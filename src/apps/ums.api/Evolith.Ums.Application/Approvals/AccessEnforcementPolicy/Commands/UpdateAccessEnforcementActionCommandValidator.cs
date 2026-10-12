namespace Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Commands;
public sealed class UpdateAccessEnforcementActionCommandValidator : AbstractValidator<UpdateAccessEnforcementActionCommand>
{
    private static readonly string[] ValidActions = ["BlockUser", "RestrictProfile", "LogOnly"];
    public UpdateAccessEnforcementActionCommandValidator()
    {
        RuleFor(x => x.PolicyId).NotEmpty();
        RuleFor(x => x.NewAction)
            .NotEmpty()
            .Must(v => ValidActions.Contains(v, StringComparer.OrdinalIgnoreCase))
            .WithMessage("NewAction must be one of: BlockUser, RestrictProfile, LogOnly.");
    }
}
