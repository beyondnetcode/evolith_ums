using Evolith.Ums.ReadModels.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Evolith.Ums.ReadModels;

public sealed class ReadModelDbContext : DbContext
{
    public ReadModelDbContext(DbContextOptions<ReadModelDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    public DbSet<PermissionTemplateReadModel> PermissionTemplates { get; set; } = null!;
    public DbSet<PermissionTemplateItemReadModel> PermissionTemplateItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Map to the same schema as the write model (Authorization)
        modelBuilder.HasDefaultSchema("Authorization");

        modelBuilder.Entity<PermissionTemplateReadModel>(builder =>
        {
            builder.ToTable("PermissionTemplateReadModels");
            builder.HasKey(x => x.Id);
            builder.HasMany(x => x.Items)
                   .WithOne()
                   .HasForeignKey(x => x.TemplateId)
                   .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PermissionTemplateItemReadModel>(builder =>
        {
            builder.ToTable("PermissionTemplateItemReadModels");
            builder.HasKey(x => x.Id);
        });
    }
}
