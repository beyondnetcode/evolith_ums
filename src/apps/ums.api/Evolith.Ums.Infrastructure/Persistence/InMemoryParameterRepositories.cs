#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.Parameter;

/// <summary>
/// In-memory implementation of all three Parameter repositories for dev/test environments.
/// Uses explicit interface implementation to resolve GetByIdAsync / SaveChangesAsync
/// signature collisions between the three interfaces.
/// </summary>
public sealed class InMemoryParameterRepositories
    : IParameterDefinitionRepository,
      IParameterGlobalValueRepository,
      IParameterTenantValueRepository
{
    private readonly ConcurrentDictionary<Guid, ParameterDefinition>  _defs   = new();
    private readonly ConcurrentDictionary<Guid, ParameterGlobalValue> _gv     = new();
    private readonly ConcurrentDictionary<Guid, ParameterTenantValue> _tv     = new();

    // ── IParameterDefinitionRepository ───────────────────────────────────────

    // Las lecturas ocultan lo eliminado lógicamente, igual que el filtro global de consulta del
    // contexto PostgreSQL: la fila sigue en el diccionario (nunca se retira), simplemente no se sirve.
    Task<ParameterDefinition?> IParameterDefinitionRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var definition = _defs.GetValueOrDefault(id);
        return Task.FromResult(definition is { Props.IsDeleted: false } ? definition : null);
    }

    Task<ParameterDefinition?> IParameterDefinitionRepository.GetByCodeAsync(string code, CancellationToken cancellationToken)
        => Task.FromResult(_defs.Values.FirstOrDefault(definition =>
            !definition.Props.IsDeleted &&
            string.Equals(definition.Props.Code.GetValue(), code, StringComparison.OrdinalIgnoreCase)));

    Task<IReadOnlyList<ParameterDefinition>> IParameterDefinitionRepository.GetAllAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ParameterDefinition>>(
            _defs.Values
                .Where(definition => !definition.Props.IsDeleted)
                .OrderBy(definition => definition.Props.DisplayOrder)
                .ToList());

    Task IParameterDefinitionRepository.AddAsync(ParameterDefinition definition, CancellationToken cancellationToken)
    {
        _defs[definition.Props.Id.GetValue()] = definition;
        return Task.CompletedTask;
    }

    Task IParameterDefinitionRepository.UpdateAsync(ParameterDefinition definition, CancellationToken cancellationToken)
    {
        _defs[definition.Props.Id.GetValue()] = definition;
        return Task.CompletedTask;
    }

    // Cuenta solo las VIVAS: el índice único de `Code` es parcial, así que una definición eliminada
    // ya no reserva su código y volver a declararlo debe poder hacerse.
    Task<int> IParameterDefinitionRepository.CountByCodeAsync(string code, CancellationToken cancellationToken)
        => Task.FromResult(_defs.Values.Count(definition =>
            !definition.Props.IsDeleted &&
            string.Equals(definition.Props.Code.GetValue(), code, StringComparison.OrdinalIgnoreCase)));

    Task<int> IParameterDefinitionRepository.CountLiveGlobalValuesAsync(Guid definitionId, CancellationToken cancellationToken)
        => Task.FromResult(_gv.Values.Count(value =>
            value.Props.ParameterDefinitionId.GetValue() == definitionId &&
            value.Props.Status != ConfigStatus.Deleted));

    Task<int> IParameterDefinitionRepository.CountLiveTenantValuesAsync(Guid definitionId, CancellationToken cancellationToken)
        => Task.FromResult(_tv.Values.Count(value =>
            value.Props.ParameterDefinitionId.GetValue() == definitionId &&
            value.Props.Status != ConfigStatus.Deleted));

    Task<bool> IParameterDefinitionRepository.SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    // ── IParameterGlobalValueRepository ──────────────────────────────────────

    Task<ParameterGlobalValue?> IParameterGlobalValueRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var value = _gv.GetValueOrDefault(id);
        return Task.FromResult(value is not null && value.Props.Status != ConfigStatus.Deleted ? value : null);
    }

    // Solo el valor VIVO: el índice único parcial dejó de contar las lápidas, así que devolverlas
    // bloquearía un alta que la base permite y daría por vigente un valor ya retirado.
    Task<ParameterGlobalValue?> IParameterGlobalValueRepository.GetByDefinitionIdAsync(Guid definitionId, CancellationToken cancellationToken)
        => Task.FromResult(_gv.Values.FirstOrDefault(value =>
            value.Props.ParameterDefinitionId.GetValue() == definitionId &&
            value.Props.Status != ConfigStatus.Deleted));

    Task IParameterGlobalValueRepository.AddAsync(ParameterGlobalValue value, CancellationToken cancellationToken)
    {
        _gv[value.Props.Id.GetValue()] = value;
        return Task.CompletedTask;
    }

    Task IParameterGlobalValueRepository.UpdateAsync(ParameterGlobalValue value, CancellationToken cancellationToken)
    {
        _gv[value.Props.Id.GetValue()] = value;
        return Task.CompletedTask;
    }

    Task<bool> IParameterGlobalValueRepository.SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    // ── IParameterTenantValueRepository ──────────────────────────────────────

    Task<ParameterTenantValue?> IParameterTenantValueRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var value = _tv.GetValueOrDefault(id);
        return Task.FromResult(value is not null && value.Props.Status != ConfigStatus.Deleted ? value : null);
    }

    // Solo el override VIVO, por la misma razón que en el valor global.
    Task<ParameterTenantValue?> IParameterTenantValueRepository.GetByTenantAndDefinitionAsync(
        Guid tenantId, Guid definitionId, CancellationToken cancellationToken)
        => Task.FromResult(_tv.Values.FirstOrDefault(value =>
            value.Props.TenantId.GetValue() == tenantId &&
            value.Props.ParameterDefinitionId.GetValue() == definitionId &&
            value.Props.Status != ConfigStatus.Deleted));

    Task IParameterTenantValueRepository.AddAsync(ParameterTenantValue value, CancellationToken cancellationToken)
    {
        _tv[value.Props.Id.GetValue()] = value;
        return Task.CompletedTask;
    }

    Task IParameterTenantValueRepository.UpdateAsync(ParameterTenantValue value, CancellationToken cancellationToken)
    {
        _tv[value.Props.Id.GetValue()] = value;
        return Task.CompletedTask;
    }

    Task<bool> IParameterTenantValueRepository.SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}

#pragma warning restore S4144
