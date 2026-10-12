namespace Evolith.Ums.Application.IGA.RolePromotion.Queries;

using Evolith.Ums.Application.IGA.DTOs;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Domain.IGA.RolePromotionRequest;
using RolePromotionRequestAggregate = Evolith.Ums.Domain.IGA.RolePromotionRequest.RolePromotionRequest;

public sealed class ListRolePromotionRequestsQueryHandler
    : IQueryHandler<ListRolePromotionRequestsQuery, IReadOnlyList<RolePromotionRequestDto>>
{
    private readonly IRolePromotionRequestRepository _repository;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public ListRolePromotionRequestsQueryHandler(
        IRolePromotionRequestRepository repository,
        ITenantScopePolicy tenantScopePolicy)
    {
        _repository = repository;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [AuthorizationAspect(ResourceCode = "view_role_promotions", ActionCode = "view")]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<IReadOnlyList<RolePromotionRequestDto>>> Handle(
        ListRolePromotionRequestsQuery request,
        CancellationToken cancellationToken)
    {
        // Acotación por inquilino: un usuario regular queda ceñido a su inquilino; un administrador
        // interno (alcance nulo) puede consultar el inquilino solicitado o, en su defecto, todos.
        var scope = _tenantScopePolicy.ResolveQueryScope();
        var effectiveTenantId = scope ?? request.TenantId;

        IReadOnlyList<RolePromotionRequestAggregate> items;
        if (effectiveTenantId is null)
        {
            items = await _repository.GetAllAsync(null, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.Status))
        {
            items = await _repository.GetByTenantAndStatusAsync(effectiveTenantId.Value, request.Status!, cancellationToken);
        }
        else
        {
            items = await _repository.GetByTenantIdAsync(effectiveTenantId.Value, cancellationToken);
        }

        var dtos = items.Select(RolePromotionMapper.ToDto).ToList();
        return Result<IReadOnlyList<RolePromotionRequestDto>>.Success(dtos);
    }
}
