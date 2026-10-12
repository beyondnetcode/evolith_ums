namespace Evolith.Ums.Application.Authorization.Role.Commands;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.SystemSuiteId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Value).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.HierarchyLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PromotionOrder).GreaterThanOrEqualTo(0);
    }
}
