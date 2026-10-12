using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals;

using UserDocumentAggregate = Evolith.Ums.Domain.Approvals.UserDocument.UserDocument;

public sealed class PostgreSqlUserDocumentRepository : IUserDocumentRepository, IUnitOfWork
{
    private readonly UmsPlatformDbContext _dbContext;
    private readonly HashSet<UserDocumentAggregate> _trackedAggregates = [];

    public PostgreSqlUserDocumentRepository(UmsPlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IUnitOfWork UnitOfWork => this;

    public async Task<UserDocumentAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.Set<UserDocumentRecord>()
            .Include(x => x.Notifications)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public Task<UserDocumentAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public async Task<IReadOnlyList<UserDocumentAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var records = await _dbContext.Set<UserDocumentRecord>()
            .Include(x => x.Notifications)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<UserDocumentAggregate>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var records = await _dbContext.Set<UserDocumentRecord>()
            .Include(x => x.Notifications)
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public Task AddAsync(UserDocumentAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<UserDocumentRecord>().Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(UserDocumentAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Set<UserDocumentRecord>()
            .Include(x => x.Notifications)
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"User document {aggregate.Props.Id.GetValue()} does not exist.");

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

    public void Dispose() { }

    private static UserDocumentAggregate Rehydrate(UserDocumentRecord record)
        => ApprovalsAggregateFactory.RehydrateUserDocument(record, record.Notifications);

    private static UserDocumentRecord ToRecord(UserDocumentAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new UserDocumentRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            UserId = aggregate.UserId.GetValue(),
            DocumentTypeId = aggregate.DocumentTypeId.GetValue(),
            IssueDate = aggregate.IssueDate,
            ExpirationDate = aggregate.ExpirationDate,
            StatusId = aggregate.Status.Id,
            CriticityId = aggregate.Criticity.Id,
            FileStoragePath = aggregate.FileStoragePath.GetValue(),
            FileChecksum = aggregate.FileChecksum,
            NotificationStep = aggregate.NotificationStep,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
            Notifications = aggregate.Notifications.Select(n => new AccessNotificationRecord
            {
                Id = n.Props.Id.GetValue(),
                UserDocumentId = aggregate.Props.Id.GetValue(),
                Step = n.Step,
                ChannelId = n.Channel.Id,
                DaysRemaining = n.DaysRemaining,
                SentAt = n.SentAt
            }).ToList()
        };
    }

    private static void Apply(UserDocumentRecord target, UserDocumentAggregate source)
    {
        var replacement = ToRecord(source);

        target.UserId = replacement.UserId;
        target.DocumentTypeId = replacement.DocumentTypeId;
        target.IssueDate = replacement.IssueDate;
        target.ExpirationDate = replacement.ExpirationDate;
        target.StatusId = replacement.StatusId;
        target.CriticityId = replacement.CriticityId;
        target.FileStoragePath = replacement.FileStoragePath;
        target.FileChecksum = replacement.FileChecksum;
        target.NotificationStep = replacement.NotificationStep;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;

        // Sync notifications: add new entries; existing ones are immutable (append-only)
        var existingIds = target.Notifications.Select(n => n.Id).ToHashSet();
        foreach (var n in replacement.Notifications.Where(n => !existingIds.Contains(n.Id)))
        {
            target.Notifications.Add(n);
        }
    }
}
