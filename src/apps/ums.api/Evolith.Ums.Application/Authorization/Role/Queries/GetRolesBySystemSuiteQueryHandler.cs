using Evolith.Ums.Application.Authorization.Role.DTOs;
using Evolith.Ums.Domain.Authorization;

namespace Evolith.Ums.Application.Authorization.Role.Queries;

public sealed class GetRolesBySystemSuiteQueryHandler : IQueryHandler<GetRolesBySystemSuiteQuery, IReadOnlyList<RoleDto>>
{
    private readonly IRoleRepository _repository;

    public GetRolesBySystemSuiteQueryHandler(IRoleRepository repository)
    {
        _repository = repository;
    }

    [AuthorizationAspect(ResourceCode = "view_roles", ActionCode = "view")]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(GetRolesBySystemSuiteQuery request, CancellationToken cancellationToken)
    {
        var roles = await _repository.GetBySystemSuiteIdAsync(request.SystemSuiteId, cancellationToken);
        return Result<IReadOnlyList<RoleDto>>.Success(roles.Select(RoleDto.Map).ToList());
    }
}
