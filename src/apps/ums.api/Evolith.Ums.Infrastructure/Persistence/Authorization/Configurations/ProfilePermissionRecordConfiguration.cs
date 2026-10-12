using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Configurations;

public sealed class ProfilePermissionRecordConfiguration : IEntityTypeConfiguration<ProfilePermissionRecord>
{
    public void Configure(EntityTypeBuilder<ProfilePermissionRecord> builder)
    {
        builder.ToTable("ProfilePermissions", AuthorizationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.ProfileId);
        builder.HasIndex(x => new { x.ProfileId, x.TemplateId, x.ActionId, x.TargetId });

        // El compuesto anterior lidera por ProfileId: no cubre «cuántos perfiles usan esta
        // plantilla», la guarda que corre antes de despublicar o borrar una plantilla.
        builder.HasIndex(x => x.TemplateId)
               .HasDatabaseName("IX_ProfilePermissions_TemplateId");
    }
}
