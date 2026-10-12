using Evolith.Ums.Application.Identity.UserManagementDelegation.DTOs;

namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Queries;

public sealed class GetAllDelegationsQueryHandler : IQueryHandler<GetAllDelegationsQuery, IReadOnlyList<DelegationDto>>
{
    private readonly IUserManagementDelegationRepository _repository;

    public GetAllDelegationsQueryHandler(IUserManagementDelegationRepository repository)
    {
        _repository = repository;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_delegations", ActionCode = "view")]
    public async Task<Result<IReadOnlyList<DelegationDto>>> Handle(
        GetAllDelegationsQuery request,
        CancellationToken cancellationToken)
    {
        var delegations = await _repository.GetAllAsync(null, cancellationToken);
        var dtos = delegations.Select(GetDelegationByIdQueryHandler.ToDto).ToList();
        return Result<IReadOnlyList<DelegationDto>>.Success(dtos);
    }
}
