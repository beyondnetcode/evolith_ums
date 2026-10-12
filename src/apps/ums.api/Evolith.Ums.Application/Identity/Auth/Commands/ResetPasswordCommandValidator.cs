namespace Evolith.Ums.Application.Identity.Auth.Commands;

using Evolith.Ums.Application.Configuration.Services;

/// <summary>
/// La longitud mínima se toma de la configuración global —la misma que ya rige el alta de
/// contraseñas— para que el restablecimiento no sea la puerta trasera por la que entra una
/// credencial más débil de lo que la política permite.
/// </summary>
public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator(IConfigurationProvider configProvider)
    {
        var minLength = configProvider.Global().MinPasswordLength;

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("El código de restablecimiento es obligatorio.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La nueva contraseña es obligatoria.")
            .MinimumLength(minLength).WithMessage($"La contraseña debe tener al menos {minLength} caracteres.")
            .MaximumLength(128).WithMessage("La contraseña no puede superar 128 caracteres.");
    }
}
