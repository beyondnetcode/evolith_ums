namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

/// <summary>
/// PendingEligibilityCheck → PendingManagerApproval (elegible) o → Rejected (no elegible, fail-closed).
/// Orquesta <c>RoleMaturityStatus.EvaluateEligibility</c> y aplica el resultado en
/// <c>RolePromotionRequest.ConfirmEligibility</c> (IGA, ADR-UMS-093, FR-062, INV-RPR4).
/// </summary>
public sealed record ConfirmRolePromotionEligibilityCommand(Guid RolePromotionRequestId) : ICommand;
