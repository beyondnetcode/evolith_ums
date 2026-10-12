using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals.Configurations;

public sealed class NotificationRuleRecordConfiguration : IEntityTypeConfiguration<NotificationRuleRecord>
{


    public void Configure(EntityTypeBuilder<NotificationRuleRecord> builder)
    {
        builder.ToTable("NotificationRules", ApprovalsPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Recipient).HasMaxLength(255).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.TenantId);
    }
}
