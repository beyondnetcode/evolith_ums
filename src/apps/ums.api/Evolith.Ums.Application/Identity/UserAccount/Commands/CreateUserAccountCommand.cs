using Evolith.Ums.Application.Identity.UserAccount.DTOs;

namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record CreateUserAccountCommand(
    Guid TenantId,
    Guid? BranchId,
    string Email,
    string Category,
    string? IdentityReference,
    string? IdentityReferenceType,
    /// <summary>
    /// Nombre para mostrar. El dominio lo admite desde siempre (<c>UserAccount.Create</c> recibe un
    /// <c>Name?</c>) pero ningún comando lo exponía, así que toda cuenta creada por API —o por la
    /// aplicación web, que usa este mismo endpoint— nacía sin nombre y no había forma de ponérselo
    /// después: no existe comando que lo actualice. El único camino que lo fijaba era la aprobación
    /// de una solicitud de inquilino, que lo toma del contacto.
    ///
    /// Opcional y al final del record para no romper a los llamadores existentes.
    /// </summary>
    string? DisplayName = null) : ICommand<CreateUserAccountResponse>;
