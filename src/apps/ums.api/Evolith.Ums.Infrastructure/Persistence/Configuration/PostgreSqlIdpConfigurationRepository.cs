using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration;

using IdpConfigurationAggregate = Evolith.Ums.Domain.Configuration.IdpConfiguration.IdpConfiguration;

public sealed class PostgreSqlIdpConfigurationRepository(UmsPlatformDbContext dbContext) : IIdpConfigurationRepository, IUnitOfWork
{
    private readonly HashSet<IdpConfigurationAggregate> _trackedAggregates = [];

    public IUnitOfWork UnitOfWork => this;

    public async Task<IdpConfigurationAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.IdpConfigurations
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public Task<IdpConfigurationAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public async Task<IReadOnlyList<IdpConfigurationAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        IQueryable<IdpConfigurationRecord> query = dbContext.IdpConfigurations;

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var records = await query.OrderBy(x => x.TenantId).ThenBy(x => x.ResolutionPriority).ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<IdpConfigurationAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.IdpConfigurations
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.ResolutionPriority)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public Task AddAsync(IdpConfigurationAggregate aggregate, CancellationToken cancellationToken = default)
    {
        dbContext.IdpConfigurations.Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(IdpConfigurationAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.IdpConfigurations
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"IdpConfiguration {aggregate.Props.Id.GetValue()} does not exist.");

        Apply(existing, aggregate);
        _trackedAggregates.Add(aggregate);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var aggregate in _trackedAggregates)
        {
            await dbContext.PublishDomainEventsAsync(aggregate.DomainEvents.GetUncommittedChanges(), cancellationToken);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            // FIX-03: surface optimistic concurrency failures as 409 Conflict
            var entry = ex.Entries.FirstOrDefault();
            var id = (Guid)(entry?.Property("Id").CurrentValue ?? Guid.Empty);
            throw new ConcurrencyConflictException(entry?.Metadata.Name ?? "Unknown", id);
        }

        foreach (var aggregate in _trackedAggregates)
        {
            aggregate.DomainEvents.MarkChangesAsCommitted();
        }

        _trackedAggregates.Clear();
        return true;
    }

    public void Dispose() => dbContext.Dispose();

    private static IdpConfigurationAggregate Rehydrate(IdpConfigurationRecord record)
        => ConfigurationAggregateFactory.RehydrateIdpConfiguration(record);

    private static IdpConfigurationRecord ToRecord(IdpConfigurationAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new IdpConfigurationRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            TenantId = aggregate.Props.TenantId.GetValue(),
            SystemSuiteId = aggregate.Props.SystemSuiteId.GetValue(),
            ProviderTypeId = aggregate.Props.ProviderType.Id,
            DomainHintsJson = JsonSerializer.Serialize(aggregate.Props.DomainHints),
            ConfigPayload = aggregate.Props.ConfigPayload,
            SecretRef = aggregate.Props.SecretRef,
            StatusId = aggregate.Props.Status.Id,
            ResolutionPriority = aggregate.Props.ResolutionPriority,
            FallbackToId = aggregate.Props.FallbackToId,
            Version = aggregate.Props.Version,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
        };
    }

    private static void Apply(IdpConfigurationRecord target, IdpConfigurationAggregate source)
    {
        var replacement = ToRecord(source);

        target.TenantId = replacement.TenantId;
        target.SystemSuiteId = replacement.SystemSuiteId;
        target.ProviderTypeId = replacement.ProviderTypeId;
        target.DomainHintsJson = replacement.DomainHintsJson;
        target.ConfigPayload = replacement.ConfigPayload;
        target.SecretRef = replacement.SecretRef;
        target.StatusId = replacement.StatusId;
        target.ResolutionPriority = replacement.ResolutionPriority;
        target.FallbackToId = replacement.FallbackToId;
        target.Version = replacement.Version;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;
    }
}
