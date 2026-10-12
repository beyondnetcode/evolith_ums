namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

/// <summary>
/// Draft → PendingApproval (delegación, ADR-UMS-086, espejo de ADR-UMS-093 FR-060).
/// Genera y congela el identificador de la solicitud de aprobación en el agregado
/// (<c>SubmitForApproval</c>). La delegación orquesta su propia máquina de aprobación
/// dentro de su agregado — un-agregado-por-transacción, sin ApprovalRequest genérico.
/// </summary>
public sealed record SubmitDelegationForApprovalCommand(Guid DelegationId) : ICommand;
