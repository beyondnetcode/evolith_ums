namespace Evolith.Ums.Infrastructure.Persistence.Seeders;

using Microsoft.Extensions.DependencyInjection;
using Evolith.Ums.Domain.Audit.AuditRecord;
using Evolith.Ums.Domain.Kernel.ValueObjects;

public static class AuditDevDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var auditRepository = serviceProvider.GetService<IAuditRecordRepository>();
        var inMemoryAuditRepository = serviceProvider.GetService<InMemoryAuditRecordRepository>();

        var ransaTenantId = TenantId.Load(Guid.Parse(CoreDevDataSeeder.RansaTenantId));

        var recordResult = AuditRecord.Record(
            Guid.Parse(CoreDevDataSeeder.SystemActorId),
            SubjectType.System,
            "TenantCreated",
            "TenantCreated",
            AuditResult.Success,
            ransaTenantId.GetValue(),
            "Tenant",
            ransaTenantId.GetValue(),
            "{\"Code\":\"RANSA_PERU\"}");

        if (recordResult.IsFailure) return;

        if (inMemoryAuditRepository is not null)
        {
            inMemoryAuditRepository.Seed(recordResult.Value);
            return;
        }

        if (auditRepository is null) return;

        var existing = await auditRepository.QueryByEventTypeAsync(
            "TenantCreated", ransaTenantId.GetValue(),
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(1),
            cancellationToken);

        if (existing.Count > 0) return;

        await auditRepository.AppendAsync(recordResult.Value, cancellationToken);
        await auditRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
    }
}
