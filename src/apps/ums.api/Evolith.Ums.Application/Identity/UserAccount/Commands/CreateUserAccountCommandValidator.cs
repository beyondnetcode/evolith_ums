namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

using Evolith.Ums.Domain.Enums;
using FluentValidation;

public sealed class CreateUserAccountCommandValidator : AbstractValidator<CreateUserAccountCommand>
{
    public CreateUserAccountCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(150)
            .EmailAddress();

        RuleFor(command => command.Category)
            .NotEmpty()
            .Must(category => DomainEnumerationParser.FromName<UserCategory>(category) is not null)
            .WithMessage("User category is not supported.");

        RuleFor(command => command.DisplayName)
            .MaximumLength(SetUserAccountDisplayNameCommandValidator.LongitudMaxima)
            .When(command => !string.IsNullOrWhiteSpace(command.DisplayName));

        RuleFor(command => command.IdentityReference)
            .MaximumLength(150)
            .When(command => !string.IsNullOrWhiteSpace(command.IdentityReference));

        RuleFor(command => command.IdentityReferenceType)
            .Must(type => string.IsNullOrWhiteSpace(type) || DomainEnumerationParser.FromName<IdentityReferenceType>(type) is not null)
            .WithMessage("Identity reference type is not supported.");
    }
}
