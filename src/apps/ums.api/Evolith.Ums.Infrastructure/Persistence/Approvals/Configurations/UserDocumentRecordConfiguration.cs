using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals.Configurations;

public sealed class UserDocumentRecordConfiguration : IEntityTypeConfiguration<UserDocumentRecord>
{


    public void Configure(EntityTypeBuilder<UserDocumentRecord> builder)
    {
        builder.ToTable("UserDocuments", ApprovalsPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileStoragePath).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.FileChecksum).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasMany(x => x.Notifications)
               .WithOne(x => x.UserDocument)
               .HasForeignKey(x => x.UserDocumentId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.UserId, x.DocumentTypeId });
        builder.HasIndex(x => x.ExpirationDate);
        builder.HasIndex(x => x.StatusId);
    }
}
