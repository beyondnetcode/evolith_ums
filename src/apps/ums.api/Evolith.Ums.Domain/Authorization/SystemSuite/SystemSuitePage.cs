namespace Evolith.Ums.Domain.Authorization.SystemSuite;

/// <summary>
/// Criterios de una página del catálogo de sistemas. Los valores llegan ya normalizados desde la
/// capa de aplicación: aquí no se corrigen, se traducen a SQL.
/// </summary>
/// <param name="TenantId">Ámbito del inquilino; null solo para el operador de la plataforma.</param>
/// <param name="Status">Estado exacto a filtrar, o null para no filtrar.</param>
/// <param name="SearchField">Campo sobre el que buscar: <c>code</c>, <c>id</c> o <c>name</c>.</param>
/// <param name="Search">Texto a buscar, o null.</param>
/// <param name="SortBy">Campo de orden: <c>code</c>, <c>status</c> o <c>name</c>.</param>
/// <param name="Descending">Orden descendente.</param>
public sealed record SystemSuitePageQuery(
    Guid?   TenantId,
    string? Status,
    string  SearchField,
    string? Search,
    string  SortBy,
    bool    Descending,
    int     Page,
    int     PageSize);

/// <summary>Identificadores de la página, en orden, y el total que cumple el filtro.</summary>
public sealed record SystemSuitePage(IReadOnlyList<Guid> Ids, int TotalItems);
