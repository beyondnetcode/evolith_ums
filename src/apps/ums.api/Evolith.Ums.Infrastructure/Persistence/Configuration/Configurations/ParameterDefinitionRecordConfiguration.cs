using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration.Configurations;

public sealed class ParameterDefinitionRecordConfiguration : IEntityTypeConfiguration<ParameterDefinitionRecord>
{
    public void Configure(EntityTypeBuilder<ParameterDefinitionRecord> builder)
    {
        builder.ToTable("ParameterDefinitions", ConfigurationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();

        // Índice único PARCIAL sobre las definiciones VIVAS. El código de un parámetro es una ranura
        // del catálogo, no la identidad de una cosa del mundo real: si la lápida siguiera ocupando el
        // índice, retirar una definición prohibiría volver a declarar ese mismo parámetro para
        // siempre. La fila eliminada se conserva (ADR-0164 §2.1); lo que se libera es la ranura.
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.DefaultValue).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.DeletedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.ScopeId);
        builder.HasIndex(x => x.IsActive);
        // Toda lectura del catálogo filtra por IsDeleted (filtro global de consulta): sin índice,
        // ese predicado se paga con un recorrido completo en cada listado.
        builder.HasIndex(x => x.IsDeleted);
    }
}