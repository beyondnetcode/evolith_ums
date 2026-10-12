using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Configurations;

public sealed class PermissionTemplateRecordConfiguration : IEntityTypeConfiguration<PermissionTemplateRecord>
{
    public void Configure(EntityTypeBuilder<PermissionTemplateRecord> builder)
    {
        builder.ToTable("PermissionTemplates", AuthorizationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Version).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.RoleId, x.SystemSuiteId, x.Version }).IsUnique();

        builder.HasOne<SystemSuiteRecord>()
            .WithMany()
            .HasForeignKey(x => x.SystemSuiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RoleRecord>()
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Template)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
