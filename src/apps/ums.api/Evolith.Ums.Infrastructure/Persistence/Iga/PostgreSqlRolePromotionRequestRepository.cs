using Microsoft.EntityFrameworkCore;
using BeyondNetCode.Shell.Ddd;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Domain.IGA.RolePromotionRequest;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence.Iga.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;

namespace Evolith.Ums.Infrastructure.Persistence.Iga;

using RolePromotionRequestAggregate = Evolith.Ums.Domain.IGA.RolePromotionRequest.RolePromotionRequest;

/// <summary>
/// Implementación PostgreSQL de <see cref="IRolePromotionRequestRepository"/> sobre
/// <see cref="UmsPlatformDbContext"/> (IGA, ADR-UMS-093). Mapea el agregado a/desde
/// <see cref="RolePromotionRequestRecord"/> y publica los eventos de transición de la máquina de
/// estados hacia el Outbox (ADR-0052) en <see cref="SaveEntitiesAsync"/> — así la traza de auditoría
/// append-only recibe cada transición sin infraestructura adicional.
/// </summary>
public sealed class PostgreSqlRolePromotionRequestRepository : IRolePromotionRequestRepository, IUnitOfWork
{
    private readonly UmsPlatformDbContext _dbContext;
    private readonly HashSet<RolePromotionRequestAggregate> _trackedAggregates = [];

    public PostgreSqlRolePromotionRequestRepository(UmsPlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IUnitOfWork UnitOfWork => this;

    public async Task<RolePromotionRequestAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.Set<RolePromotionRequestRecord>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : IgaAggregateFactory.RehydrateRolePromotionRequest(record);
    }

    public Task<RolePromotionRequestAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public async Task<IReadOnlyList<RolePromotionRequestAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<RolePromotionRequestRecord>().AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var records = await query.ToListAsync(cancellationToken);

        return records.Select(IgaAggregateFactory.RehydrateRolePromotionRequest).ToList();
    }

    public async Task<IReadOnlyList<RolePromotionRequestAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var records = await _dbContext.Set<RolePromotionRequestRecord>()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return records.Select(IgaAggregateFactory.RehydrateRolePromotionRequest).ToList();
    }

    public async Task<IReadOnlyList<RolePromotionRequestAggregate>> GetByTenantAndStatusAsync(Guid tenantId, string status, CancellationToken cancellationToken = default)
    {
        var statusEnum = DomainEnumeration.FromDisplayName<RolePromotionStatus>(status);
        if (statusEnum is null)
        {
            return Array.Empty<RolePromotionRequestAggregate>();
        }

        var records = await _dbContext.Set<RolePromotionRequestRecord>()
            .Where(x => x.TenantId == tenantId && x.StatusId == statusEnum.Id)
            .ToListAsync(cancellationToken);

        return records.Select(IgaAggregateFactory.RehydrateRolePromotionRequest).ToList();
    }

    public Task AddAsync(RolePromotionRequestAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<RolePromotionRequestRecord>().Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(RolePromotionRequestAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Set<RolePromotionRequestRecord>()
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"Role promotion request {aggregate.Props.Id.GetValue()} does not exist.");

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

    private static RolePromotionRequestRecord ToRecord(RolePromotionRequestAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new RolePromotionRequestRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            TenantId = aggregate.TenantId.GetValue(),
            TargetUserId = aggregate.TargetUserId.GetValue(),
            RequesterId = aggregate.RequesterId.GetValue(),
            CurrentRoleId = aggregate.CurrentRoleId.GetValue(),
            TargetRoleId = aggregate.TargetRoleId.GetValue(),
            StatusId = aggregate.Status.Id,
            RiskScore = aggregate.RiskScore?.GetValue(),
            ApproverId = aggregate.ApproverId?.GetValue(),
            SecurityReviewerId = aggregate.SecurityReviewerId?.GetValue(),
            ExecutorId = aggregate.ExecutorId?.GetValue(),
            VerifierId = aggregate.VerifierId?.GetValue(),
            DecisionReason = aggregate.DecisionReason,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
        };
    }

    private static void Apply(RolePromotionRequestRecord target, RolePromotionRequestAggregate source)
    {
        var replacement = ToRecord(source);

        target.TenantId = replacement.TenantId;
        target.TargetUserId = replacement.TargetUserId;
        target.RequesterId = replacement.RequesterId;
        target.CurrentRoleId = replacement.CurrentRoleId;
        target.TargetRoleId = replacement.TargetRoleId;
        target.StatusId = replacement.StatusId;
        target.RiskScore = replacement.RiskScore;
        target.ApproverId = replacement.ApproverId;
        target.SecurityReviewerId = replacement.SecurityReviewerId;
        target.ExecutorId = replacement.ExecutorId;
        target.VerifierId = replacement.VerifierId;
        target.DecisionReason = replacement.DecisionReason;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;
    }
}
