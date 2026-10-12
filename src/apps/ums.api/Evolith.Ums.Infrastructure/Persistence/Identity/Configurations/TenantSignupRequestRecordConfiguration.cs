using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Configurations;

public sealed class TenantSignupRequestRecordConfiguration : IEntityTypeConfiguration<TenantSignupRequestRecord>
{
    public void Configure(EntityTypeBuilder<TenantSignupRequestRecord> builder)
    {
        builder.ToTable("TenantSignupRequests", IdentityPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CompanyReference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ContactName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ContactEmail).HasMaxLength(255).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.StatusId);
        builder.HasIndex(x => x.CompanyReference).IsUnique();
        builder.HasIndex(x => x.ContactEmail);
    }
}
