namespace Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.DTOs;

public sealed record AccessEnforcementPolicyDto(
    Guid AccessEnforcementPolicyId,
    Guid TenantId,
    Guid? ProfileId,
    Guid? RoleId,
    string EnforcementAction,
    bool IsActive,
    // G-120 (FR-053): periodo de gracia en días antes de aplicar el enforcement (0 = inmediato).
    int GracePeriodDays);
