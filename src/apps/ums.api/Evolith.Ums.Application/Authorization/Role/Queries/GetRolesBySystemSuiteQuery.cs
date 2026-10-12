using Evolith.Ums.Application.Authorization.Role.DTOs;

namespace Evolith.Ums.Application.Authorization.Role.Queries;

public sealed record GetRolesBySystemSuiteQuery(Guid SystemSuiteId) : IQuery<IReadOnlyList<RoleDto>>;
