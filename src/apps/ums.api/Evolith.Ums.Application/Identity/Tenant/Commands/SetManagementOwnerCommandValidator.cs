namespace Evolith.Ums.Application.Identity.Tenant.Commands;

using FluentValidation;

public sealed class SetManagementOwnerCommandValidator : AbstractValidator<SetManagementOwnerCommand>
{
    public SetManagementOwnerCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
    }
}
