namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed class UpdateAppSettingCommandValidator : AbstractValidator<UpdateAppSettingCommand>
{
    public UpdateAppSettingCommandValidator()
    {
        RuleFor(x => x.SystemSuiteId).NotEmpty();
        RuleFor(x => x.Key).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NewValue).NotEmpty();
    }
}
