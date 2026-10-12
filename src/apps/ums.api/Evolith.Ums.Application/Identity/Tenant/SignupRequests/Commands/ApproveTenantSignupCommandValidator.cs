using FluentValidation;

namespace Evolith.Ums.Application.Identity.Tenant.SignupRequests.Commands;

public sealed class ApproveTenantSignupCommandValidator : AbstractValidator<ApproveTenantSignupCommand>
{
    public ApproveTenantSignupCommandValidator()
    {
        RuleFor(x => x.TenantSignupRequestId)
            .NotEmpty();
    }
}
