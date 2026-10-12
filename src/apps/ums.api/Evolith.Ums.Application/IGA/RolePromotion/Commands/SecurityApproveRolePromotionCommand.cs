namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

/// <summary>PendingSecurityReview → Approved. El revisor es el usuario autenticado. IGA, ADR-UMS-093, FR-060.</summary>
public sealed record SecurityApproveRolePromotionCommand(Guid RolePromotionRequestId) : ICommand;
