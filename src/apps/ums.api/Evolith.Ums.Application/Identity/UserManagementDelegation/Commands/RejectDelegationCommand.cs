namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

/// <summary>
/// PendingApproval → Rejected (delegación, ADR-UMS-086, espejo de ADR-UMS-093 FR-060).
/// Resuelve la solicitud de aprobación rechazándola con un motivo obligatorio.
/// El revisor es el usuario autenticado.
/// </summary>
public sealed record RejectDelegationCommand(Guid DelegationId, string Reason) : ICommand;
