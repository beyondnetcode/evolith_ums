namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class TenantRepositoryBasicTests
{
    private readonly Guid _testTenantId = Guid.NewGuid();

    [Fact]
    public async Task AddAndRetrieveTenant_ReturnsCorrectData()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = CreateContext(options);

        var tenant = new TenantRecord
        {
            Id = Guid.NewGuid(),
            Code = "TEST-001",
            Name = "Test Company",
            StatusId = 0,
            CreatedBy = "test",
            CreatedAtUtc = DateTime.UtcNow,
            AuditTimeSpan = ""
        };

        await context.Tenants.AddAsync(tenant, ct);
        await context.SaveChangesAsync(ct);

        var retrieved = await context.Tenants.FindAsync([tenant.Id], ct);

        retrieved.Should().NotBeNull();
        retrieved!.Code.Should().Be("TEST-001");
        retrieved.Name.Should().Be("Test Company");
    }

    [Fact]
    public async Task QueryMultipleTenants_ReturnsAll()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = CreateContext(options);

        await context.Tenants.AddRangeAsync(
            new TenantRecord
            {
                Id = Guid.NewGuid(), Code = "A", Name = "Company A",
                StatusId = 0, CreatedBy = "test", CreatedAtUtc = DateTime.UtcNow,
                AuditTimeSpan = ""
            },
            new TenantRecord
            {
                Id = Guid.NewGuid(), Code = "B", Name = "Company B",
                StatusId = 0, CreatedBy = "test", CreatedAtUtc = DateTime.UtcNow,
                AuditTimeSpan = ""
            }
        );
        await context.SaveChangesAsync(ct);

        var tenants = await context.Tenants.ToListAsync(ct);

        tenants.Should().HaveCount(2);
    }

    private UmsPlatformDbContext CreateContext(DbContextOptions<UmsPlatformDbContext> options)
    {
        var tenantContext = new TestTenantContext(_testTenantId);
        return new UmsPlatformDbContext(options, tenantContext, new Moq.Mock<MediatR.IPublisher>().Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<UmsPlatformDbContext>.Instance);
    }

    private class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? OrganizationId  => tenantId;
        public Guid? OriginalTenantId => tenantId;
        public bool  IsInternalAdmin  => false;
        public void Initialize(Guid userTenantId, bool isInternalAdmin) { }
        public void SetOrganizationId(Guid organizationId) { }
        public void EnableCrossTenantAccess() { }
        public void DisableCrossTenantAccess() { }
    }
}

public class UserAccountRepositoryBasicTests
{
    private readonly Guid _testTenantId = Guid.NewGuid();

    [Fact]
    public async Task AddAndRetrieveUserAccount_ReturnsCorrectData()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = CreateContext(options);

        var user = new UserAccountRecord
        {
            Id = Guid.NewGuid(),
            TenantId = _testTenantId,
            Email = "test@example.com",
            StatusId = 0,
            CreatedBy = "test",
            CreatedAtUtc = DateTime.UtcNow,
            AuditTimeSpan = ""
        };

        await context.UserAccounts.AddAsync(user, ct);
        await context.SaveChangesAsync(ct);

        var retrieved = await context.UserAccounts.FindAsync([user.Id], ct);

        retrieved.Should().NotBeNull();
        retrieved!.Email.Should().Be("test@example.com");
        retrieved.TenantId.Should().Be(_testTenantId);
    }

    [Fact]
    public async Task QueryByEmail_ReturnsCorrectUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = CreateContext(options);

        var user = new UserAccountRecord
        {
            Id = Guid.NewGuid(),
            TenantId = _testTenantId,
            Email = "findme@example.com",
            StatusId = 0,
            CreatedBy = "test",
            CreatedAtUtc = DateTime.UtcNow,
            AuditTimeSpan = ""
        };

        await context.UserAccounts.AddAsync(user, ct);
        await context.SaveChangesAsync(ct);

        var found = await context.UserAccounts
            .FirstOrDefaultAsync(u => u.Email == "findme@example.com", ct);

        found.Should().NotBeNull();
        found!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task QueryByTenant_ReturnsTenantUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = CreateContext(options);

        await context.UserAccounts.AddRangeAsync(
            new UserAccountRecord
            {
                Id = Guid.NewGuid(), TenantId = _testTenantId,
                Email = "user1@test.com", StatusId = 0,
                CreatedBy = "test", CreatedAtUtc = DateTime.UtcNow,
                AuditTimeSpan = ""
            },
            new UserAccountRecord
            {
                Id = Guid.NewGuid(), TenantId = _testTenantId,
                Email = "user2@test.com", StatusId = 0,
                CreatedBy = "test", CreatedAtUtc = DateTime.UtcNow,
                AuditTimeSpan = ""
            },
            new UserAccountRecord
            {
                Id = Guid.NewGuid(), TenantId = Guid.NewGuid(),
                Email = "other-tenant@test.com", StatusId = 0,
                CreatedBy = "test", CreatedAtUtc = DateTime.UtcNow,
                AuditTimeSpan = ""
            }
        );
        await context.SaveChangesAsync(ct);

        var tenantUsers = await context.UserAccounts
            .Where(u => u.TenantId == _testTenantId)
            .ToListAsync(ct);

        tenantUsers.Should().HaveCount(2);
        tenantUsers.All(u => u.TenantId == _testTenantId).Should().BeTrue();
    }

    private UmsPlatformDbContext CreateContext(DbContextOptions<UmsPlatformDbContext> options)
    {
        var tenantContext = new TestTenantContext(_testTenantId);
        return new UmsPlatformDbContext(options, tenantContext, new Moq.Mock<MediatR.IPublisher>().Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<UmsPlatformDbContext>.Instance);
    }

    private class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? OrganizationId  => tenantId;
        public Guid? OriginalTenantId => tenantId;
        public bool  IsInternalAdmin  => false;
        public void Initialize(Guid userTenantId, bool isInternalAdmin) { }
        public void SetOrganizationId(Guid organizationId) { }
        public void EnableCrossTenantAccess() { }
        public void DisableCrossTenantAccess() { }
    }
}
