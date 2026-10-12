using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Configurations;

public sealed class TenantBranchRecordConfiguration : IEntityTypeConfiguration<TenantBranchRecord>
{
    public void Configure(EntityTypeBuilder<TenantBranchRecord> builder)
    {
        builder.ToTable("TenantBranches", IdentityPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.GeofencingMetadata).HasMaxLength(4000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        // ADR-0164 §2.1: cierre definitivo.
        builder.Property(x => x.IsClosed).HasDefaultValue(false).IsRequired();
        builder.Property(x => x.ClosedBy).HasMaxLength(100);

        // ADR-0164 §2.3: el índice único NO se filtra por el estado de cierre. Es la mitad de base de
        // datos de la misma decisión que toma `Tenant.AddBranch`: el código de una sucursal cerrada
        // queda ocupado para siempre, así que un alta con ese código choca aquí igual que arriba y
        // ninguna consulta histórica queda ambigua.
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

        // Índice parcial que cubre el predicado de las LECTURAS de listado (`IsClosed = false`),
        // el mismo patrón que ya usan Tenants y UserAccounts para su borrado lógico.
        builder.HasIndex(x => x.IsClosed).HasFilter("\"IsClosed\" = false");

        // La bitácora cuelga de la sucursal. El borrado en cascada es teórico —la sucursal ya no se
        // borra nunca— pero se declara para que el modelo no dependa de esa promesa.
        builder.HasMany(x => x.LifecycleEntries)
            .WithOne(x => x.Branch)
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
