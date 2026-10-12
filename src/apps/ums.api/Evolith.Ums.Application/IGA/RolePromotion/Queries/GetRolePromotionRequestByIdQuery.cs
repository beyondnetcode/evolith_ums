namespace Evolith.Ums.Application.IGA.RolePromotion.Queries;

using Evolith.Ums.Application.IGA.DTOs;

/// <summary>Obtiene una solicitud de promoción de rol por su identificador, acotada por inquilino. IGA, ADR-UMS-093.</summary>
public sealed record GetRolePromotionRequestByIdQuery(Guid RolePromotionRequestId) : IQuery<RolePromotionRequestDto>;
