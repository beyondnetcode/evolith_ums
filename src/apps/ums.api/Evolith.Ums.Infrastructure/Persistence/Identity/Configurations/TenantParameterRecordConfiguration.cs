using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Configurations;

public sealed class TenantParameterRecordConfiguration : IEntityTypeConfiguration<TenantParameterRecord>
{
    public void Configure(EntityTypeBuilder<TenantParameterRecord> builder)
    {
        builder.ToTable("TenantParameters", IdentityPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ValueTypeId).IsRequired();
        builder.Property(x => x.CategoryId).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        // REC-16 (mismo patrón que TenantRecord/UserAccountRecord): la marca de borrado lógico nace en
        // false para toda fila existente, así que la migración no reescribe datos.
        builder.Property(x => x.IsDeleted).HasDefaultValue(false).IsRequired();
        builder.Property(x => x.IsSensitive).IsRequired();
        builder.Property(x => x.DefaultValue).HasMaxLength(4000);
        builder.Property(x => x.AllowedValues).HasMaxLength(2000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        // El índice único parcial sigue cubriendo solo las filas ACTIVAS. Un parámetro eliminado
        // lógicamente está siempre inactivo (Delete exige desactivación previa), así que queda fuera
        // del índice y no impide dar de alta de nuevo el mismo código.
        builder.HasIndex(x => new { x.TenantId, x.Code, x.IsActive })
            .HasFilter("\"IsActive\" = true")
            .IsUnique()
            .HasDatabaseName("IX_TenantParameters_TenantId_Code_IsActive");

        // Índice parcial sobre las filas vivas: es el predicado que el filtro global añade a TODA
        // consulta de parámetros, igual que en Tenants y UserAccounts.
        builder.HasIndex(x => x.IsDeleted).HasFilter("\"IsDeleted\" = false");
    }
}