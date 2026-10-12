namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

using FluentValidation;

public sealed class SetUserAccountDisplayNameCommandValidator : AbstractValidator<SetUserAccountDisplayNameCommand>
{
    /// <summary>
    /// Mismo límite que el alta y que la columna: 150. Fijarlo aquí evita que un nombre demasiado
    /// largo llegue a la base y falle con un error de truncado, que no dice nada al llamante.
    /// </summary>
    public const int LongitudMaxima = 150;

    public SetUserAccountDisplayNameCommandValidator()
    {
        RuleFor(command => command.UserAccountId).NotEmpty();

        // Se admite null y cadena vacía: son la forma de dejar la cuenta SIN nombre, que es un
        // estado válido. Solo se acota la longitud cuando viene algo.
        RuleFor(command => command.DisplayName)
            .MaximumLength(LongitudMaxima)
            .When(command => !string.IsNullOrWhiteSpace(command.DisplayName));
    }
}
