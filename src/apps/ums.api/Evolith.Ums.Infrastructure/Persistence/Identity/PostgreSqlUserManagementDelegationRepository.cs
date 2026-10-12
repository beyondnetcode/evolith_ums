using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserManagementDelegation;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;

namespace Evolith.Ums.Infrastructure.Persistence.Identity;

using UserManagementDelegationAggregate = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation;

public sealed class PostgreSqlUserManagementDelegationRepository(UmsPlatformDbContext dbContext)
    : IUserManagementDelegationRepository, IUnitOfWork
{
    private readonly HashSet<UserManagementDelegationAggregate> _trackedAggregates = [];

    public IUnitOfWork UnitOfWork => this;

    public async Task<UserManagementDelegationAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.UserManagementDelegations
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public Task<UserManagementDelegationAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public async Task<IReadOnlyList<UserManagementDelegationAggregate>> GetByDelegatedAdminAsync(Guid delegatedAdminId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.UserManagementDelegations
            .Where(x => x.DelegatedAdminId == delegatedAdminId && x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<UserManagementDelegationAggregate>> GetByDelegatingAdminAsync(Guid delegatingAdminId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.UserManagementDelegations
            .Where(x => x.DelegatingAdminId == delegatingAdminId && x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<UserManagementDelegationAggregate>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var activeStatusId = DelegationStatus.Active.Id;
        var records = await dbContext.UserManagementDelegations
            .Where(x => x.TenantId == tenantId && x.StatusId == activeStatusId)
            .OrderByDescending(x => x.ValidUntil)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<UserManagementDelegationAggregate>> GetExpiredActiveAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        var activeStatusId = DelegationStatus.Active.Id;
        var records = await dbContext.UserManagementDelegations
            .Where(x => x.StatusId == activeStatusId && x.ValidUntil <= asOf)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<UserManagementDelegationAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        IQueryable<UserManagementDelegationRecord> query = dbContext.UserManagementDelegations;

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var records = await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public Task AddAsync(UserManagementDelegationAggregate aggregate, CancellationToken cancellationToken = default)
    {
        dbContext.UserManagementDelegations.Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(UserManagementDelegationAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.UserManagementDelegations
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"Delegation {aggregate.Props.Id.GetValue()} does not exist.");

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

    private static UserManagementDelegationAggregate Rehydrate(UserManagementDelegationRecord record)
        => IdentityAggregateFactory.RehydrateUserManagementDelegation(record);

    private static UserManagementDelegationRecord ToRecord(UserManagementDelegationAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new UserManagementDelegationRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            TenantId = aggregate.Props.TenantId.GetValue(),
            DelegatingAdminId = aggregate.Props.DelegatingAdminId.GetValue(),
            DelegatedAdminId = aggregate.Props.DelegatedAdminId.GetValue(),
            ScopeTypeId = aggregate.Props.ScopeType.Id,
            ScopeId = aggregate.Props.ScopeId,
            AllowedActionsJson = JsonSerializer.Serialize(aggregate.Props.AllowedActions.Select(a => a.Id).ToList()),
            ValidFrom = aggregate.Props.ValidFrom,
            ValidUntil = aggregate.Props.ValidUntil,
            MaxDurationDays = aggregate.Props.MaxDurationDays,
            RequiresApproval = aggregate.Props.RequiresApproval,
            ApprovalRequestId = aggregate.Props.ApprovalRequestId,
            StatusId = aggregate.Props.Status.Id,
            RevokedAt = aggregate.Props.RevokedAt,
            RevokedBy = aggregate.Props.RevokedBy,
            RevocationReason = aggregate.Props.RevocationReason,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
        };
    }

    private static void Apply(UserManagementDelegationRecord target, UserManagementDelegationAggregate source)
    {
        var replacement = ToRecord(source);

        target.TenantId = replacement.TenantId;
        target.DelegatingAdminId = replacement.DelegatingAdminId;
        target.DelegatedAdminId = replacement.DelegatedAdminId;
        target.ScopeTypeId = replacement.ScopeTypeId;
        target.ScopeId = replacement.ScopeId;
        target.AllowedActionsJson = replacement.AllowedActionsJson;
        target.ValidFrom = replacement.ValidFrom;
        target.ValidUntil = replacement.ValidUntil;
        target.MaxDurationDays = replacement.MaxDurationDays;
        target.RequiresApproval = replacement.RequiresApproval;
        target.ApprovalRequestId = replacement.ApprovalRequestId;
        target.StatusId = replacement.StatusId;
        target.RevokedAt = replacement.RevokedAt;
        target.RevokedBy = replacement.RevokedBy;
        target.RevocationReason = replacement.RevocationReason;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;
    }
}
