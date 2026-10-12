namespace Evolith.Ums.Application.Identity.Tenant.SignupRequests.DTOs;

public sealed record ApproveTenantSignupResponse(
    Guid TenantId,
    Guid UserAccountId,
    string TemporaryPassword,
    string Message);
