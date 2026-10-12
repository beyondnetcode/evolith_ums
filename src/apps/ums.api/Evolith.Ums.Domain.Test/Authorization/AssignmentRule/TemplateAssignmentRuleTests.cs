namespace Evolith.Ums.Domain.Test.Authorization.AssignmentRule;

using Evolith.Ums.Domain.Authorization.AssignmentRule;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Xunit;

public class TemplateAssignmentRuleTests
{
    private static TenantId AnyTenant() => TenantId.Load(Guid.NewGuid());
    private static TemplateId AnyTemplate() => TemplateId.Load(Guid.NewGuid());
    private static RoleId AnyRole() => RoleId.Load(Guid.NewGuid());
    private static ActorId Actor() => ActorId.Create("admin-001");

    [Fact]
    public void Create_WithValidArgs_ReturnsActiveRule()
    {
        var result = TemplateAssignmentRule.Create(AnyTenant(), AnyTemplate(), AnyRole(), 10, Actor());

        Assert.True(result.IsSuccess);
        Assert.Equal(TemplateAssignmentRuleStatus.Active, result.Value.Status);
        Assert.Equal(10, result.Value.Priority);
    }

    [Fact]
    public void Create_WithZeroPriority_ReturnsFailure()
    {
        var result = TemplateAssignmentRule.Create(AnyTenant(), AnyTemplate(), AnyRole(), 0, Actor());

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.AssignmentRulePriorityMustBePositive, result.Error);
    }

    [Fact]
    public void Create_WithNegativePriority_ReturnsFailure()
    {
        var result = TemplateAssignmentRule.Create(AnyTenant(), AnyTemplate(), AnyRole(), -5, Actor());

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Deactivate_WhenActive_SetsStatusInactive()
    {
        var rule = TemplateAssignmentRule.Create(AnyTenant(), AnyTemplate(), AnyRole(), 1, Actor()).Value;

        var result = rule.Deactivate(Actor());

        Assert.True(result.IsSuccess);
        Assert.Equal(TemplateAssignmentRuleStatus.Inactive, rule.Status);
        Assert.False(rule.IsActive);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ReturnsFailure()
    {
        var rule = TemplateAssignmentRule.Create(AnyTenant(), AnyTemplate(), AnyRole(), 1, Actor()).Value;
        rule.Deactivate(Actor());

        var result = rule.Deactivate(Actor());

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.AssignmentRuleAlreadyInactive, result.Error);
    }

    [Fact]
    public void Reactivate_WhenInactive_SetsStatusActive()
    {
        var rule = TemplateAssignmentRule.Create(AnyTenant(), AnyTemplate(), AnyRole(), 1, Actor()).Value;
        rule.Deactivate(Actor());

        var result = rule.Reactivate(Actor());

        Assert.True(result.IsSuccess);
        Assert.Equal(TemplateAssignmentRuleStatus.Active, rule.Status);
        Assert.True(rule.IsActive);
    }

    [Fact]
    public void Reactivate_WhenAlreadyActive_ReturnsFailure()
    {
        var rule = TemplateAssignmentRule.Create(AnyTenant(), AnyTemplate(), AnyRole(), 1, Actor()).Value;

        var result = rule.Reactivate(Actor());

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.AssignmentRuleAlreadyActive, result.Error);
    }

    [Fact]
    public void Create_RaisesAssignmentRuleCreatedEvent()
    {
        var rule = TemplateAssignmentRule.Create(AnyTenant(), AnyTemplate(), AnyRole(), 5, Actor()).Value;

        var uncommitted = rule.DomainEvents.GetUncommittedChanges();
        Assert.Contains(uncommitted, e => e is AssignmentRuleCreatedEvent);
    }

    [Fact]
    public void Deactivate_RaisesAssignmentRuleDeactivatedEvent()
    {
        var rule = TemplateAssignmentRule.Create(AnyTenant(), AnyTemplate(), AnyRole(), 5, Actor()).Value;
        rule.DomainEvents.MarkChangesAsCommitted();

        rule.Deactivate(Actor());

        var uncommitted = rule.DomainEvents.GetUncommittedChanges();
        Assert.Contains(uncommitted, e => e is AssignmentRuleDeactivatedEvent);
    }
}
