using System.Reflection;
using BeyondNetCode.Shell.Ddd.ValueObjects.Audit;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.TenantSignupRequest;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;
using Microsoft.EntityFrameworkCore;
using TenantSignupRequestAggregate = Evolith.Ums.Domain.Identity.TenantSignupRequest.TenantSignupRequest;

namespace Evolith.Ums.Infrastructure.Persistence.Identity;

public sealed class PostgreSqlTenantSignupRequestRepository(UmsPlatformDbContext dbContext) : ITenantSignupRequestRepository, IUnitOfWork
{
    private readonly HashSet<TenantSignupRequestAggregate> _trackedAggregates = [];

    public IUnitOfWork UnitOfWork => this;

    public async Task<TenantSignupRequestAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.Set<TenantSignupRequestRecord>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public async Task<IReadOnlyList<TenantSignupRequestAggregate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var records = await dbContext.Set<TenantSignupRequestRecord>()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<TenantSignupRequestAggregate>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var records = await dbContext.Set<TenantSignupRequestRecord>()
            .Where(x => x.StatusId == TenantSignupRequestStatus.Pending.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public Task AddAsync(TenantSignupRequestAggregate aggregate, CancellationToken cancellationToken = default)
    {
        dbContext.Set<TenantSignupRequestRecord>().Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(TenantSignupRequestAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Set<TenantSignupRequestRecord>()
            .FirstOrDefaultAsync(x => x.Id == aggregate.GetId().GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"Tenant signup request {aggregate.GetId().GetValue()} does not exist.");

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
        catch (DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.FirstOrDefault();
            var id = (Guid)(entry?.Property("Id").CurrentValue ?? Guid.Empty);
            throw new ConcurrencyConflictException(entry?.Metadata.Name ?? "Unknown", id);
        }

        _trackedAggregates.Clear();
        return true;
    }

    public void Dispose() => dbContext.Dispose();

    private static TenantSignupRequestAggregate Rehydrate(TenantSignupRequestRecord record)
    {
        var audit = AuditValueObject.Load(new AuditProps
        {
            CreatedBy = record.CreatedBy,
            CreatedAt = record.CreatedAtUtc,
            UpdatedBy = record.UpdatedBy,
            UpdatedAt = record.UpdatedAtUtc,
            TimeSpan = record.AuditTimeSpan
        });

        var idValueObject = CreateIdValueObject(record.Id);
        var props = (TenantSignupRequestProps)Activator.CreateInstance(
            typeof(TenantSignupRequestProps),
            idValueObject,
            Name.Create(record.CompanyName),
            CompanyReference.Create(record.CompanyReference),
            Name.Create(record.ContactName),
            Email.Create(record.ContactEmail),
            DomainEnumerationMapper.FromValue<TenantSignupRequestStatus>(record.StatusId),
            record.ApprovedTenantId.HasValue ? TenantId.Load(record.ApprovedTenantId.Value) : null,
            audit)!;

        var aggregate = new TenantSignupRequestAggregate(props);
        aggregate.BrokenRules.Clear();
        return aggregate;
    }

    private static TenantSignupRequestRecord ToRecord(TenantSignupRequestAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new TenantSignupRequestRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            CompanyName = aggregate.CompanyName.GetValue(),
            CompanyReference = aggregate.CompanyReference.GetValue(),
            ContactName = aggregate.ContactName.GetValue(),
            ContactEmail = aggregate.ContactEmail.GetValue(),
            StatusId = aggregate.Status.Id,
            ApprovedTenantId = aggregate.ApprovedTenantId?.GetValue(),
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
        };
    }

    private static void Apply(TenantSignupRequestRecord target, TenantSignupRequestAggregate source)
    {
        var replacement = ToRecord(source);
        target.CompanyName = replacement.CompanyName;
        target.CompanyReference = replacement.CompanyReference;
        target.ContactName = replacement.ContactName;
        target.ContactEmail = replacement.ContactEmail;
        target.StatusId = replacement.StatusId;
        target.ApprovedTenantId = replacement.ApprovedTenantId;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;
    }

    private static object CreateIdValueObject(Guid id)
    {
        var idType = typeof(TenantSignupRequestProps).GetProperty(nameof(TenantSignupRequestProps.Id))!.PropertyType;
        var loadMethod = idType.GetMethod(
            "Load",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(Guid)],
            modifiers: null);

        if (loadMethod is null)
        {
            throw new InvalidOperationException($"Could not resolve IdValueObject.Load(Guid) for {idType.FullName}.");
        }

        return loadMethod.Invoke(null, new object[] { id })!;
    }
}
