using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration.Configurations;

public sealed class AppConfigurationRecordConfiguration : IEntityTypeConfiguration<AppConfigurationRecord>
{
    public void Configure(EntityTypeBuilder<AppConfigurationRecord> builder)
    {
        builder.ToTable("AppConfigurations", ConfigurationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion(); // FIX-03: optimistic concurrency

        // Índice único PARCIAL: la ranura (ámbito, código) la ocupa solo una configuración VIVA.
        // Una configuración eliminada lógicamente sale del índice —su fila permanece en la tabla,
        // que es lo que ADR-0164 §2.3 protege— y su código vuelve a poder configurarse. Sin este
        // filtro, borrar la configuración global de `MFA_REQUIRED_FOR_ADMIN` impedía volver a
        // configurar ese parámetro NUNCA, porque su código no lo inventa quien opera: viene de un
        // catálogo cerrado. Lo midió `app-configuration-state.spec.ts` (409 donde esperaba 201).
        //
        // Sucursales y SystemSuite NO cambian: ahí el código sí identifica algo del mundo real y la
        // regla original —clave ocupada para siempre— se mantiene por decisión explícita.
        builder.HasIndex(x => new { x.TenantId, x.SystemSuiteId, x.ModuleId, x.Code })
            .IsUnique()
            .HasFilter(ConfigurationPersistenceConstants.LiveRowIndexFilter);

        builder.HasIndex(x => x.ScopeId);
        builder.HasIndex(x => x.StatusId);
    }
}
