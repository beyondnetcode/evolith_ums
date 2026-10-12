namespace Evolith.Ums.Infrastructure.Persistence.Configuration;

using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.Parameter;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;

public sealed class PostgreSqlParameterDefinitionRepository(UmsPlatformDbContext db)
    : IParameterDefinitionRepository
{
    public async Task<ParameterDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await db.ParameterDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return r is null ? null : ConfigurationAggregateFactory.RehydrateParameterDefinition(r);
    }

    public async Task<ParameterDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var upper = code.ToUpperInvariant();
        var r = await db.ParameterDefinitions.FirstOrDefaultAsync(x => x.Code == upper, cancellationToken);
        return r is null ? null : ConfigurationAggregateFactory.RehydrateParameterDefinition(r);
    }

    public async Task<IReadOnlyList<ParameterDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var records = await db.ParameterDefinitions.OrderBy(x => x.DisplayOrder).ToListAsync(cancellationToken);
        return records.Select(ConfigurationAggregateFactory.RehydrateParameterDefinition).ToList();
    }

    public async Task AddAsync(ParameterDefinition definition, CancellationToken cancellationToken = default)
        => await db.ParameterDefinitions.AddAsync(ToRecord(definition), cancellationToken);

    public async Task UpdateAsync(ParameterDefinition definition, CancellationToken cancellationToken = default)
    {
        var existing = await db.ParameterDefinitions
            .FirstOrDefaultAsync(x => x.Id == definition.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"ParameterDefinition {definition.Props.Id.GetValue()} not found.");
        Apply(existing, definition);
    }

    /// <inheritdoc/>
    public Task<int> CountByCodeAsync(string code, CancellationToken cancellationToken = default)
        // Cuenta solo las VIVAS: el filtro global `!IsDeleted` basta y ya no se apaga. El índice
        // único de `Code` pasó a ser parcial, así que una lápida no reserva el código; contarla
        // devolvería un 409 sobre una ranura que la base considera libre.
        => db.ParameterDefinitions
            .CountAsync(x => x.Code == code.ToUpperInvariant(), cancellationToken);

    /// <inheritdoc/>
    public Task<int> CountLiveGlobalValuesAsync(Guid definitionId, CancellationToken cancellationToken = default)
        => db.ParameterGlobalValues.CountAsync(
            x => x.ParameterDefinitionId == definitionId && x.StatusId != ConfigStatus.Deleted.Id,
            cancellationToken);

    /// <inheritdoc/>
    public Task<int> CountLiveTenantValuesAsync(Guid definitionId, CancellationToken cancellationToken = default)
        => db.ParameterTenantValues
            // IgnoreQueryFilters a propósito: la integridad referencial es una pregunta del SISTEMA,
            // no del inquilino en curso. Con el filtro de inquilino activo, un administrador interno
            // operando con X-Tenant-Id no vería los overrides de los demás inquilinos y borraría la
            // definición dejándolos huérfanos.
            .IgnoreQueryFilters()
            .CountAsync(
                x => x.ParameterDefinitionId == definitionId && x.StatusId != ConfigStatus.Deleted.Id,
                cancellationToken);

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ParameterDefinitionRecord ToRecord(ParameterDefinition definition)
    {
        var audit = definition.Props.Audit.GetValue();
        return new ParameterDefinitionRecord
        {
            Id            = definition.Props.Id.GetValue(),
            Code          = definition.Props.Code.GetValue(),
            Name          = definition.Props.Name.Value,
            Description   = definition.Props.Description.GetValue(),
            DataTypeId    = definition.Props.DataType.Id,
            DefaultValue  = definition.Props.DefaultValue.Value,
            ScopeId       = definition.Props.Scope.Id,
            IsActive      = definition.Props.IsActive,
            IsMandatory   = definition.Props.IsMandatory,
            DisplayOrder  = definition.Props.DisplayOrder,
            IsDeleted     = definition.Props.IsDeleted,
            Version       = definition.Props.Version,
            CreatedBy     = audit.CreatedBy,
            CreatedAtUtc  = audit.CreatedAt,
            UpdatedBy     = audit.UpdatedBy,
            UpdatedAtUtc  = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
        };
    }

    private static void Apply(ParameterDefinitionRecord t, ParameterDefinition definition)
    {
        var audit = definition.Props.Audit.GetValue();
        t.Name = definition.Props.Name.Value;
        t.Description = definition.Props.Description.GetValue();
        t.DefaultValue = definition.Props.DefaultValue.Value;
        t.ScopeId = definition.Props.Scope.Id;
        t.IsActive = definition.Props.IsActive;
        t.IsMandatory = definition.Props.IsMandatory;
        t.DisplayOrder = definition.Props.DisplayOrder;
        t.Version = definition.Props.Version;
        t.UpdatedBy = audit.UpdatedBy;
        t.UpdatedAtUtc = audit.UpdatedAt;
        t.AuditTimeSpan = audit.TimeSpan;

        // Sello de borrado lógico: se estampa una sola vez, en la transición. Si ya estaba puesto no
        // se reescribe, para que la fecha y el actor del borrado original no se pierdan.
        if (definition.Props.IsDeleted && !t.IsDeleted)
        {
            t.IsDeleted = true;
            t.DeletedAtUtc = audit.UpdatedAt ?? DateTime.UtcNow;
            t.DeletedBy = audit.UpdatedBy;
        }
    }
}

