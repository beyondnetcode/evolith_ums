namespace Evolith.Ums.Application.Test.Authorization.AssignmentRule;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Authorization.AssignmentRule.Commands;
using Evolith.Ums.Application.Authorization.AssignmentRule.DTOs;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.AssignmentRule;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;
using System;
using System.Threading;
using System.Threading.Tasks;

public class AssignmentRuleCommandHandlerTests
{
    private readonly Mock<ITemplateAssignmentRuleRepository> _ruleRepo = new();
    private readonly Mock<IPermissionTemplateRepository> _templateRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserContext> _ctx = new();
    private readonly Mock<ITenantScopePolicy> _scopePolicy = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _roleId = Guid.NewGuid();

    public AssignmentRuleCommandHandlerTests()
    {
        _ruleRepo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("admin-001");
        _scopePolicy.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _ruleRepo.Setup(r => r.ExistsActiveWithPriorityAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private PermissionTemplate MakePublishedTemplate()
    {
        var template = PermissionTemplate.Create(
            TenantId.Load(_tenantId),
            RoleId.Load(_roleId),
            SystemSuiteId.Load(Guid.NewGuid()),
            ActorId.Create("admin-001")).Value;

        template.AddItem(
            ExclusiveArcTarget.SystemSuite,
            IdValueObject.Create(),
            ActionId.Load(Guid.NewGuid()),
            isAllowed: true, isDenied: false,
            ActorId.Create("admin-001"));

        template.Publish(ActorId.Create("admin-001"));
        return template;
    }

    private CreateAssignmentRuleCommandHandler CreateHandler()
        => new(_ruleRepo.Object, _templateRepo.Object, _ctx.Object, _scopePolicy.Object);

    // ───────────────────────── CreateAssignmentRuleCommandHandler ─────────────

    [Fact]
    public async Task Create_WithValidArgs_ReturnsSuccess()
    {
        var template = MakePublishedTemplate();
        _templateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var cmd = new CreateAssignmentRuleCommand(_tenantId, template.Props.Id.GetValue(), _roleId, 10);
        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.RuleId);
        _ruleRepo.Verify(r => r.AddAsync(It.IsAny<TemplateAssignmentRule>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new CreateAssignmentRuleCommand(_tenantId, Guid.NewGuid(), _roleId, 10);
        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenTemplateNotFound_ReturnsFailure()
    {
        _templateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PermissionTemplate?)null);

        var cmd = new CreateAssignmentRuleCommand(_tenantId, Guid.NewGuid(), _roleId, 10);
        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenTemplateNotPublished_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(
            TenantId.Load(_tenantId), RoleId.Load(_roleId),
            SystemSuiteId.Load(Guid.NewGuid()), ActorId.Create("admin")).Value;

        _templateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var cmd = new CreateAssignmentRuleCommand(_tenantId, template.Props.Id.GetValue(), _roleId, 10);
        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateNotPublishedForProfile, result.Error);
    }

    [Fact]
    public async Task Create_WhenPriorityAlreadyTaken_ReturnsFailure()
    {
        var template = MakePublishedTemplate();
        _templateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _ruleRepo.Setup(r => r.ExistsActiveWithPriorityAsync(_tenantId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cmd = new CreateAssignmentRuleCommand(_tenantId, template.Props.Id.GetValue(), _roleId, 10);
        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.AssignmentRuleDuplicatePriority, result.Error);
    }

    // ───────────────────────── DeactivateAssignmentRuleCommandHandler ─────────

    [Fact]
    public async Task Deactivate_WhenRuleExists_ReturnsSuccess()
    {
        var rule = TemplateAssignmentRule.Create(
            TenantId.Load(_tenantId), TemplateId.Load(Guid.NewGuid()),
            RoleId.Load(_roleId), 5, ActorId.Create("admin-001")).Value;

        _ruleRepo.Setup(r => r.GetByIdAsync(rule.Props.Id.GetValue(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        var handler = new DeactivateAssignmentRuleCommandHandler(_ruleRepo.Object, _ctx.Object);
        var result = await handler.Handle(new DeactivateAssignmentRuleCommand(rule.Props.Id.GetValue()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TemplateAssignmentRuleStatus.Inactive, rule.Status);
    }

    [Fact]
    public async Task Deactivate_WhenRuleNotFound_ReturnsFailure()
    {
        _ruleRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TemplateAssignmentRule?)null);

        var handler = new DeactivateAssignmentRuleCommandHandler(_ruleRepo.Object, _ctx.Object);
        var result = await handler.Handle(new DeactivateAssignmentRuleCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
