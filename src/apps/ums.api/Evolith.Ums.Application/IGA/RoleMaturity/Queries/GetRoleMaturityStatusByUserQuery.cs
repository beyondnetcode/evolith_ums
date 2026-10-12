namespace Evolith.Ums.Application.IGA.RoleMaturity.Queries;

using Evolith.Ums.Application.IGA.DTOs;

/// <summary>
/// Obtiene el/los estado(s) de madurez de un usuario, acotado por inquilino. Si se indica
/// <paramref name="RoleId"/> devuelve el estado de ese rol; en caso contrario, todos los del usuario.
/// IGA, ADR-UMS-093, FR-062.
/// </summary>
public sealed record GetRoleMaturityStatusByUserQuery(Guid TenantId, Guid UserId, Guid? RoleId)
    : IQuery<IReadOnlyList<RoleMaturityStatusDto>>;
