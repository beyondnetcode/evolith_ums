using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Evolith.Ums.Domain.Events;
using Evolith.Ums.ReadModels.Models;
using Evolith.Ums.ReadModels;
using Microsoft.EntityFrameworkCore;
namespace Evolith.Ums.ReadModels.Projections;

public sealed class PermissionTemplateProjectionHandler :
    INotificationHandler<PermissionTemplateCreatedEvent>,
    INotificationHandler<PermissionTemplateMutatedEvent>,
    INotificationHandler<PermissionTemplatePublishedEvent>,
    INotificationHandler<PermissionTemplateDeletedEvent>
{
    private readonly ReadModelDbContext _dbContext;

    public PermissionTemplateProjectionHandler(ReadModelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(PermissionTemplateCreatedEvent notification, CancellationToken cancellationToken)
    {
        var readModel = new PermissionTemplateReadModel
        {
            Id = notification.TemplateId,
            TenantId = notification.TenantId,
            RoleId = notification.RoleId,
            SystemSuiteId = notification.SystemSuiteId,
            Version = notification.Version,
            CreatedBy = "system",
            CreatedAtUtc = DateTime.UtcNow,
            AuditTimeSpan = "",
            Items = []
        };
        await _dbContext.Set<PermissionTemplateReadModel>().AddAsync(readModel, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(PermissionTemplateMutatedEvent notification, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Set<PermissionTemplateReadModel>()
            .FirstOrDefaultAsync(p => p.Id == notification.TemplateId, cancellationToken);
        if (entity != null)
        {
            entity.Version = notification.Version;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            entity.UpdatedBy = "system";
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task Handle(PermissionTemplatePublishedEvent notification, CancellationToken cancellationToken)
    {
        // For simplicity, treat publish as a version update (same as mutate)
        var entity = await _dbContext.Set<PermissionTemplateReadModel>()
            .FirstOrDefaultAsync(p => p.Id == notification.TemplateId, cancellationToken);
        if (entity != null)
        {
            entity.Version = notification.Version;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            entity.UpdatedBy = "system";
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task Handle(PermissionTemplateDeletedEvent notification, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Set<PermissionTemplateReadModel>()
            .FirstOrDefaultAsync(p => p.Id == notification.TemplateId, cancellationToken);
        if (entity != null)
        {
            _dbContext.Set<PermissionTemplateReadModel>().Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
