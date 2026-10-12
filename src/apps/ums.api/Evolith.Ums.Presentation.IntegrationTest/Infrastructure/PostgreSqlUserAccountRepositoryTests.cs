namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Identity;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class PostgreSqlUserAccountRepositoryTests
{
    [Fact]
    public async Task GetByTenantAndEmailAsync_WithIncludeDeleted_ReturnsSoftDeletedUserForSameTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = CreateContext(options, tenantId);
        var repo = new PostgreSqlUserAccountRepository(context);

        var deletedUser = new UserAccountRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "seeded@example.com",
            CategoryId = 1,
            StatusId = 4,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow,
            DeletedBy = "seed",
            AnonymizedAtUtc = DateTime.UtcNow,
            CreatedBy = "seed",
            CreatedAtUtc = DateTime.UtcNow,
            AuditTimeSpan = ""
        };

        await context.UserAccounts.AddAsync(deletedUser, ct);
        await context.SaveChangesAsync(ct);

        var found = await repo.GetByTenantAndEmailAsync(
            tenantId,
            Evolith.Ums.Domain.Kernel.ValueObjects.Email.Create("seeded@example.com"),
            includeDeleted: true,
            cancellationToken: ct);

        found.Should().NotBeNull();
        found!.Props.Id.GetValue().Should().Be(deletedUser.Id);
        found.Props.TenantId.GetValue().Should().Be(tenantId);
    }

    [Fact]
    public async Task GetByTenantAndEmailAsync_DoesNotCrossTenants()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = CreateContext(options, tenantId);
        var repo = new PostgreSqlUserAccountRepository(context);

        await context.UserAccounts.AddRangeAsync(
            new UserAccountRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Email = "shared@example.com",
                CategoryId = 1,
                StatusId = 2,
                CreatedBy = "seed",
                CreatedAtUtc = DateTime.UtcNow,
                AuditTimeSpan = ""
            },
            new UserAccountRecord
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenantId,
                Email = "shared@example.com",
                CategoryId = 1,
                StatusId = 2,
                CreatedBy = "seed",
                CreatedAtUtc = DateTime.UtcNow,
                AuditTimeSpan = ""
            }
        );
        await context.SaveChangesAsync(ct);

        var found = await repo.GetByTenantAndEmailAsync(
            tenantId,
            Evolith.Ums.Domain.Kernel.ValueObjects.Email.Create("shared@example.com"),
            cancellationToken: ct);

        found.Should().NotBeNull();
        found!.Props.TenantId.GetValue().Should().Be(tenantId);
    }

    private static UmsPlatformDbContext CreateContext(DbContextOptions<UmsPlatformDbContext> options, Guid tenantId)
    {
        var tenantContext = new TestTenantContext(tenantId);
        return new UmsPlatformDbContext(options, tenantContext, new Moq.Mock<MediatR.IPublisher>().Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<UmsPlatformDbContext>.Instance);
    }

    private sealed class TestTenantContext(Guid tenantId) : Evolith.Ums.Application.Common.Interfaces.ITenantContext
    {
        public Guid? OrganizationId => tenantId;
        public Guid? OriginalTenantId => tenantId;
        public bool IsInternalAdmin => false;
        public void Initialize(Guid userTenantId, bool isInternalAdmin) { }
        public void SetOrganizationId(Guid organizationId) { }
        public void EnableCrossTenantAccess() { }
        public void DisableCrossTenantAccess() { }
    }
}
