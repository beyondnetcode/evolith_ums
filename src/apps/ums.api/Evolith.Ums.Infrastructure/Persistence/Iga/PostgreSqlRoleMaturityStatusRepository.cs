using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence.Iga.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;

namespace Evolith.Ums.Infrastructure.Persistence.Iga;

using RoleMaturityStatusAggregate = Evolith.Ums.Domain.IGA.RoleMaturityStatus.RoleMaturityStatus;

/// <summary>
/// Implementación PostgreSQL de <see cref="IRoleMaturityStatusRepository"/> sobre
/// <see cref="UmsPlatformDbContext"/> (IGA, ADR-UMS-093). Mapea el agregado a/desde
/// <see cref="RoleMaturityStatusRecord"/> y publica los eventos de dominio hacia el Outbox
/// (ADR-0052) en <see cref="SaveEntitiesAsync"/>. El aislamiento por inquilino lo garantiza el
/// filtro global del contexto; las consultas acotadas añaden además el filtro explícito.
/// </summary>
public sealed class PostgreSqlRoleMaturityStatusRepository : IRoleMaturityStatusRepository, IUnitOfWork
{
    private readonly UmsPlatformDbContext _dbContext;
    private readonly HashSet<RoleMaturityStatusAggregate> _trackedAggregates = [];

    public PostgreSqlRoleMaturityStatusRepository(UmsPlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IUnitOfWork UnitOfWork => this;

    public async Task<RoleMaturityStatusAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.Set<RoleMaturityStatusRecord>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : IgaAggregateFactory.RehydrateRoleMaturityStatus(record);
    }

    public Task<RoleMaturityStatusAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public async Task<IReadOnlyList<RoleMaturityStatusAggregate>> GetByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var records = await _dbContext.Set<RoleMaturityStatusRecord>()
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .ToListAsync(cancellationToken);

        return records.Select(IgaAggregateFactory.RehydrateRoleMaturityStatus).ToList();
    }

    public async Task<RoleMaturityStatusAggregate?> GetByUserAndRoleAsync(Guid tenantId, Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.Set<RoleMaturityStatusRecord>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId && x.RoleId == roleId, cancellationToken);

        return record is null ? null : IgaAggregateFactory.RehydrateRoleMaturityStatus(record);
    }

    public async Task<IReadOnlyList<RoleMaturityStatusAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<RoleMaturityStatusRecord>().AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var records = await query.ToListAsync(cancellationToken);

        return records.Select(IgaAggregateFactory.RehydrateRoleMaturityStatus).ToList();
    }

    public Task AddAsync(RoleMaturityStatusAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<RoleMaturityStatusRecord>().Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(RoleMaturityStatusAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Set<RoleMaturityStatusRecord>()
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"Role maturity status {aggregate.Props.Id.GetValue()} does not exist.");

        Apply(existing, aggregate);
        _trackedAggregates.Add(aggregate);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var aggregate in _trackedAggregates)
        {
            await _dbContext.PublishDomainEventsAsync(aggregate.DomainEvents.GetUncommittedChanges(), cancellationToken);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
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

    public void Dispose()
    {
    }

    private static RoleMaturityStatusRecord ToRecord(RoleMaturityStatusAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new RoleMaturityStatusRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            TenantId = aggregate.TenantId.GetValue(),
            UserId = aggregate.UserId.GetValue(),
            RoleId = aggregate.RoleId.GetValue(),
            CurrentMaturityLevel = (int)aggregate.CurrentMaturityLevel,
            NextEligibleMaturityLevel = aggregate.NextEligibleMaturityLevel.HasValue ? (int)aggregate.NextEligibleMaturityLevel.Value : null,
            AssignedAt = aggregate.AssignedAt,
            CurrentLevelSince = aggregate.CurrentLevelSince,
            EligibleForPromotionAt = aggregate.EligibleForPromotionAt,
            CompletedCertificationsCount = aggregate.CompletedCertificationsCount,
            CompletedTrainingsCount = aggregate.CompletedTrainingsCount,
            PerformanceScore = aggregate.PerformanceScore,
            HasNoComplianceIssues = aggregate.HasNoComplianceIssues,
            BlockingFactor = aggregate.BlockingFactor?.GetValue(),
            LastReviewedAt = aggregate.LastReviewedAt,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
        };
    }

    private static void Apply(RoleMaturityStatusRecord target, RoleMaturityStatusAggregate source)
    {
        var replacement = ToRecord(source);

        target.TenantId = replacement.TenantId;
        target.UserId = replacement.UserId;
        target.RoleId = replacement.RoleId;
        target.CurrentMaturityLevel = replacement.CurrentMaturityLevel;
        target.NextEligibleMaturityLevel = replacement.NextEligibleMaturityLevel;
        target.AssignedAt = replacement.AssignedAt;
        target.CurrentLevelSince = replacement.CurrentLevelSince;
        target.EligibleForPromotionAt = replacement.EligibleForPromotionAt;
        target.CompletedCertificationsCount = replacement.CompletedCertificationsCount;
        target.CompletedTrainingsCount = replacement.CompletedTrainingsCount;
        target.PerformanceScore = replacement.PerformanceScore;
        target.HasNoComplianceIssues = replacement.HasNoComplianceIssues;
        target.BlockingFactor = replacement.BlockingFactor;
        target.LastReviewedAt = replacement.LastReviewedAt;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;
    }
}
