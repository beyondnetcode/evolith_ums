namespace Evolith.Ums.Application.IGA.RoleMaturity.Queries;

using Evolith.Ums.Application.IGA.Common;
using Evolith.Ums.Application.IGA.DTOs;
using Evolith.Ums.Domain.IGA;
using RoleMaturityStatusAggregate = Evolith.Ums.Domain.IGA.RoleMaturityStatus.RoleMaturityStatus;

public sealed class GetRoleMaturityStatusByUserQueryHandler
    : IQueryHandler<GetRoleMaturityStatusByUserQuery, IReadOnlyList<RoleMaturityStatusDto>>
{
    private readonly IRoleMaturityStatusRepository _repository;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public GetRoleMaturityStatusByUserQueryHandler(
        IRoleMaturityStatusRepository repository,
        ITenantScopePolicy tenantScopePolicy)
    {
        _repository = repository;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [AuthorizationAspect(ResourceCode = "view_role_promotions", ActionCode = "view")]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<IReadOnlyList<RoleMaturityStatusDto>>> Handle(
        GetRoleMaturityStatusByUserQuery request,
        CancellationToken cancellationToken)
    {
        var scope = IgaHandlerGuards.EnsureTenantInScope(_tenantScopePolicy, request.TenantId);
        if (scope.IsFailure)
        {
            return Result<IReadOnlyList<RoleMaturityStatusDto>>.Failure(scope.Error);
        }

        if (request.RoleId is not null)
        {
            var single = await _repository.GetByUserAndRoleAsync(
                request.TenantId, request.UserId, request.RoleId.Value, cancellationToken);
            var singleList = single is null
                ? new List<RoleMaturityStatusDto>()
                : new List<RoleMaturityStatusDto> { RolePromotionMapper.ToDto(single) };
            return Result<IReadOnlyList<RoleMaturityStatusDto>>.Success(singleList);
        }

        IReadOnlyList<RoleMaturityStatusAggregate> items =
            await _repository.GetByUserAsync(request.TenantId, request.UserId, cancellationToken);

        var dtos = items.Select(RolePromotionMapper.ToDto).ToList();
        return Result<IReadOnlyList<RoleMaturityStatusDto>>.Success(dtos);
    }
}
