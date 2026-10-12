namespace Evolith.Ums.Application.Identity.Tenant.SignupRequests.DTOs;

public sealed record TenantSignupRequestDto(
    Guid TenantSignupRequestId,
    string CompanyName,
    string CompanyReference,
    string ContactName,
    string ContactEmail,
    string Status,
    Guid? ApprovedTenantId,
    DateTime RequestedAtUtc);
