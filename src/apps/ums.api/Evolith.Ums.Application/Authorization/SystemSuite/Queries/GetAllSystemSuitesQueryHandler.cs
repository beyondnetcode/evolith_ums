using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.SystemSuite;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Queries;

public sealed class GetAllSystemSuitesQueryHandler : IQueryHandler<GetAllSystemSuitesQuery, PagedResult<SystemSuiteDto>>
{
    private readonly ISystemSuiteRepository _systemSuiteRepository;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public GetAllSystemSuitesQueryHandler(
        ISystemSuiteRepository systemSuiteRepository,
        ITenantScopePolicy tenantScopePolicy)
    {
        _systemSuiteRepository = systemSuiteRepository;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_suites", ActionCode = "view")]
    public async Task<Result<PagedResult<SystemSuiteDto>>> Handle(
        GetAllSystemSuitesQuery request,
        CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var criteria = NormalizeText(request.Criteria, "name").ToLowerInvariant();
        var status = NormalizeStatusFilter(request.Status);
        var sortBy = NormalizeText(request.SortBy, "name").ToLowerInvariant();
        var sortOrder = NormalizeSortOrder(request.SortOrder);
        var search = NormalizeSearch(request.Search);

        var effectiveTenantId = _tenantScopePolicy.ResolveQueryScope();

        // G-179: filtro, orden y recorte se resuelven EN LA BASE. Antes se traían todas las suites
        // del inquilino con su árbol completo —módulos, nodos, acciones y recursos de dominio— para
        // devolver una página de veinte: con cientos de sistemas, traer el catálogo entero a la
        // aplicación y tirar el 95 %.
        var pagina = await _systemSuiteRepository.GetPageAsync(
            new SystemSuitePageQuery(
                TenantId:    effectiveTenantId,
                Status:      status,
                SearchField: criteria,
                Search:      string.IsNullOrWhiteSpace(search) ? null : search,
                SortBy:      sortBy,
                Descending:  string.Equals(sortOrder, QueryDefaults.Descending, StringComparison.OrdinalIgnoreCase),
                Page:        page,
                PageSize:    pageSize),
            cancellationToken);

        // El árbol se carga SOLO para las suites de la página, y en el orden que la página fijó.
        var suites = await _systemSuiteRepository.GetByIdsAsync(pagina.Ids, cancellationToken);
        var items = suites.Select(SystemSuiteDto.Map).ToList();

        var totalItems = pagina.TotalItems;
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        return Result<PagedResult<SystemSuiteDto>>.Success(new PagedResult<SystemSuiteDto>(
            items,
            page,
            pageSize,
            totalItems,
            totalPages));
    }
}
