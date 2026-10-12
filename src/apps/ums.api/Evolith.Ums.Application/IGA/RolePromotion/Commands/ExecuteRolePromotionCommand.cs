namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

/// <summary>Approved → Executed (aplica el cambio de rol). El ejecutor es el usuario autenticado. IGA, ADR-UMS-093, INV-RPR5.</summary>
public sealed record ExecuteRolePromotionCommand(Guid RolePromotionRequestId) : ICommand;
