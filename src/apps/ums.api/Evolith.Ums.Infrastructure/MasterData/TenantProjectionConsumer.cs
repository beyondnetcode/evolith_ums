using Evolith.Ums.Contracts.MasterData;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Evolith.Ums.Infrastructure.MasterData;

/// <summary>
/// Projects MMS master-Tenant events into the local UMS read model (ADR-0083 / ADR-0106).
/// Idempotent and order-tolerant: the MassTransit inbox dedups by messageId, and the versioned
/// upsert applies only when the incoming sequence is newer than the stored version (stale /
/// out-of-order events are discarded). Deactivated/Deleted map to a soft-delete status.
/// </summary>
public sealed class TenantProjectionConsumer : IConsumer<TenantEvent>
{
    private readonly TenantProjectionDbContext _db;
    private readonly ILogger<TenantProjectionConsumer> _logger;

    public TenantProjectionConsumer(TenantProjectionDbContext db, ILogger<TenantProjectionConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TenantEvent> context)
    {
        var e = context.Message;
        var ct = context.CancellationToken;

        // DS-05: single atomic, monotonic upsert. The previous read-check-write had no concurrency
        // token: two in-flight events for the same tenant could both pass the version check and the
        // lower sequence commit last, permanently regressing the projection. The set-based
        // `ON CONFLICT ... WHERE Version < EXCLUDED.Version` applies only strictly-newer sequences
        // (stale / out-of-order discarded), atomically, and saves a round-trip. It runs inside the
        // MassTransit inbox transaction (see TenantProjectionConsumerDefinition, DS-04).
        // SQL Server no tiene `ON CONFLICT`. El equivalente atómico es MERGE, y necesita
        // WITH (HOLDLOCK): sin esa pista, dos MERGE concurrentes sobre la misma clave pueden
        // ambos no encontrar fila y ambos insertar, violando la PK — justo la carrera que este
        // upsert existe para cerrar. El WHEN MATCHED lleva la misma guarda de monotonía, así que
        // un evento rezagado no actualiza nada y @@ROWCOUNT queda en 0, igual que en Postgres.
        var applied = _db.Database.IsNpgsql()
            ? await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO masterdata.tenant_projection ("TenantId", "Code", "Name", "Status", "Version", "UpdatedAt")
                VALUES ({e.Subject}, {e.Data.Code}, {e.Data.Name}, {e.Data.Status}, {e.Sequence}, {e.Time})
                ON CONFLICT ("TenantId") DO UPDATE SET
                    "Code" = EXCLUDED."Code",
                    "Name" = EXCLUDED."Name",
                    "Status" = EXCLUDED."Status",
                    "Version" = EXCLUDED."Version",
                    "UpdatedAt" = EXCLUDED."UpdatedAt"
                WHERE masterdata.tenant_projection."Version" < EXCLUDED."Version"
                """, ct)
            : await _db.Database.ExecuteSqlInterpolatedAsync($"""
                MERGE masterdata.tenant_projection WITH (HOLDLOCK) AS destino
                USING (VALUES ({e.Subject}, {e.Data.Code}, {e.Data.Name}, {e.Data.Status}, {e.Sequence}, {e.Time}))
                    AS origen ("TenantId", "Code", "Name", "Status", "Version", "UpdatedAt")
                    ON destino."TenantId" = origen."TenantId"
                WHEN MATCHED AND destino."Version" < origen."Version" THEN
                    UPDATE SET
                        "Code"      = origen."Code",
                        "Name"      = origen."Name",
                        "Status"    = origen."Status",
                        "Version"   = origen."Version",
                        "UpdatedAt" = origen."UpdatedAt"
                WHEN NOT MATCHED THEN
                    INSERT ("TenantId", "Code", "Name", "Status", "Version", "UpdatedAt")
                    VALUES (origen."TenantId", origen."Code", origen."Name", origen."Status", origen."Version", origen."UpdatedAt");
                """, ct);

        if (applied == 0)
        {
            _logger.LogInformation(
                "Tenant projection: discarded stale/out-of-order {Type} seq={Seq} tenant={Tenant} corr={Corr}",
                e.Type, e.Sequence, e.Subject, e.CorrelationId);
            return;
        }

        _logger.LogInformation(
            "Tenant projection applied {Type} seq={Seq} tenant={Tenant} status={Status} corr={Corr}",
            e.Type, e.Sequence, e.Subject, e.Data.Status, e.CorrelationId);
    }
}
