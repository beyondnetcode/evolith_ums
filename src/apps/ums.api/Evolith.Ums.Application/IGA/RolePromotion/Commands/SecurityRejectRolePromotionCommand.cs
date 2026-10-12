namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

/// <summary>PendingSecurityReview → Rejected (seguridad rechaza con motivo). IGA, ADR-UMS-093, FR-060.</summary>
public sealed record SecurityRejectRolePromotionCommand(Guid RolePromotionRequestId, string Reason) : ICommand;
