using Evolith.Ums.Application.Identity.UserManagementDelegation.DTOs;

namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Queries;

public sealed record GetAllDelegationsQuery : IQuery<IReadOnlyList<DelegationDto>>;
