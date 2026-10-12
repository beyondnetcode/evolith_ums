using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Configurations;

public sealed class ProfileRecordConfiguration : IEntityTypeConfiguration<ProfileRecord>
{
    public void Configure(EntityTypeBuilder<ProfileRecord> builder)
    {
        builder.ToTable("Profiles", AuthorizationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion(); // FIX-03: optimistic concurrency

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.UserId, "IX_Profiles_UserId");

        // Índice PARCIAL: el login pregunta siempre por los perfiles ACTIVOS de un usuario. Al
        // filtrar el índice por esa condición, las filas inactivas —que solo crecen— no ocupan
        // espacio en él ni ensucian el plan.
        builder.HasIndex(x => x.UserId, "IX_Profiles_UserId_Active")
               .HasFilter("\"IsActive\" = true");
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.RoleId, x.BranchId });

        builder.HasMany(x => x.Permissions)
            .WithOne(x => x.Profile)
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
