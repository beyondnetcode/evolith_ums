using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Configurations;

public sealed class SystemSuiteAppSettingRecordConfiguration : IEntityTypeConfiguration<SystemSuiteAppSettingRecord>
{
    public void Configure(EntityTypeBuilder<SystemSuiteAppSettingRecord> builder)
    {
        builder.ToTable("SystemSuiteAppSettings", AuthorizationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ConfigKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ConfigValue).HasMaxLength(4000).IsRequired();

        // G-178: fail-closed también en la base. Las filas que ya existan quedan en `false`, que
        // es lo correcto: nadie decidió publicarlas.
        builder.Property(x => x.IsClientVisible).IsRequired().HasDefaultValue(false);

        builder.HasIndex(x => new { x.SystemSuiteId, x.ConfigKey, x.ScopeId }).IsUnique();
    }
}
