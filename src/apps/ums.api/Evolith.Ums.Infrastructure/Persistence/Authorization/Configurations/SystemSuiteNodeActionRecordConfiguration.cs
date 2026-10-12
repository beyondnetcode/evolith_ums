using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Configurations;

public sealed class SystemSuiteNodeActionRecordConfiguration : IEntityTypeConfiguration<SystemSuiteNodeActionRecord>
{
    public void Configure(EntityTypeBuilder<SystemSuiteNodeActionRecord> builder)
    {
        builder.ToTable("SystemSuiteNodeActions", AuthorizationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActionCode).HasMaxLength(100).IsRequired();

        // Una funcionalidad no se vincula dos veces al mismo nodo.
        builder.HasIndex(x => new { x.NodeId, x.ActionCode }).IsUnique();
    }
}
