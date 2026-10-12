using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals;

using ApprovalRequestAggregate = Evolith.Ums.Domain.Approvals.ApprovalRequest.ApprovalRequest;

public sealed class PostgreSqlApprovalRequestRepository : IApprovalRequestRepository, IUnitOfWork
{
    private readonly UmsPlatformDbContext _dbContext;
    private readonly HashSet<ApprovalRequestAggregate> _trackedAggregates = [];

    public PostgreSqlApprovalRequestRepository(UmsPlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IUnitOfWork UnitOfWork => this;

    public async Task<ApprovalRequestAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.Set<ApprovalRequestRecord>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public Task<ApprovalRequestAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public async Task<IReadOnlyList<ApprovalRequestAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<ApprovalRequestRecord>().AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Join(_dbContext.Set<ApprovalWorkflowRecord>(),
                req => req.WorkflowId,
                wf => wf.Id,
                (req, wf) => req)
            .Where(x => _dbContext.Set<ApprovalWorkflowRecord>()
                .Any(wf => wf.Id == x.WorkflowId && wf.TenantId == tenantId.Value));
        }

        var records = await query.ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<ApprovalRequestAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Join with ApprovalWorkflow to filter by TenantId
        var records = await _dbContext.Set<ApprovalRequestRecord>()
            .Join(_dbContext.Set<ApprovalWorkflowRecord>(),
                  req => req.WorkflowId,
                  wf => wf.Id,
                  (req, wf) => new { req, wf })
            .Where(x => x.wf.TenantId == tenantId)
            .Select(x => x.req)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public Task AddAsync(ApprovalRequestAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<ApprovalRequestRecord>().Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(ApprovalRequestAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Set<ApprovalRequestRecord>()
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"Approval request {aggregate.Props.Id.GetValue()} does not exist.");

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
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
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

    private static ApprovalRequestAggregate Rehydrate(ApprovalRequestRecord record)
        => ApprovalsAggregateFactory.RehydrateRequest(record);

    public async Task<bool> ExistsPendingForScopeAsync(
        Guid userId, Guid systemId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<ApprovalRequestRecord>()
            .AnyAsync(x =>
                x.TargetUserId == userId &&
                x.RequestedSystemId == systemId &&
                x.RequestedBranchId == branchId &&
                x.StatusId == ApprovalStatus.Pending.Id,
                cancellationToken);
    }

    private static ApprovalRequestRecord ToRecord(ApprovalRequestAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new ApprovalRequestRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            WorkflowId = aggregate.WorkflowId.GetValue(),
            TargetUserId = aggregate.TargetUserId.GetValue(),
            TargetProfileId = aggregate.TargetProfileId?.GetValue(),
            StatusId = aggregate.Status.Id,
            RequestedSystemId = aggregate.RequestedSystemId.GetValue(),
            RequestedBranchId = aggregate.RequestedBranchId?.GetValue(),
            RequestedRoleId = aggregate.RequestedRoleId.GetValue(),
            Justification = aggregate.Justification,
            GrantedRoleId = aggregate.GrantedRoleId?.GetValue(),
            DecisionReason = aggregate.DecisionReason,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan
        };
    }

    private static void Apply(ApprovalRequestRecord target, ApprovalRequestAggregate source)
    {
        var replacement = ToRecord(source);

        target.WorkflowId = replacement.WorkflowId;
        target.TargetUserId = replacement.TargetUserId;
        target.TargetProfileId = replacement.TargetProfileId;
        target.StatusId = replacement.StatusId;
        target.RequestedSystemId = replacement.RequestedSystemId;
        target.RequestedBranchId = replacement.RequestedBranchId;
        target.RequestedRoleId = replacement.RequestedRoleId;
        target.Justification = replacement.Justification;
        target.GrantedRoleId = replacement.GrantedRoleId;
        target.DecisionReason = replacement.DecisionReason;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;
    }
}
