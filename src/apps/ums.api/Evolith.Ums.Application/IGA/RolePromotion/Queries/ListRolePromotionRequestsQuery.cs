namespace Evolith.Ums.Application.IGA.RolePromotion.Queries;

using Evolith.Ums.Application.IGA.DTOs;

/// <summary>
/// Lista solicitudes de promoción de rol acotadas por inquilino y, opcionalmente, por estado
/// (nombre de <c>RolePromotionStatus</c>). IGA, ADR-UMS-093.
/// </summary>
public sealed record ListRolePromotionRequestsQuery(Guid? TenantId, string? Status)
    : IQuery<IReadOnlyList<RolePromotionRequestDto>>;
