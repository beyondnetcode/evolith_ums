using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Audit.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Audit.Configurations;

public sealed class AuditRecordRecordConfiguration : IEntityTypeConfiguration<AuditRecordRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecordRecord> builder)
    {
        builder.ToTable("AuditRecords", AuditPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.WhatChanged).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(255).IsRequired();
        builder.Property(x => x.AffectedEntityType).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Metadata).HasMaxLength(4000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        // Optimization Indices
        builder.HasIndex(x => x.WhoActed);
        builder.HasIndex(x => x.AffectedEntityId);
        builder.HasIndex(x => x.RootTenantId);
        builder.HasIndex(x => x.EventType);
        builder.HasIndex(x => new { x.AffectedEntityId, x.AffectedEntityType });
    }
}
