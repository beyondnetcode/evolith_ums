using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration.Configurations;

public sealed class ParameterGlobalValueRecordConfiguration : IEntityTypeConfiguration<ParameterGlobalValueRecord>
{
    public void Configure(EntityTypeBuilder<ParameterGlobalValueRecord> builder)
    {
        builder.ToTable("ParameterGlobalValues", ConfigurationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EffectiveValue).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        // Índice único PARCIAL: una definición tiene como mucho UN valor global vivo. El valor
        // eliminado se queda en la tabla como historia —explica por qué el sistema se comportó como
        // se comportó— pero deja de reservar la ranura, así que el parámetro puede volver a recibir
        // un valor global. Sin el filtro, borrar el valor global era irreversible.
        builder.HasIndex(x => x.ParameterDefinitionId)
            .IsUnique()
            .HasFilter(ConfigurationPersistenceConstants.LiveRowIndexFilter);

        builder.HasIndex(x => x.StatusId);
    }
}