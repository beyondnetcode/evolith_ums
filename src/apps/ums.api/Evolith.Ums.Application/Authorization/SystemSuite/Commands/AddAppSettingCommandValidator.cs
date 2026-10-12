namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed class AddAppSettingCommandValidator : AbstractValidator<AddAppSettingCommand>
{
    public AddAppSettingCommandValidator()
    {
        RuleFor(x => x.SystemSuiteId).NotEmpty();
        RuleFor(x => x.Key).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Value).NotEmpty();
        RuleFor(x => x.Scope).NotEmpty().MaximumLength(50);
    }
}
