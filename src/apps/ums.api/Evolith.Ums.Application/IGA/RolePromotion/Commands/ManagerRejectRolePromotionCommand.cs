namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

/// <summary>PendingManagerApproval → Rejected (el gerente rechaza con motivo). IGA, ADR-UMS-093, FR-060.</summary>
public sealed record ManagerRejectRolePromotionCommand(Guid RolePromotionRequestId, string Reason) : ICommand;
