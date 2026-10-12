using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.DTOs;

namespace Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Commands;

public sealed record CreateAccessEnforcementPolicyCommand(
    Guid TenantId, Guid? ProfileId, Guid? RoleId, string EnforcementAction,
    // G-120 (FR-053): periodo de gracia en días (0 = inmediato). Opcional para compatibilidad.
    int GracePeriodDays = 0) : ICommand<CreateAccessEnforcementPolicyResponse>;
