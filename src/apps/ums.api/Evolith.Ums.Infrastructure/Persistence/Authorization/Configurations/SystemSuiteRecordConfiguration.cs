using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Configurations;

public sealed class SystemSuiteRecordConfiguration : IEntityTypeConfiguration<SystemSuiteRecord>
{
    public void Configure(EntityTypeBuilder<SystemSuiteRecord> builder)
    {
        builder.ToTable("SystemSuites", AuthorizationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

        builder.HasMany(x => x.Modules)
            .WithOne(x => x.SystemSuite)
            .HasForeignKey(x => x.SystemSuiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.AppSettings)
            .WithOne(x => x.SystemSuite)
            .HasForeignKey(x => x.SystemSuiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Actions)
            .WithOne(x => x.SystemSuite)
            .HasForeignKey(x => x.SystemSuiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.DomainResources)
            .WithOne(x => x.SystemSuite)
            .HasForeignKey(x => x.SystemSuiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
