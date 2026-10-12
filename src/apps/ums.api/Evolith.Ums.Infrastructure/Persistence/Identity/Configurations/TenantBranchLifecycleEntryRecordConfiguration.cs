using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Configurations;

public sealed class TenantBranchLifecycleEntryRecordConfiguration : IEntityTypeConfiguration<TenantBranchLifecycleEntryRecord>
{
    public void Configure(EntityTypeBuilder<TenantBranchLifecycleEntryRecord> builder)
    {
        builder.ToTable("TenantBranchLifecycleEntries", IdentityPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActorId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(x => x.GeofencingSnapshot).HasMaxLength(4000);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.EpisodeId).IsRequired();
        builder.Property(x => x.OccurredAtUtc).IsRequired();

        // La consulta de la bitácora es siempre «los episodios de ESTA sucursal en orden»; el índice
        // compuesto la resuelve sin ordenar en memoria.
        builder.HasIndex(x => new { x.BranchId, x.OccurredAtUtc });

        // Aislamiento por inquilino: el filtro global y la política RLS filtran por TenantId.
        builder.HasIndex(x => x.TenantId);
    }
}
