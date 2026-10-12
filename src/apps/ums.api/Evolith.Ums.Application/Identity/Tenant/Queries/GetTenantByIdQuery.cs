using Evolith.Ums.Application.Identity.Tenant.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.Queries;

public sealed record GetTenantByIdQuery(Guid TenantId) : IQuery<TenantDto>;
