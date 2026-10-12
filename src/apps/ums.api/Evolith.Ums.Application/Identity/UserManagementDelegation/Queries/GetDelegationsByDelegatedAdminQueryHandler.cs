using Evolith.Ums.Application.Identity.UserManagementDelegation.DTOs;

namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Queries;

public sealed class GetDelegationsByDelegatedAdminQueryHandler : IQueryHandler<GetDelegationsByDelegatedAdminQuery, IReadOnlyList<DelegationDto>>
{
    private readonly IUserManagementDelegationRepository _repository;

    public GetDelegationsByDelegatedAdminQueryHandler(IUserManagementDelegationRepository repository)
    {
        _repository = repository;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_delegations", ActionCode = "view")]
    public async Task<Result<IReadOnlyList<DelegationDto>>> Handle(
        GetDelegationsByDelegatedAdminQuery request,
        CancellationToken cancellationToken)
    {
        var delegations = await _repository.GetByDelegatedAdminAsync(
            request.DelegatedAdminId,
            request.TenantId,
            cancellationToken);

        var dtos = delegations.Select(GetDelegationByIdQueryHandler.ToDto).ToList();
        return Result<IReadOnlyList<DelegationDto>>.Success(dtos);
    }
}
