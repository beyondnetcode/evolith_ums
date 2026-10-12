namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

/// <summary>Executed → Verified (verificación post-ejecución por un auditor). IGA, ADR-UMS-093, INV-RPR5.</summary>
public sealed record VerifyRolePromotionCommand(Guid RolePromotionRequestId) : ICommand;
