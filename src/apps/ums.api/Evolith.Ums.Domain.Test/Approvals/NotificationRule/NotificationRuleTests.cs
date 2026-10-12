namespace Evolith.Ums.Domain.Test.Approvals.NotificationRule;

using Evolith.Ums.Domain.Approvals.NotificationRule;
using Xunit;

public class NotificationRuleTests
{
    private static readonly TenantId ValidTenantId = TenantId.Load(Guid.NewGuid().ToString());
    private static readonly NotificationChannel ValidChannel = NotificationChannel.Email;
    private static readonly TextValueObject ValidRecipient = TextValueObject.Create("admin@example.com");
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    #region Create

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = NotificationRule.Create(ValidTenantId, ValidChannel, ValidRecipient, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidTenantId, result.Value.TenantId);
        Assert.Equal(ValidChannel, result.Value.Channel);
        Assert.Equal(ValidRecipient, result.Value.Recipient);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public void Create_WithEmptyRecipient_ReturnsFailure()
    {
        var emptyRecipient = TextValueObject.Create("");

        var result = NotificationRule.Create(ValidTenantId, ValidChannel, emptyRecipient, ValidActor);

        Assert.True(result.IsFailure);
    }

    #endregion

    #region Deactivate

    [Fact]
    public void Deactivate_WhenActive_ReturnsSuccess()
    {
        var rule = NotificationRule.Create(ValidTenantId, ValidChannel, ValidRecipient, ValidActor).Value;

        var result = rule.Deactivate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.False(rule.IsActive);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ReturnsFailure()
    {
        var rule = NotificationRule.Create(ValidTenantId, ValidChannel, ValidRecipient, ValidActor).Value;
        rule.Deactivate(ValidActor);

        var result = rule.Deactivate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RuleAlreadyInactive, result.Error);
    }

    #endregion

    #region UpdateRecipient

    [Fact]
    public void UpdateRecipient_WithValidData_ReturnsSuccess()
    {
        var rule = NotificationRule.Create(ValidTenantId, ValidChannel, ValidRecipient, ValidActor).Value;
        var newRecipient = TextValueObject.Create("newadmin@example.com");

        var result = rule.UpdateRecipient(newRecipient, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(newRecipient, rule.Recipient);
    }

    [Fact]
    public void UpdateRecipient_WhenInactive_StillSucceeds()
    {
        var rule = NotificationRule.Create(ValidTenantId, ValidChannel, ValidRecipient, ValidActor).Value;
        rule.Deactivate(ValidActor);
        var newRecipient = TextValueObject.Create("newadmin@example.com");

        var result = rule.UpdateRecipient(newRecipient, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(newRecipient, rule.Recipient);
    }

    [Fact]
    public void UpdateRecipient_WithEmptyRecipient_ReturnsFailure()
    {
        var rule = NotificationRule.Create(ValidTenantId, ValidChannel, ValidRecipient, ValidActor).Value;

        var result = rule.UpdateRecipient(TextValueObject.Create(""), ValidActor);

        Assert.True(result.IsFailure);
    }

    #endregion

    #region Reactivate

    [Fact]
    public void Reactivate_WhenInactive_SetsActiveTrue()
    {
        var rule = NotificationRule.Create(ValidTenantId, ValidChannel, ValidRecipient, ValidActor).Value;
        rule.Deactivate(ValidActor);

        var result = rule.Reactivate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.True(rule.IsActive);
    }

    [Fact]
    public void Reactivate_WhenAlreadyActive_ReturnsFailure()
    {
        var rule = NotificationRule.Create(ValidTenantId, ValidChannel, ValidRecipient, ValidActor).Value;

        var result = rule.Reactivate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.RuleAlreadyActive, result.Error);
    }

    #endregion

    #region UpdateChannel

    [Fact]
    public void UpdateChannel_WithValidChannel_ReturnsSuccess()
    {
        var rule = NotificationRule.Create(ValidTenantId, NotificationChannel.Email, ValidRecipient, ValidActor).Value;

        var result = rule.UpdateChannel(NotificationChannel.InApp, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(NotificationChannel.InApp, rule.Channel);
    }

    #endregion
}
