namespace Evolith.Ums.Domain.Test.Authorization.Role;

using Evolith.Ums.Domain.Authorization.Role;
using Xunit;

public sealed class RoleTests
{
    private static readonly TenantId ValidTenantId = TenantId.Load(Guid.NewGuid());
    private static readonly SystemSuiteId ValidSuiteId = SystemSuiteId.Load(Guid.NewGuid());
    private static readonly ActorId ValidActor = ActorId.Create("user-001");

    [Fact]
    public void Create_WithCatalogFields_ReturnsActiveRootRole()
    {
        var result = Role.Create(
            ValidTenantId,
            ValidSuiteId,
            Code.Create("SECURITY_ADMIN"),
            Name.Create("Security Administrator"),
            Description.Create("Manages authorization configuration"),
            null,
            0,
            1,
            ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal("SECURITY_ADMIN", result.Value.Code.GetValue());
        Assert.Equal("Security Administrator", result.Value.Value.GetValue());
        Assert.True(result.Value.IsActive);
        Assert.Contains(result.Value.DomainEvents.GetUncommittedChanges(), item => item is RoleCreatedEvent);
    }

    [Fact]
    public void Create_RootRoleWithChildLevel_ReturnsFailure()
    {
        var result = Role.Create(
            ValidTenantId,
            ValidSuiteId,
            Code.Create("INVALID_ROOT"),
            Name.Create("Invalid Root"),
            Description.Create(""),
            null,
            1,
            0,
            ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.RootRoleHierarchyLevelInvalid, result.Error);
    }

    [Fact]
    public void Deactivate_ActiveRole_SetsInactiveAndRaisesEvent()
    {
        var role = Role.Create(
            ValidTenantId,
            ValidSuiteId,
            Code.Create("OPERATOR"),
            Name.Create("Operator"),
            Description.Create(""),
            null,
            0,
            0,
            ValidActor).Value;

        var result = role.Deactivate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.False(role.IsActive);
        Assert.Contains(role.DomainEvents.GetUncommittedChanges(), item => item is RoleDeactivatedEvent);
    }

    [Fact]
    public void Deactivate_WhenHasActiveProfiles_ReturnsFailure()
    {
        var role = Role.Create(
            ValidTenantId,
            ValidSuiteId,
            Code.Create("OPERATOR"),
            Name.Create("Operator"),
            Description.Create(""),
            null,
            0,
            0,
            ValidActor).Value;

        var result = role.Deactivate(ValidActor, activeProfileCount: 1);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.RoleHasActiveProfiles, result.Error);
    }

    [Fact]
    public void Deactivate_WhenHasActiveChildRoles_ReturnsFailure()
    {
        var role = Role.Create(
            ValidTenantId,
            ValidSuiteId,
            Code.Create("OPERATOR"),
            Name.Create("Operator"),
            Description.Create(""),
            null,
            0,
            0,
            ValidActor).Value;

        var result = role.Deactivate(ValidActor, activeChildRoleCount: 1);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.RoleHasActiveChildRoles, result.Error);
    }
}
