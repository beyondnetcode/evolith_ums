namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

using Evolith.Ums.Application.Configuration.Services;

public sealed class AddUserAccountPasswordCommandValidator : AbstractValidator<AddUserAccountPasswordCommand>
{
    public AddUserAccountPasswordCommandValidator(IConfigurationProvider configProvider)
    {
        var minLength = configProvider.Global().MinPasswordLength;

        RuleFor(x => x.UserAccountId).NotEmpty();
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña temporal es obligatoria.")
            .MinimumLength(minLength).WithMessage($"La contraseña temporal debe tener al menos {minLength} caracteres.")
            .MaximumLength(128).WithMessage("La contraseña temporal no puede superar 128 caracteres.");
    }
}
