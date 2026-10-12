using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Configurations;

public sealed class UserAccountPasswordCredentialRecordConfiguration : IEntityTypeConfiguration<UserAccountPasswordCredentialRecord>
{
    public void Configure(EntityTypeBuilder<UserAccountPasswordCredentialRecord> builder)
    {
        builder.ToTable("UserAccountPasswordCredentials", IdentityPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.UserAccountId);
    }
}
