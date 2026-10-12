using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Configurations;

public sealed class SystemSuiteNodeRecordConfiguration : IEntityTypeConfiguration<SystemSuiteNodeRecord>
{
    public void Configure(EntityTypeBuilder<SystemSuiteNodeRecord> builder)
    {
        builder.ToTable("SystemSuiteNodes", AuthorizationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        // Metadatos SDLC opcionales
        builder.Property(x => x.Responsable).HasMaxLength(200);
        builder.Property(x => x.Criticidad).HasMaxLength(50);
        builder.Property(x => x.ProductoImpactado).HasMaxLength(200);
        builder.Property(x => x.ComponenteTecnico).HasMaxLength(200);
        builder.Property(x => x.Dependencias).HasMaxLength(2000);
        builder.Property(x => x.Evidencias).HasMaxLength(2000);
        builder.Property(x => x.TrazabilidadSdlc).HasMaxLength(2000);

        // Código único entre hermanos (por módulo + padre). En Postgres los NULL
        // de ParentNodeId son distintos, así que la unicidad de raíces la garantiza
        // adicionalmente el dominio; el índice cubre consultas y ramas no-raíz.
        builder.Property(x => x.Icon).HasMaxLength(64);
        builder.Property(x => x.Route).HasMaxLength(400);

        builder.HasIndex(x => new { x.ModuleId, x.ParentNodeId, x.Code }).IsUnique();

        builder.HasOne(x => x.Module)
            .WithMany(m => m.Nodes)
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(x => x.ParentNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Actions)
            .WithOne(a => a.Node)
            .HasForeignKey(a => a.NodeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
