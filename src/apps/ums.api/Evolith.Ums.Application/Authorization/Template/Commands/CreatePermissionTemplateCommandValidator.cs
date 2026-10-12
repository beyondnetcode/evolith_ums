namespace Evolith.Ums.Application.Authorization.Template.Commands;

using FluentValidation;

public sealed class CreatePermissionTemplateCommandValidator : AbstractValidator<CreatePermissionTemplateCommand>
{
    public CreatePermissionTemplateCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.SystemSuiteId).NotEmpty();
    }
}
