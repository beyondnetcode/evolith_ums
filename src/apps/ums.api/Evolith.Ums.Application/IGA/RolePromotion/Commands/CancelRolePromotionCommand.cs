namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

/// <summary>Draft → Cancelled (el solicitante cancela antes de enviar). IGA, ADR-UMS-093, FR-060.</summary>
public sealed record CancelRolePromotionCommand(Guid RolePromotionRequestId, string Reason) : ICommand;
