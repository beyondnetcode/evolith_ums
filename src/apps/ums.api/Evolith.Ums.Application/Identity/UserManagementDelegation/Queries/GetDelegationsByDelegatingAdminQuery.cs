using Evolith.Ums.Application.Identity.UserManagementDelegation.DTOs;

namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Queries;

public sealed record GetDelegationsByDelegatingAdminQuery(Guid DelegatingAdminId, Guid TenantId) : IQuery<IReadOnlyList<DelegationDto>>;
