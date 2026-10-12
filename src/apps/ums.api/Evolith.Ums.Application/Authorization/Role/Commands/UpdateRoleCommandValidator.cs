namespace Evolith.Ums.Application.Authorization.Role.Commands;

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.Value).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.HierarchyLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PromotionOrder).GreaterThanOrEqualTo(0);
    }
}
