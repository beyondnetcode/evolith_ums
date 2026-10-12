using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals.Configurations;

public sealed class AccessNotificationRecordConfiguration : IEntityTypeConfiguration<AccessNotificationRecord>
{


    public void Configure(EntityTypeBuilder<AccessNotificationRecord> builder)
    {
        builder.ToTable("UserDocumentNotifications", ApprovalsPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UserDocumentId);
        builder.HasIndex(x => new { x.UserDocumentId, x.Step }).IsUnique();
    }
}
