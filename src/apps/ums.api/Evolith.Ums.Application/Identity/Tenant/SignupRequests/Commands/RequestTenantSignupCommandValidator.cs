using FluentValidation;

namespace Evolith.Ums.Application.Identity.Tenant.SignupRequests.Commands;

public sealed class RequestTenantSignupCommandValidator : AbstractValidator<RequestTenantSignupCommand>
{
    public RequestTenantSignupCommandValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.CompanyReference)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.ContactName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.ContactEmail)
            .NotEmpty()
            .MaximumLength(255)
            .EmailAddress();
    }
}
