namespace Evolith.Ums.Domain.Configuration;

using Evolith.Ums.Domain.Configuration.AppConfiguration;
using Evolith.Ums.Domain.Configuration.FeatureFlag;
using Evolith.Ums.Domain.Configuration.IdpConfiguration;
using Evolith.Ums.Domain.Configuration.Parameter;
using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;
using FeatureFlagAggregate = Evolith.Ums.Domain.Configuration.FeatureFlag.FeatureFlag;
using IdpConfigurationAggregate = Evolith.Ums.Domain.Configuration.IdpConfiguration.IdpConfiguration;

public interface IAppConfigurationRepository : IAggregateRepository<AppConfigurationAggregate>
{
    Task<AppConfigurationAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quién ocupa la ranura (ámbito, código) AHORA. Nunca devuelve una configuración eliminada
    /// lógicamente: su fila permanece en la tabla, pero deja de reservar la ranura porque el índice
    /// único es parcial. Con varias filas del mismo código —una viva y N lápidas— la respuesta es
    /// determinista: la viva.
    /// </summary>
    Task<AppConfigurationAggregate?> GetByScopeAndCodeAsync(Guid? tenantId, Guid? systemSuiteId, Guid? moduleId, string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppConfigurationAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// REC-10: Overload of UpdateAsync that enforces optimistic concurrency by setting
    /// the expected RowVersion (from the client's If-Match ETag).  If <paramref name="expectedRowVersion"/>
    /// is null the update proceeds without the explicit concurrency check (EF still tracks its own version).
    /// </summary>
    Task UpdateAsync(AppConfigurationAggregate aggregate, byte[]? expectedRowVersion, CancellationToken cancellationToken = default);

    // NOTA DELIBERADA: aquí NO hay DeleteAsync. El borrado de una configuración es LÓGICO
    // (`AppConfiguration.Delete()` → estado terminal Deleted, persistido por UpdateAsync). Exponer
    // un borrado físico en el contrato sería un arma cargada: basta una llamada para perder la fila
    // que explica por qué el sistema se comportó como se comportó.
}

public interface IFeatureFlagRepository : IAggregateRepository<FeatureFlagAggregate>
{
    Task<FeatureFlagAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    [Obsolete("Use GetBySystemSuiteAndCodeAsync instead. Global flag codes are no longer unique.")]
    Task<FeatureFlagAggregate?> GetByCodeAsync(string flagCode, CancellationToken cancellationToken = default);
    Task<FeatureFlagAggregate?> GetBySystemSuiteAndCodeAsync(Guid systemSuiteId, string flagCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeatureFlagAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeatureFlagAggregate>> GetBySystemSuiteIdAsync(Guid systemSuiteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Banderas de un sistema SIN su historial de evaluaciones, para la construcción del grafo.
    ///
    /// El evaluador que usa el grafo es sin estado y jamás mira `EvaluationLogs`, pero la carga
    /// general los arrastra: una colección sin cota que crece con cada evaluación registrada
    /// desde el endpoint administrativo y que se pagaba en cada login (G-174).
    /// </summary>
    Task<IReadOnlyList<FeatureFlagAggregate>> GetBySystemSuiteIdForEvaluationAsync(Guid systemSuiteId, CancellationToken cancellationToken = default);
}

public interface IParameterDefinitionRepository
{
    /// <summary>Las lecturas NO devuelven definiciones eliminadas lógicamente.</summary>
    Task<ParameterDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <inheritdoc cref="GetByIdAsync"/>
    Task<ParameterDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    /// <inheritdoc cref="GetByIdAsync"/>
    Task<IReadOnlyList<ParameterDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ParameterDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste el agregado, incluida la marca de borrado lógico (`IsDeleted`). No existe un
    /// DeleteAsync: el borrado de una definición es un cambio de estado, no una baja de la fila.
    /// </summary>
    Task UpdateAsync(ParameterDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta las definiciones VIVAS con ese código: son las únicas que reservan la ranura desde que
    /// el índice único de `Code` es parcial. Una definición eliminada lógicamente sigue en la base
    /// —eso no cambia— pero su código vuelve a estar disponible, porque el código de un parámetro
    /// sale de un catálogo cerrado y no identifica una cosa del mundo real.
    /// </summary>
    Task<int> CountByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dependientes VIVOS (no eliminados lógicamente) de la definición. Son los que bloquean su
    /// borrado: una referencia ya eliminada lógicamente dejó de ser una referencia real.
    /// </summary>
    Task<int> CountLiveGlobalValuesAsync(Guid definitionId, CancellationToken cancellationToken = default);
    /// <inheritdoc cref="CountLiveGlobalValuesAsync"/>
    Task<int> CountLiveTenantValuesAsync(Guid definitionId, CancellationToken cancellationToken = default);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IParameterGlobalValueRepository
{
    /// <summary>No devuelve valores eliminados lógicamente (estado Deleted).</summary>
    Task<ParameterGlobalValue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// El valor VIVO de esa definición, si lo hay. No devuelve los eliminados lógicamente: el índice
    /// único sobre `ParameterDefinitionId` es parcial, así que una lápida no reserva la ranura y
    /// devolverla bloquearía un alta que la base permite. La fila eliminada sigue existiendo y se
    /// consulta con `IgnoreQueryFilters()` cuando lo que se quiere es el histórico.
    /// </summary>
    Task<ParameterGlobalValue?> GetByDefinitionIdAsync(Guid definitionId, CancellationToken cancellationToken = default);
    Task AddAsync(ParameterGlobalValue value, CancellationToken cancellationToken = default);
    Task UpdateAsync(ParameterGlobalValue value, CancellationToken cancellationToken = default);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IParameterTenantValueRepository
{
    /// <summary>No devuelve valores eliminados lógicamente (estado Deleted).</summary>
    Task<ParameterTenantValue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IParameterGlobalValueRepository.GetByDefinitionIdAsync"/>
    Task<ParameterTenantValue?> GetByTenantAndDefinitionAsync(Guid tenantId, Guid definitionId, CancellationToken cancellationToken = default);
    Task AddAsync(ParameterTenantValue value, CancellationToken cancellationToken = default);
    Task UpdateAsync(ParameterTenantValue value, CancellationToken cancellationToken = default);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IIdpConfigurationRepository : IAggregateRepository<IdpConfigurationAggregate>
{
    Task<IdpConfigurationAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IdpConfigurationAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IdpConfigurationAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
