using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Configurations;

public sealed class TemplateAssignmentRuleRecordConfiguration : IEntityTypeConfiguration<TemplateAssignmentRuleRecord>
{
    public void Configure(EntityTypeBuilder<TemplateAssignmentRuleRecord> builder)
    {
        builder.ToTable("TemplateAssignmentRules", AuthorizationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.RoleId });
        builder.HasIndex(x => new { x.TenantId, x.Priority, x.StatusId });
    }
}
