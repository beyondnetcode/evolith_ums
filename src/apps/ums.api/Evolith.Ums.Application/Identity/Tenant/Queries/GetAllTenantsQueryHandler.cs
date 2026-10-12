using Evolith.Ums.Application.Common.Aop;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.Tenant.DTOs;
using Evolith.Ums.Domain.Identity;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Identity.Tenant.Queries;

public sealed class GetAllTenantsQueryHandler : IQueryHandler<GetAllTenantsQuery, PagedResult<TenantDto>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public GetAllTenantsQueryHandler(ITenantRepository tenantRepository, ITenantScopePolicy tenantScopePolicy)
    {
        _tenantRepository = tenantRepository;
        _tenantScopePolicy = tenantScopePolicy;
    }

    // EN EL MÉTODO, NO EN LA CLASE, y esa distinción era el fallo: `PointCut.CanApply` --quien
    // decide si el aspecto intercepta-- solo mira los atributos del MÉTODO objetivo
    // (reference/architecture/shell-libraries/aop.md). Con el atributo en la clase, el aspecto no
    // llegaba a ejecutarse NUNCA y este endpoint quedaba sin control de permisos: comprobado
    // montando el proxy real con el DLL de producción, `HasPermission` no se consultaba jamás.
    //
    // Y el código del permiso también estaba mal. Pedía `TENANTS:VIEW`, que `UserContext` normaliza
    // a `tenants.view`, y ese scope NO EXISTE: el catálogo de la suite UMS declara `view_tenants` y
    // `manage_tenants`. Arreglar solo la colocación habría encendido el gate para denegar a todo el
    // mundo salvo al administrador interno, que pasa por el atajo de `is_internal_admin`.
    [AuthorizationAspect(ResourceCode = "view_tenants", ActionCode = "view")]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<PagedResult<TenantDto>>> Handle(
        GetAllTenantsQuery request,
        CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var status = NormalizeText(request.Status, "all");
        var sortBy = NormalizeText(request.SortBy, "name").ToLowerInvariant();
        var sortOrder = NormalizeText(request.SortOrder, "asc").ToLowerInvariant();
        var search = NormalizeSearch(request.Search);

        var effectiveTenantId = _tenantScopePolicy.ResolveQueryScope();

        // REC-12: Push filtering/sorting/pagination to the repository so SQL
        // implementations use DB-level Skip/Take instead of loading all rows.
        // searchField (parámetro `criteria` del API) determina el campo de búsqueda. Se pasa el valor
        // CRUDO (no el normalizado con default "name"): si el cliente no envía `criteria`, va null y el
        // repositorio cae a `sortBy` por compatibilidad hacia atrás (antes el campo de búsqueda se
        // derivaba de sortBy). Si el cliente sí envía `criteria` (p. ej. "code"), este manda.
        var (tenants, totalItems) = await _tenantRepository.GetPagedAsync(
            page, pageSize, search, status, sortBy, sortOrder, effectiveTenantId, cancellationToken, searchField: request.Criteria);

        var items = tenants.Select(t => new TenantDto(
            t.Props.Id.GetValue(),
            t.Props.Code.GetValue(),
            t.Props.Name.GetValue(),
            t.Props.Type.ToString(),
            t.Props.Status.ToString(),
            t.Props.ParentTenantId?.GetValue(),
            t.Props.CompanyReference?.GetValue(),
            t.Props.IsManagementOwner))
            .ToList();

        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        return Result<PagedResult<TenantDto>>.Success(new PagedResult<TenantDto>(
            items,
            page,
            pageSize,
            totalItems,
            totalPages));
    }
}
