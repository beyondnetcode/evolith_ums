using Evolith.Ums.Application.Identity.Tenant.SignupRequests.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.SignupRequests.Queries;

public sealed record GetPendingTenantSignupRequestsQuery() : IQuery<IReadOnlyList<TenantSignupRequestDto>>;
