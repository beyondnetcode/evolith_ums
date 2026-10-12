namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

/// <summary>
/// Fija o corrige el nombre para mostrar de una cuenta.
///
/// <para>Va como SUB-RECURSO propio (<c>POST /user-accounts/{id}/display-name</c>) y no como un PUT
/// general de la cuenta, por el mismo criterio que ya sigue el resto de la API —<c>/activate</c>,
/// <c>/block</c>, <c>/validity</c>—: un PUT general obligaría a todo cliente a reenviar los campos
/// que no quiere tocar, y el que se los dejara los borraría sin pedirlo.</para>
///
/// <para><c>null</c> o cadena vacía dejan la cuenta sin nombre, que es un estado válido y distinto
/// de la cadena vacía.</para>
/// </summary>
public sealed record SetUserAccountDisplayNameCommand(
    Guid UserAccountId,
    string? DisplayName) : ICommand;
