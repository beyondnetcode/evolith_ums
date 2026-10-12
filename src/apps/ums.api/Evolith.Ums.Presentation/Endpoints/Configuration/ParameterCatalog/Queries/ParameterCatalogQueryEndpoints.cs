namespace Evolith.Ums.Presentation.Endpoints.Configuration.ParameterCatalog.Queries;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Configuration.ParameterCatalog.DTOs;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;

public static class ParameterCatalogQueryEndpoints
{
    public static IEndpointRouteBuilder MapParameterCatalogQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/parameter-definitions").WithTags("Parameter Catalog - Queries");

        group.MapGet("/", async (
            [FromServices] UmsPlatformDbContext dbContext,
            [FromQuery] string? search,
            [FromQuery] int? scopeId,
            [FromQuery] bool? isActive,
            CancellationToken ct) =>
        {
            var query = dbContext.ParameterDefinitions.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(x =>
                    x.Code.ToLower().Contains(searchLower) ||
                    x.Name.ToLower().Contains(searchLower));
            }

            if (scopeId.HasValue)
                query = query.Where(x => x.ScopeId == scopeId.Value);

            if (isActive.HasValue)
                query = query.Where(x => x.IsActive == isActive.Value);

            var items = await query
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Code)
                .Select(x => new ParameterDefinitionDto(
                    x.Id,
                    x.Code,
                    x.Name,
                    x.Description,
                    x.DataTypeId,
                    GetDataTypeName(x.DataTypeId),
                    x.DefaultValue,
                    x.ScopeId,
                    GetScopeName(x.ScopeId),
                    x.IsActive,
                    x.IsMandatory,
                    x.DisplayOrder,
                    x.Version))
                .ToListAsync(ct);

            return Results.Ok(new { items, totalItems = items.Count });
        })
        .WithName("GetParameterDefinitions")
        .Produces<ParameterDefinitionDto>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] UmsPlatformDbContext dbContext,
            CancellationToken ct) =>
        {
            var record = await dbContext.ParameterDefinitions
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (record is null)
                return Results.NotFound();

            var dto = new ParameterDefinitionDto(
                record.Id,
                record.Code,
                record.Name,
                record.Description,
                record.DataTypeId,
                GetDataTypeName(record.DataTypeId),
                record.DefaultValue,
                record.ScopeId,
                GetScopeName(record.ScopeId),
                record.IsActive,
                record.IsMandatory,
                record.DisplayOrder,
                record.Version);

            return Results.Ok(dto);
        })
        .WithName("GetParameterDefinitionById")
        .Produces<ParameterDefinitionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/resolved/{code}", async (
            string code,
            [FromServices] UmsPlatformDbContext dbContext,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new { error = "Parameter code is required." });

            var normalizedCode = code.Trim().ToUpperInvariant();
            // Las definiciones eliminadas lógicamente ya las descarta el filtro global de consulta;
            // los valores globales eliminados hay que descartarlos aquí: un valor retirado no puede
            // seguir resolviendo, la resolución debe caer al DefaultValue de la definición.
            var deletedStatusId = ConfigStatus.Deleted.Id;
            var resolved = await (
                from definition in dbContext.ParameterDefinitions
                join globalValue in dbContext.ParameterGlobalValues.Where(v => v.StatusId != deletedStatusId)
                    on definition.Id equals globalValue.ParameterDefinitionId into globalValues
                from globalValue in globalValues.DefaultIfEmpty()
                where definition.Code == normalizedCode && definition.IsActive
                select new ResolvedParameterDto(
                    definition.Id,
                    definition.Code,
                    definition.Name,
                    definition.Description ?? string.Empty,
                    definition.DataTypeId,
                    GetDataTypeName(definition.DataTypeId),
                    globalValue != null ? globalValue.EffectiveValue : definition.DefaultValue,
                    definition.DefaultValue,
                    definition.ScopeId,
                    GetScopeName(definition.ScopeId),
                    globalValue != null,
                    "Active"))
                .FirstOrDefaultAsync(ct);

            return resolved is null ? Results.NotFound() : Results.Ok(resolved);
        })
        .WithName("ResolveParameterByCode")
        .Produces<ResolvedParameterDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static string GetDataTypeName(int dataTypeId) => dataTypeId switch
    {
        1 => "String",
        2 => "Number",
        3 => "Boolean",
        4 => "Json",
        _ => "Unknown"
    };

    private static string GetScopeName(int scopeId) => scopeId switch
    {
        1 => "GlobalOnly",
        2 => "TenantOnly",
        3 => "GlobalAndTenant",
        _ => "Unknown"
    };
}
