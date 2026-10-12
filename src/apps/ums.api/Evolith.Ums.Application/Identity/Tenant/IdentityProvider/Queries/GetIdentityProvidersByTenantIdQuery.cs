using Evolith.Ums.Application.Identity.Tenant.IdentityProvider.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.IdentityProvider.Queries;

public sealed record GetIdentityProvidersByTenantIdQuery(Guid TenantId) : IQuery<IReadOnlyList<IdentityProviderDto>>;
