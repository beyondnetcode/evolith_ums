using Evolith.Ums.Application.Identity.UserManagementDelegation.DTOs;

namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Queries;

public sealed record GetDelegationsByDelegatedAdminQuery(Guid DelegatedAdminId, Guid TenantId) : IQuery<IReadOnlyList<DelegationDto>>;
