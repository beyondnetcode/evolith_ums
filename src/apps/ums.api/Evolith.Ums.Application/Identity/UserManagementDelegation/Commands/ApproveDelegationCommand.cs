namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

/// <summary>
/// PendingApproval → Active (delegación, ADR-UMS-086, espejo de ADR-UMS-093 FR-060).
/// Resuelve la solicitud de aprobación aprobándola y activa la delegación.
/// El aprobador es el usuario autenticado.
/// </summary>
public sealed record ApproveDelegationCommand(Guid DelegationId) : ICommand;
