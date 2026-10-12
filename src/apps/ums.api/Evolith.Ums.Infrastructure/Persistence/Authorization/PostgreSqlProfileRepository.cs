using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization;

using ProfileAggregate = Evolith.Ums.Domain.Authorization.Profile.Profile;

public sealed class PostgreSqlProfileRepository(UmsPlatformDbContext dbContext) : IProfileRepository, IUnitOfWork
{
    private readonly HashSet<ProfileAggregate> _trackedAggregates = [];

    public IUnitOfWork UnitOfWork => this;

    public async Task<ProfileAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.Profiles
            .AsSplitQuery()
            .Include(x => x.Permissions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public async Task<ProfileAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.Profiles
            .AsSplitQuery()
            .Include(x => x.Permissions)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public async Task<IReadOnlyList<ProfileAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        IQueryable<ProfileRecord> query = dbContext.Profiles.AsSplitQuery().Include(x => x.Permissions);

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var records = await query.OrderBy(x => x.UserId).ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<ProfileAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.Profiles
            .AsSplitQuery()
            .Include(x => x.Permissions)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.UserId)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<ProfileAggregate>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.Profiles
            .AsSplitQuery()
            .Include(x => x.Permissions)
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.RoleId)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<ProfileAggregate>> GetActiveByUserAndTenantAsync(
        Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.Profiles
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Permissions)
            // El `IsActive == true` va explícito para que el planificador pueda usar el índice
            // parcial `IX_Profiles_UserId_Active`.
            .Where(x => x.UserId == userId && x.TenantId == tenantId && x.IsActive)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public Task AddAsync(ProfileAggregate aggregate, CancellationToken cancellationToken = default)
    {
        dbContext.Profiles.Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(ProfileAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Profiles
            .Include(x => x.Permissions)
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"Profile {aggregate.Props.Id.GetValue()} does not exist.");

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

    // ── Dependency guard queries ────────────────────────────────────────────

    public Task<int> CountActiveByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        => dbContext.Profiles.CountAsync(p => p.RoleId == roleId && p.IsActive, cancellationToken);

    public Task<int> CountActiveByTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
        => dbContext.ProfilePermissions
            .Where(pp => pp.TemplateId == templateId)
            .Select(pp => pp.ProfileId)
            .Distinct()
            .Join(dbContext.Profiles.Where(p => p.IsActive),
                  ppId => ppId, p => p.Id,
                  (_, p) => p.Id)
            .CountAsync(cancellationToken);

    public Task<int> CountActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => dbContext.Profiles.CountAsync(p => p.UserId == userId && p.IsActive, cancellationToken);

    // ADR-0164 §2.2: guarda de cascada del cierre de sucursal. Solo los ACTIVOS bloquean; un perfil
    // ya desactivado es una referencia muerta y no impide nada.
    public Task<int> CountActiveByBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
        => dbContext.Profiles.CountAsync(p => p.BranchId == branchId && p.IsActive, cancellationToken);

    private static ProfileAggregate Rehydrate(ProfileRecord record)
        => AuthorizationAggregateFactory.RehydrateProfile(record, record.Permissions);

    private static ProfileRecord ToRecord(ProfileAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new ProfileRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            TenantId = aggregate.Props.TenantId.GetValue(),
            UserId = aggregate.Props.UserId.GetValue(),
            RoleId = aggregate.Props.RoleId.GetValue(),
            BranchId = aggregate.Props.BranchId?.GetValue(),
            ScopeId = aggregate.Scope.Id,
            IsActive = aggregate.IsActive,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
            Permissions = aggregate.Permissions.Select(permission =>
            {
                var a = permission.Props.Audit.GetValue();
                return new ProfilePermissionRecord
                {
                    Id = permission.Props.Id.GetValue(),
                    ProfileId = permission.Props.ProfileId.GetValue(),
                    TemplateId = permission.Props.TemplateId.GetValue(),
                    TargetTypeId = permission.TargetType.Id,
                    TargetId = permission.TargetId.GetValue(),
                    ActionId = permission.ActionId.GetValue(),
                    IsAllowed = permission.IsAllowed,
                    IsDenied = permission.IsDenied,
                    IsActive = permission.IsActive,
                    IsOverride = permission.IsOverride,
                    CreatedBy = a.CreatedBy,
                    CreatedAtUtc = a.CreatedAt,
                    UpdatedBy = a.UpdatedBy,
                    UpdatedAtUtc = a.UpdatedAt,
                    AuditTimeSpan = a.TimeSpan,
                };
            }).ToList(),
        };
    }

    private void Apply(ProfileRecord target, ProfileAggregate source)
    {
        var replacement = ToRecord(source);

        target.TenantId = replacement.TenantId;
        target.UserId = replacement.UserId;
        target.RoleId = replacement.RoleId;
        target.BranchId = replacement.BranchId;
        target.ScopeId = replacement.ScopeId;
        target.IsActive = replacement.IsActive;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;

        EfChildCollectionReconciler.ReconcileById(
            dbContext,
            target.Permissions,
            replacement.Permissions,
            permission => permission.Id,
            UpdatePermission);
    }

    private static void UpdatePermission(ProfilePermissionRecord target, ProfilePermissionRecord source)
    {
        target.ProfileId = source.ProfileId;
        target.TemplateId = source.TemplateId;
        target.TargetTypeId = source.TargetTypeId;
        target.TargetId = source.TargetId;
        target.ActionId = source.ActionId;
        target.IsAllowed = source.IsAllowed;
        target.IsDenied = source.IsDenied;
        target.IsActive = source.IsActive;
        target.IsOverride = source.IsOverride;
        target.CreatedBy = source.CreatedBy;
        target.CreatedAtUtc = source.CreatedAtUtc;
        target.UpdatedBy = source.UpdatedBy;
        target.UpdatedAtUtc = source.UpdatedAtUtc;
        target.AuditTimeSpan = source.AuditTimeSpan;
    }
}
