namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

/// <summary>
/// PendingManagerApproval → PendingSecurityReview (RiskScore ≥ umbral) o → Approved (RiskScore &lt; umbral).
/// El aprobador es el usuario autenticado (IGA, ADR-UMS-093, FR-060).
/// </summary>
public sealed record ManagerApproveRolePromotionCommand(Guid RolePromotionRequestId) : ICommand;
