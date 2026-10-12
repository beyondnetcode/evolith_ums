namespace Evolith.Ums.Application.Authorization.Profile.DTOs;

public sealed record ProfileDto(
    Guid ProfileId,
    Guid TenantId,
    string TenantCode,
    string TenantName,
    Guid UserId,
    string UserEmail,
    Guid RoleId,
    string RoleCode,
    string RoleName,
    Guid SystemSuiteId,
    string SystemSuiteCode,
    string SystemSuiteName,
    Guid? BranchId,
    string? BranchName,
    string Scope,
    bool IsActive,
    int PermissionCount,
    IReadOnlyList<ProfilePermissionDto> Permissions);
