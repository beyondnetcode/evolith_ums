using Evolith.Ums.Application.Identity.Tenant.SignupRequests.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.SignupRequests.Commands;

public sealed record ApproveTenantSignupCommand(Guid TenantSignupRequestId) : ICommand<ApproveTenantSignupResponse>;
