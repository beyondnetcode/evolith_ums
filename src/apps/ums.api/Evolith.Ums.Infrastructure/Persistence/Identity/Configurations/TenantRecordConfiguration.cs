using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Configurations;

public sealed class TenantRecordConfiguration : IEntityTypeConfiguration<TenantRecord>
{
    public void Configure(EntityTypeBuilder<TenantRecord> builder)
    {
        builder.ToTable("Tenants", IdentityPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CompanyReference).HasMaxLength(100);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion(); // FIX-03: optimistic concurrency (PostgreSQL default applied centrally in UmsPlatformDbContext)
        builder.Property(x => x.IsManagementOwner).HasDefaultValue(false).IsRequired();

        // REC-16: Soft-delete
        builder.Property(x => x.IsDeleted).HasDefaultValue(false).IsRequired();
        builder.Property(x => x.DeletedBy).HasMaxLength(100);
        builder.HasIndex(x => x.IsDeleted).HasFilter("\"IsDeleted\" = false");

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.ParentTenantId);

        // ADR-0071 / FS-26 (G-025): invariante de un único propietario de gestión (Admin Root)
        // por ecosistema. Índice único filtrado: a lo sumo una fila con IsManagementOwner = true.
        builder.HasIndex(x => x.IsManagementOwner)
            .IsUnique()
            .HasFilter("\"IsManagementOwner\" = true")
            .HasDatabaseName("IX_Tenants_SingleManagementOwner");

        builder.HasMany(x => x.Branches)
            .WithOne(x => x.Tenant)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.IdentityProviders)
            .WithOne(x => x.Tenant)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