public sealed class PostgreSqlParameterGlobalValueRepository(UmsPlatformDbContext db)
    : IParameterGlobalValueRepository
{
    /// <inheritdoc/>
    public async Task<ParameterGlobalValue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await db.ParameterGlobalValues
            .FirstOrDefaultAsync(x => x.Id == id && x.StatusId != ConfigStatus.Deleted.Id, cancellationToken);
        return r is null ? null : ConfigurationAggregateFactory.RehydrateParameterGlobalValue(r);
    }

    /// <inheritdoc/>
    public async Task<ParameterGlobalValue?> GetByDefinitionIdAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        // Solo la fila VIVA: el índice único es ahora parcial, así que las lápidas no reservan la
        // ranura y no deben devolverse. Devolver una haría dos daños a la vez: bloquear un alta que
        // la base permite, y dar por «el valor global» uno que se retiró hace meses.
        var r = await db.ParameterGlobalValues
            .Where(x => x.ParameterDefinitionId == definitionId && x.StatusId != ConfigStatus.Deleted.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return r is null ? null : ConfigurationAggregateFactory.RehydrateParameterGlobalValue(r);
    }

    public async Task AddAsync(ParameterGlobalValue value, CancellationToken cancellationToken = default)
        => await db.ParameterGlobalValues.AddAsync(ToRecord(value), cancellationToken);

    public async Task UpdateAsync(ParameterGlobalValue value, CancellationToken cancellationToken = default)
    {
        var existing = await db.ParameterGlobalValues
            .FirstOrDefaultAsync(x => x.Id == value.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"ParameterGlobalValue {value.Props.Id.GetValue()} not found.");
        Apply(existing, value);
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ParameterGlobalValueRecord ToRecord(ParameterGlobalValue value)
    {
        var audit = value.Props.Audit.GetValue();
        return new ParameterGlobalValueRecord
        {
            Id                    = value.Props.Id.GetValue(),
            ParameterDefinitionId = value.Props.ParameterDefinitionId.GetValue(),
            EffectiveValue        = value.Props.Value.Value,
            StatusId              = value.Props.Status.Id,
            Version               = value.Props.Version,
            CreatedBy             = audit.CreatedBy,
            CreatedAtUtc          = audit.CreatedAt,
            UpdatedBy             = audit.UpdatedBy,
            UpdatedAtUtc          = audit.UpdatedAt,
            AuditTimeSpan         = audit.TimeSpan,
        };
    }

    private static void Apply(ParameterGlobalValueRecord t, ParameterGlobalValue value)
    {
        var audit = value.Props.Audit.GetValue();
        t.EffectiveValue = value.Props.Value.Value;
        t.StatusId       = value.Props.Status.Id;
        t.Version        = value.Props.Version;
        t.UpdatedBy      = audit.UpdatedBy;
        t.UpdatedAtUtc   = audit.UpdatedAt;
        t.AuditTimeSpan  = audit.TimeSpan;
    }
}

public sealed class PostgreSqlParameterTenantValueRepository(UmsPlatformDbContext db)
    : IParameterTenantValueRepository
{
    /// <inheritdoc/>
    public async Task<ParameterTenantValue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await db.ParameterTenantValues
            .FirstOrDefaultAsync(x => x.Id == id && x.StatusId != ConfigStatus.Deleted.Id, cancellationToken);
        return r is null ? null : ConfigurationAggregateFactory.RehydrateParameterTenantValue(r);
    }

    /// <inheritdoc/>
    public async Task<ParameterTenantValue?> GetByTenantAndDefinitionAsync(
        Guid tenantId, Guid definitionId, CancellationToken cancellationToken = default)
    {
        // Solo la fila VIVA, por la misma razón que en el valor global: el índice único parcial
        // (TenantId, ParameterDefinitionId) ya no cuenta las lápidas.
        var r = await db.ParameterTenantValues
            .Where(x => x.TenantId == tenantId
                && x.ParameterDefinitionId == definitionId
                && x.StatusId != ConfigStatus.Deleted.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return r is null ? null : ConfigurationAggregateFactory.RehydrateParameterTenantValue(r);
    }

    public async Task AddAsync(ParameterTenantValue value, CancellationToken cancellationToken = default)
        => await db.ParameterTenantValues.AddAsync(ToRecord(value), cancellationToken);

    public async Task UpdateAsync(ParameterTenantValue value, CancellationToken cancellationToken = default)
    {
        var existing = await db.ParameterTenantValues
            .FirstOrDefaultAsync(x => x.Id == value.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"ParameterTenantValue {value.Props.Id.GetValue()} not found.");
        Apply(existing, value);
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ParameterTenantValueRecord ToRecord(ParameterTenantValue value)
    {
        var audit = value.Props.Audit.GetValue();
        return new ParameterTenantValueRecord
        {
            Id                    = value.Props.Id.GetValue(),
            TenantId              = value.Props.TenantId.GetValue(),
            ParameterDefinitionId = value.Props.ParameterDefinitionId.GetValue(),
            OverrideValue         = value.Props.Value.Value,
            StatusId              = value.Props.Status.Id,
            Version               = value.Props.Version,
            CreatedBy             = audit.CreatedBy,
            CreatedAtUtc          = audit.CreatedAt,
            UpdatedBy             = audit.UpdatedBy,
            UpdatedAtUtc          = audit.UpdatedAt,
            AuditTimeSpan         = audit.TimeSpan,
        };
    }

    private static void Apply(ParameterTenantValueRecord t, ParameterTenantValue value)
    {
        var audit = value.Props.Audit.GetValue();
        t.OverrideValue = value.Props.Value.Value;
        t.StatusId      = value.Props.Status.Id;
        t.Version       = value.Props.Version;
        t.UpdatedBy     = audit.UpdatedBy;
        t.UpdatedAtUtc  = audit.UpdatedAt;
        t.AuditTimeSpan = audit.TimeSpan;
    }
}
