using Evolith.Ums.Application.Identity.Tenant.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.Queries;

public sealed record GetAllTenantsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    // Campo de búsqueda (`criteria`). Null = no especificado: el repositorio cae a SortBy por
    // compatibilidad (antes el campo de búsqueda se derivaba de SortBy). Ver GetAllTenantsQueryHandler.
    string? Criteria = null,
    string Status = "all",
    string SortBy = "name",
    string SortOrder = "asc") : IQuery<PagedResult<TenantDto>>;
