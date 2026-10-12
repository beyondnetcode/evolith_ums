namespace Evolith.Ums.Infrastructure.Configuration;

using Evolith.Ums.Application.Common;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Microsoft.EntityFrameworkCore;

public sealed record ResolvedParameter(
    Guid DefinitionId,
    string Code,
    string Name,
    string Description,
    int DataTypeId,
    string EffectiveValue,
    string DefaultValue,
    int ScopeId,
    bool IsOverride,
    string Status);

public interface IParameterResolutionService
{
    Task<IReadOnlyList<ResolvedParameter>> GetGlobalParametersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResolvedParameter>> GetTenantParametersAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<string> GetEffectiveValueAsync(Guid? tenantId, string code, CancellationToken cancellationToken = default);
}

public sealed class ParameterResolutionService : IParameterResolutionService
{
    private readonly UmsPlatformDbContext _dbContext;

    public ParameterResolutionService(UmsPlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Estado terminal de borrado lógico. Todas las lecturas de esta clase lo excluyen, y no es un
    /// detalle cosmético: desde que el índice único pasó a ser PARCIAL, una definición puede tener
    /// una fila de valor viva y N lápidas. Indexar por `ParameterDefinitionId` sin este filtro haría
    /// estallar `ToDictionaryAsync` con clave duplicada, y `FirstOrDefault` resolvería un valor
    /// retirado hace meses. El filtro es lo que garantiza que la resolución devuelve LA VIVA.
    /// </summary>
    private static readonly int DeletedStatusId = ConfigStatus.Deleted.Id;

    public async Task<IReadOnlyList<ResolvedParameter>> GetGlobalParametersAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await _dbContext.ParameterDefinitions
            .Where(d => d.IsActive && (d.ScopeId == 1 || d.ScopeId == 3))
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.Code)
            .ToListAsync(cancellationToken);

        var globalValues = await _dbContext.ParameterGlobalValues
            .IgnoreQueryFilters()
            .Where(v => v.StatusId != DeletedStatusId)
            .ToDictionaryAsync(v => v.ParameterDefinitionId, cancellationToken);

        var results = new List<ResolvedParameter>();
        foreach (var def in definitions)
        {
            var hasOverride = globalValues.TryGetValue(def.Id, out var globalValue);
            var effectiveValue = hasOverride ? globalValue.EffectiveValue : def.DefaultValue;
            var status = hasOverride ? globalValue.StatusId.ToString() : "Default";

            results.Add(new ResolvedParameter(
                def.Id,
                def.Code,
                def.Name,
                def.Description,
                def.DataTypeId,
                effectiveValue,
                def.DefaultValue,
                def.ScopeId,
                hasOverride,
                status));
        }

        return results;
    }

    public async Task<IReadOnlyList<ResolvedParameter>> GetTenantParametersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var definitions = await _dbContext.ParameterDefinitions
            .Where(d => d.IsActive && (d.ScopeId == 2 || d.ScopeId == 3))
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.Code)
            .ToListAsync(cancellationToken);

        var globalValues = await _dbContext.ParameterGlobalValues
            .IgnoreQueryFilters()
            .Where(v => v.StatusId != DeletedStatusId)
            .ToDictionaryAsync(v => v.ParameterDefinitionId, cancellationToken);

        var tenantValues = await _dbContext.ParameterTenantValues
            .IgnoreQueryFilters()
            .Where(v => v.TenantId == tenantId && v.StatusId != DeletedStatusId)
            .ToDictionaryAsync(v => v.ParameterDefinitionId, cancellationToken);

        var results = new List<ResolvedParameter>();
        foreach (var def in definitions)
        {
            var hasTenantOverride = tenantValues.TryGetValue(def.Id, out var tenantValue);
            string effectiveValue;
            if (hasTenantOverride)
                effectiveValue = tenantValue.OverrideValue;
            else if (def.ScopeId == 3 && globalValues.TryGetValue(def.Id, out var globalValue))
                effectiveValue = globalValue.EffectiveValue;
            else
                effectiveValue = def.DefaultValue;

            string status;
            if (hasTenantOverride)
                status = tenantValue.StatusId.ToString();
            else if (def.ScopeId == 3 && globalValues.ContainsKey(def.Id))
                status = "GlobalDefault";
            else
                status = "Default";

            results.Add(new ResolvedParameter(
                def.Id,
                def.Code,
                def.Name,
                def.Description,
                def.DataTypeId,
                effectiveValue,
                def.DefaultValue,
                def.ScopeId,
                hasTenantOverride,
                status));
        }

        return results;
    }

    public async Task<string> GetEffectiveValueAsync(Guid? tenantId, string code, CancellationToken cancellationToken = default)
    {
        var definition = await _dbContext.ParameterDefinitions
            .FirstOrDefaultAsync(d => d.Code == code && d.IsActive, cancellationToken);

        if (definition is null)
            return string.Empty;

        if (tenantId.HasValue && (definition.ScopeId == 2 || definition.ScopeId == 3))
        {
            var tenantValue = await _dbContext.ParameterTenantValues
                .IgnoreQueryFilters()
                .Where(v => v.TenantId == tenantId.Value
                    && v.ParameterDefinitionId == definition.Id
                    && v.StatusId != DeletedStatusId)
                .OrderByDescending(v => v.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (tenantValue is not null)
                return tenantValue.OverrideValue;
        }

        if (definition.ScopeId == 1 || definition.ScopeId == 3)
        {
            var globalValue = await _dbContext.ParameterGlobalValues
                .IgnoreQueryFilters()
                .Where(v => v.ParameterDefinitionId == definition.Id && v.StatusId != DeletedStatusId)
                .OrderByDescending(v => v.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (globalValue is not null)
                return globalValue.EffectiveValue;
        }

        return definition.DefaultValue;
    }
}
