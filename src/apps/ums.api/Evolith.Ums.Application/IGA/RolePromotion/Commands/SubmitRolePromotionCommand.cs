namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

/// <summary>
/// Draft → PendingEligibilityCheck (IGA, ADR-UMS-093, FR-061). Calcula el RiskScore vía
/// <c>IRiskScoreCalculator</c> y lo congela en el agregado (INV-RPR2).
/// </summary>
public sealed record SubmitRolePromotionCommand(Guid RolePromotionRequestId) : ICommand;
