using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.MasterData;

/// <summary>
/// Isolated context for the MMS Tenant projection (ADR-0083). Kept separate from the platform
/// context so the projection read model + the MassTransit inbox (idempotency) evolve
/// independently of UMS's write model.
/// </summary>
public sealed class TenantProjectionDbContext : DbContext
{
    public const string Schema = "masterdata";

    public TenantProjectionDbContext(DbContextOptions<TenantProjectionDbContext> options) : base(options) { }

    public DbSet<TenantProjectionRecord> Tenants => Set<TenantProjectionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var t = modelBuilder.Entity<TenantProjectionRecord>();
        t.ToTable("tenant_projection");
        t.HasKey(x => x.TenantId);
        t.Property(x => x.Code).HasMaxLength(64).IsRequired();
        t.Property(x => x.Name).HasMaxLength(256).IsRequired();
        t.Property(x => x.Status).HasMaxLength(32).IsRequired();

        // MassTransit EF inbox (consumer idempotency / dedup by messageId).
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Local, read-only projection of the master Tenant (source of truth is MMS).</summary>
public sealed class TenantProjectionRecord
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Status { get; set; } = default!;
    public long Version { get; set; }              // last applied sequence (ordering guard)
    public DateTimeOffset UpdatedAt { get; set; }
}
