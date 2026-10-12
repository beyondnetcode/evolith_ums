namespace Evolith.Ums.Application.Test.Approvals.NotificationRule;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.Approvals.NotificationRule.DTOs;
using Evolith.Ums.Application.Approvals.NotificationRule.Queries;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.NotificationRule;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;

public class NotificationRuleQueryHandlerTests
{
    private readonly Mock<INotificationRuleRepository> _repo = new();

    private static NotificationRule MakeNotificationRule(NotificationChannel channel, bool isActive)
    {
        var rule = NotificationRule.Create(
            TenantId.Load(Guid.NewGuid()),
            channel,
            TextValueObject.Create("recipient@example.com"),
            ActorId.Create("user-001")).Value;

        if (!isActive)
        {
            rule.Deactivate(ActorId.Create("user-001"));
        }

        return rule;
    }

    // =========================================================================
    #region GetNotificationRuleByIdQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetById_WhenFound_ReturnsSuccess()
    {
        var rule = MakeNotificationRule(NotificationChannel.Email, true);
        var ruleId = rule.Props.Id.GetValue();
        _repo.Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(rule);

        var query = new GetNotificationRuleByIdQuery(ruleId);
        var handler = new GetNotificationRuleByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ruleId, result.Value.NotificationRuleId);
        Assert.Equal("Email", result.Value.Channel);
        Assert.Equal("recipient@example.com", result.Value.Recipient);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((NotificationRule?)null);

        var query = new GetNotificationRuleByIdQuery(Guid.NewGuid());
        var handler = new GetNotificationRuleByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("rule not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region GetAllNotificationRulesQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetAll_WithoutTenantFilter_ReturnsAllItems()
    {
        var r1 = MakeNotificationRule(NotificationChannel.Email, true);
        var r2 = MakeNotificationRule(NotificationChannel.Sms, false);
        var list = new List<NotificationRule> { r1, r2 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllNotificationRulesQuery(
            TenantId: null,
            Status: "all",
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllNotificationRulesQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalItems);
        Assert.Equal(2, result.Value.Items.Count);
    }

    [Fact]
    public async Task GetAll_WithTenantFilter_ReturnsTenantItems()
    {
        var tenantId = Guid.NewGuid();
        var r1 = MakeNotificationRule(NotificationChannel.Email, true);
        var list = new List<NotificationRule> { r1 };

        _repo.Setup(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllNotificationRulesQuery(
            TenantId: tenantId,
            Status: "all",
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var adminCtx = new Mock<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        adminCtx.Setup(t => t.IsInternalAdmin).Returns(true);
        var handler = new GetAllNotificationRulesQueryHandler(_repo.Object, adminCtx.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        _repo.Verify(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_WithActiveFilter_FiltersActiveOnly()
    {
        var r1 = MakeNotificationRule(NotificationChannel.Email, true);
        var r2 = MakeNotificationRule(NotificationChannel.Sms, false);
        var list = new List<NotificationRule> { r1, r2 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllNotificationRulesQuery(
            TenantId: null,
            Status: "active",
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllNotificationRulesQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        Assert.True(result.Value.Items[0].IsActive);
    }

    #endregion
}
