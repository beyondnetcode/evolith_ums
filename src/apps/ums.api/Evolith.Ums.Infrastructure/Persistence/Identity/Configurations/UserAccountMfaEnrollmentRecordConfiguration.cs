using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Configurations;

public sealed class UserAccountMfaEnrollmentRecordConfiguration : IEntityTypeConfiguration<UserAccountMfaEnrollmentRecord>
{
    public void Configure(EntityTypeBuilder<UserAccountMfaEnrollmentRecord> builder)
    {
        builder.ToTable("UserAccountMfaEnrollments", IdentityPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => new { x.UserAccountId, x.MethodId });
    }
}
