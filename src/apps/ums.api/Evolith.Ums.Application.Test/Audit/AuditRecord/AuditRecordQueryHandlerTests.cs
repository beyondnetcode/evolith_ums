namespace Evolith.Ums.Application.Test.Audit.AuditRecord;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.Audit.AuditRecord.DTOs;
using Evolith.Ums.Application.Audit.AuditRecord.Queries;
using Evolith.Ums.Domain.Audit.AuditRecord;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;

public class AuditRecordQueryHandlerTests
{
    private readonly Mock<IAuditRecordRepository> _repo = new();

    private static AuditRecord MakeAuditRecord()
    {
        return AuditRecord.Record(
            Guid.NewGuid(),
            SubjectType.User,
            "Updated user name",
            "UserUpdated",
            AuditResult.Success,
            Guid.NewGuid(),
            "UserAccount",
            Guid.NewGuid(),
            "{}").Value;
    }

    // =========================================================================
    #region GetAuditRecordByIdQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetById_WhenFound_ReturnsSuccess()
    {
        var record = MakeAuditRecord();
        var recordId = record.Props.Id.GetValue();
        _repo.Setup(r => r.GetByIdAsync(recordId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(record);

        // G-040 (SEGURIDAD): un admin interno puede leer registros de cualquier inquilino.
        var adminCtx = new Mock<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        adminCtx.Setup(t => t.IsInternalAdmin).Returns(true);

        var query = new GetAuditRecordByIdQuery(recordId);
        var handler = new GetAuditRecordByIdQueryHandler(_repo.Object, adminCtx.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(recordId, result.Value.AuditRecordId);
        Assert.Equal("UserUpdated", result.Value.EventType);
        Assert.Equal("Success", result.Value.AuditResult);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AuditRecord?)null);

        var query = new GetAuditRecordByIdQuery(Guid.NewGuid());
        var handler = new GetAuditRecordByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("audit record not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region GetAllAuditRecordsQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetAll_WithActorFilter_ReturnsActorItems()
    {
        var actorId = Guid.NewGuid();
        var record = MakeAuditRecord();
        var list = new List<AuditRecord> { record };

        _repo.Setup(r => r.QueryByActorAsync(actorId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllAuditRecordsQuery(
            TenantId: null,
            ActorId: actorId,
            EntityId: null,
            EntityType: null,
            EventType: null,
            From: null,
            To: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllAuditRecordsQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        _repo.Verify(r => r.QueryByActorAsync(actorId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_WithEntityFilter_ReturnsEntityItems()
    {
        var entityId = Guid.NewGuid();
        var entityType = "UserAccount";
        var tenantId = Guid.NewGuid();
        var record = MakeAuditRecord();
        var list = new List<AuditRecord> { record };

        _repo.Setup(r => r.QueryByEntityAsync(entityId, entityType, tenantId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllAuditRecordsQuery(
            TenantId: tenantId,
            ActorId: null,
            EntityId: entityId,
            EntityType: entityType,
            EventType: null,
            From: null,
            To: null,
            Page: 1,
            PageSize: 10);

        var adminCtx = new Mock<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        adminCtx.Setup(t => t.IsInternalAdmin).Returns(true);
        var handler = new GetAllAuditRecordsQueryHandler(_repo.Object, adminCtx.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        _repo.Verify(r => r.QueryByEntityAsync(entityId, entityType, tenantId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_WithEventTypeFilter_ReturnsEventTypeItems()
    {
        var eventType = "UserUpdated";
        var tenantId = Guid.NewGuid();
        var record = MakeAuditRecord();
        var list = new List<AuditRecord> { record };

        _repo.Setup(r => r.QueryByEventTypeAsync(eventType, tenantId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllAuditRecordsQuery(
            TenantId: tenantId,
            ActorId: null,
            EntityId: null,
            EntityType: null,
            EventType: eventType,
            From: null,
            To: null,
            Page: 1,
            PageSize: 10);

        var adminCtx = new Mock<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        adminCtx.Setup(t => t.IsInternalAdmin).Returns(true);
        var handler = new GetAllAuditRecordsQueryHandler(_repo.Object, adminCtx.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        _repo.Verify(r => r.QueryByEventTypeAsync(eventType, tenantId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // G-113 (regresión): internal-admin SIN request.TenantId debe consultar con su PROPIA
    // OrganizationId, NO con Guid.Empty (que filtraba RootTenantId==empty y devolvía 0 pese a
    // existir 129 registros en el despliegue). No debilitar: probar el efecto de mostrar el bug.
    [Fact]
    public async Task GetAll_InternalAdminWithoutTenantId_UsesOwnOrganizationId_NotEmpty()
    {
        var orgId = Guid.NewGuid();
        _repo.Setup(r => r.QueryByEventTypeAsync("*", orgId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<AuditRecord> { MakeAuditRecord() });

        var query = new GetAllAuditRecordsQuery(
            TenantId: null, ActorId: null, EntityId: null, EntityType: null,
            EventType: null, From: null, To: null, Page: 1, PageSize: 10);

        var adminCtx = new Mock<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        adminCtx.Setup(t => t.IsInternalAdmin).Returns(true);
        adminCtx.Setup(t => t.OrganizationId).Returns((Guid?)orgId);
        var handler = new GetAllAuditRecordsQueryHandler(_repo.Object, adminCtx.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        _repo.Verify(r => r.QueryByEventTypeAsync("*", orgId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.QueryByEventTypeAsync("*", Guid.Empty, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
