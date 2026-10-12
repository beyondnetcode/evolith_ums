using Evolith.Ums.Application.Identity.Tenant.SignupRequests.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.SignupRequests.Commands;

public sealed record RequestTenantSignupCommand(
    string CompanyName,
    string CompanyReference,
    string ContactName,
    string ContactEmail) : ICommand<RequestTenantSignupResponse>;
