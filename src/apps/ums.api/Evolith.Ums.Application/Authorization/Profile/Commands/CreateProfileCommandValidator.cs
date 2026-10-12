namespace Evolith.Ums.Application.Authorization.Profile.Commands;

using FluentValidation;

public sealed class CreateProfileCommandValidator : AbstractValidator<CreateProfileCommand>
{
    public CreateProfileCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.UserId)
            .NotEmpty();

        RuleFor(command => command.RoleId)
            .NotEmpty();
    }
}
