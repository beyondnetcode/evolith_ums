namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

using Evolith.Ums.Application.IGA.DTOs;

/// <summary>
/// Crea una solicitud de promoción de rol en estado <c>Draft</c> (IGA, ADR-UMS-093, FR-060).
/// El solicitante es el usuario autenticado; no puede coincidir con el usuario objetivo (SoD).
/// </summary>
public sealed record CreateRolePromotionRequestCommand(
    Guid TenantId,
    Guid TargetUserId,
    Guid CurrentRoleId,
    Guid TargetRoleId) : ICommand<CreateRolePromotionRequestResponse>;
