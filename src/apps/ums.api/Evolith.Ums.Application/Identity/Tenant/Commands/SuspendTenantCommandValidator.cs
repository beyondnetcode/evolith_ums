namespace Evolith.Ums.Application.Identity.Tenant.Commands;

using FluentValidation;

public sealed class SuspendTenantCommandValidator : AbstractValidator<SuspendTenantCommand>
{
    public SuspendTenantCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();
    }
}
