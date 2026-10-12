namespace Evolith.Ums.Application.IGA.RolePromotion.Queries;

using Evolith.Ums.Application.IGA.Common;
using Evolith.Ums.Application.IGA.DTOs;
using Evolith.Ums.Domain.IGA;

public sealed class GetRolePromotionRequestByIdQueryHandler
    : IQueryHandler<GetRolePromotionRequestByIdQuery, RolePromotionRequestDto>
{
    private readonly IRolePromotionRequestRepository _repository;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public GetRolePromotionRequestByIdQueryHandler(
        IRolePromotionRequestRepository repository,
        ITenantScopePolicy tenantScopePolicy)
    {
        _repository = repository;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [AuthorizationAspect(ResourceCode = "view_role_promotions", ActionCode = "view")]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<RolePromotionRequestDto>> Handle(
        GetRolePromotionRequestByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.RolePromotionRequestId, cancellationToken);
        if (entity is null)
        {
            return Result<RolePromotionRequestDto>.Failure(DomainErrors.IGA.RolePromotionRequestNotFound);
        }

        var scope = IgaHandlerGuards.EnsureTenantInScope(_tenantScopePolicy, entity.TenantId.GetValue());
        if (scope.IsFailure)
        {
            return Result<RolePromotionRequestDto>.Failure(scope.Error);
        }

        return Result<RolePromotionRequestDto>.Success(RolePromotionMapper.ToDto(entity));
    }
}
