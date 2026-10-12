namespace Evolith.Ums.Application.Test.Authorization.Template;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.Authorization.Template.DTOs;
using Evolith.Ums.Application.Authorization.Template.Queries;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Role;
using Evolith.Ums.Domain.Authorization.SystemSuite;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;

public class TemplateQueryHandlerTests
{
    private readonly Mock<IPermissionTemplateRepository> _repo = new();
    private readonly Mock<IRoleRepository> _roleRepo = new();
    private readonly Mock<ISystemSuiteRepository> _suiteRepo = new();
    private readonly Mock<IUserContext> _userContext = new();

    private static PermissionTemplate MakeTemplate(TemplateStatus status)
    {
        var template = PermissionTemplate.Create(
            TenantId.Load(Guid.NewGuid()),
            RoleId.Load(Guid.NewGuid()),
            SystemSuiteId.Load(Guid.NewGuid()),
            ActorId.Create("user-001")).Value;

        var itemResult = template.AddItem(
            ExclusiveArcTarget.SystemSuite,
            IdValueObject.Create(),
            ActionId.Load(Guid.NewGuid()),
            isAllowed: true,
            isDenied: false,
            ActorId.Create("user-001"));
        if (itemResult.IsFailure)
        {
            throw new InvalidOperationException(itemResult.Error);
        }

        if (status == TemplateStatus.Published)
        {
            var publishResult = template.Publish(ActorId.Create("user-001"));
            if (publishResult.IsFailure)
            {
                throw new InvalidOperationException(publishResult.Error);
            }
        }
        else if (status == TemplateStatus.Deprecated)
        {
            var publishResult = template.Publish(ActorId.Create("user-001"));
            if (publishResult.IsFailure)
            {
                throw new InvalidOperationException(publishResult.Error);
            }

            var deprecateResult = template.Deprecate(ActorId.Create("user-001"));
            if (deprecateResult.IsFailure)
            {
                throw new InvalidOperationException(deprecateResult.Error);
            }
        }

        return template;
    }

    // =========================================================================
    #region GetPermissionTemplateByIdQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetById_WhenFound_ReturnsSuccess()
    {
        var template = MakeTemplate(TemplateStatus.Draft);
        var templateId = template.Props.Id.GetValue();
        _repo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(template);

        _roleRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Role?)null);
        _suiteRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((SystemSuite?)null);

        var query = new GetPermissionTemplateByIdQuery(templateId);
        var handler = new GetPermissionTemplateByIdQueryHandler(_repo.Object, _roleRepo.Object, _suiteRepo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(templateId, result.Value.TemplateId);
        Assert.Equal("Draft", result.Value.Status);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((PermissionTemplate?)null);

        var query = new GetPermissionTemplateByIdQuery(Guid.NewGuid());
        var handler = new GetPermissionTemplateByIdQueryHandler(_repo.Object, _roleRepo.Object, _suiteRepo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("template not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region GetAllPermissionTemplatesQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetAll_WithoutTenantFilter_ReturnsAllItems()
    {
        var t1 = MakeTemplate(TemplateStatus.Draft);
        var t2 = MakeTemplate(TemplateStatus.Published);
        var list = new List<PermissionTemplate> { t1, t2 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllPermissionTemplatesQuery(
            TenantId: null,
            Criteria: "version",
            Status: "all",
            Search: null,
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        _roleRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Role?)null);
        _suiteRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((SystemSuite?)null);

        var handler = new GetAllPermissionTemplatesQueryHandler(_repo.Object, _roleRepo.Object, _suiteRepo.Object, _userContext.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalItems);
        Assert.Equal(2, result.Value.Items.Count);
    }

    [Fact]
    public async Task GetAll_WithTenantFilter_ReturnsTenantItems()
    {
        var tenantId = Guid.NewGuid();
        var t1 = MakeTemplate(TemplateStatus.Draft);
        var list = new List<PermissionTemplate> { t1 };

        _repo.Setup(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllPermissionTemplatesQuery(
            TenantId: tenantId,
            Criteria: "version",
            Status: "all",
            Search: null,
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        _roleRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Role?)null);
        _suiteRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((SystemSuite?)null);

        var adminCtx = new Mock<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        adminCtx.Setup(t => t.IsInternalAdmin).Returns(true);
        var handler = new GetAllPermissionTemplatesQueryHandler(_repo.Object, _roleRepo.Object, _suiteRepo.Object, _userContext.Object, adminCtx.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        _repo.Verify(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_WithStatusFilter_FiltersStatus()
    {
        var t1 = MakeTemplate(TemplateStatus.Draft);
        var t2 = MakeTemplate(TemplateStatus.Published);
        var list = new List<PermissionTemplate> { t1, t2 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllPermissionTemplatesQuery(
            TenantId: null,
            Criteria: "version",
            Status: "Published",
            Search: null,
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        _roleRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Role?)null);
        _suiteRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((SystemSuite?)null);

        var handler = new GetAllPermissionTemplatesQueryHandler(_repo.Object, _roleRepo.Object, _suiteRepo.Object, _userContext.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        Assert.Equal("Published", result.Value.Items[0].Status);
    }

    #endregion
}
