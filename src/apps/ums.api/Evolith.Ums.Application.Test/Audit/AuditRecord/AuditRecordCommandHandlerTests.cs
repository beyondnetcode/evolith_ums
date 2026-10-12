namespace Evolith.Ums.Application.Test.Audit.AuditRecord;

using System;
using System.Threading;
using System.Threading.Tasks;
using BeyondNetCode.Shell.Ddd.Interfaces;
using Evolith.Ums.Application.Audit.AuditRecord.Commands;
using Evolith.Ums.Application.Audit.AuditRecord.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Audit.AuditRecord;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;

public class AuditRecordCommandHandlerTests
{
    private readonly Mock<IAuditRecordRepository>             _repo = new();
    private readonly Mock<BeyondNetCode.Shell.Ddd.Interfaces.IUnitOfWork> _uow  = new();
    private readonly Mock<IUserContext>                       _userContext = new();

    public AuditRecordCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<object>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // G-040 (SEGURIDAD): el actor y el inquilino se derivan del contexto
        // autenticado, no del cuerpo de la petición.
        _userContext.SetupGet(c => c.IsAuthenticated).Returns(true);
        _userContext.SetupGet(c => c.UserId).Returns(Guid.NewGuid().ToString());
        _userContext.SetupGet(c => c.TenantId).Returns(Guid.NewGuid().ToString());
    }

    [Fact]
    public async Task Record_WithValidCommand_ReturnsSuccess()
    {
        var cmd = new RecordAuditCommand(
            WhoActed: Guid.NewGuid(),
            SubjectType: "User",
            WhatChanged: "Updated user name",
            EventType: "UserUpdated",
            AuditResult: "Success",
            AffectedEntityId: Guid.NewGuid(),
            AffectedEntityType: "UserAccount",
            RootTenantId: Guid.NewGuid(),
            Metadata: "{}");

        var handler = new RecordAuditCommandHandler(_repo.Object, _userContext.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.AuditRecordId);
        _repo.Verify(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // G-040 (FR-072): una traza registrada con metadata que contiene un secreto NO debe persistir el
    // secreto en claro. La traza es append-only e inmutable (G-081): lo que se apende no se puede borrar.
    [Fact]
    public async Task Record_WithSecretInMetadata_DoesNotPersistSecretInClear()
    {
        AuditRecord? appended = null;
        _repo.Setup(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()))
             .Callback<AuditRecord, CancellationToken>((record, _) => appended = record)
             .Returns(Task.CompletedTask);

        var cmd = new RecordAuditCommand(
            WhoActed: Guid.NewGuid(),
            SubjectType: "User",
            WhatChanged: "Registro con secretos",
            EventType: "UserUpdated",
            AuditResult: "Success",
            AffectedEntityId: Guid.NewGuid(),
            AffectedEntityType: "UserAccount",
            RootTenantId: Guid.NewGuid(),
            Metadata: "{\"password\":\"hunter2\",\"apiKey\":\"sk-live-secret\",\"handler\":\"legit\"}");

        var handler = new RecordAuditCommandHandler(_repo.Object, _userContext.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(appended);
        Assert.NotNull(appended!.Metadata);
        // El secreto en claro no se persiste; la clave se conserva redactada.
        Assert.DoesNotContain("hunter2", appended.Metadata);
        Assert.DoesNotContain("sk-live-secret", appended.Metadata);
        Assert.Contains("[REDACTED]", appended.Metadata);
        // La metadata legítima permanece intacta.
        Assert.Contains("legit", appended.Metadata);
    }

    [Fact]
    public async Task Record_WhenUserIsNotAuthenticated_ReturnsFailure()
    {
        _userContext.SetupGet(c => c.IsAuthenticated).Returns(false);
        _userContext.SetupGet(c => c.UserId).Returns((string?)null);

        var cmd = new RecordAuditCommand(
            WhoActed: Guid.NewGuid(),
            SubjectType: "User",
            WhatChanged: "Updated user name",
            EventType: "UserUpdated",
            AuditResult: "Success",
            AffectedEntityId: Guid.NewGuid(),
            AffectedEntityType: "UserAccount",
            RootTenantId: Guid.NewGuid(),
            Metadata: "{}");

        var handler = new RecordAuditCommandHandler(_repo.Object, _userContext.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Record_WhenWhatChangedIsEmpty_ReturnsFailure()
    {
        var cmd = new RecordAuditCommand(
            WhoActed: Guid.NewGuid(),
            SubjectType: "User",
            WhatChanged: "",
            EventType: "UserUpdated",
            AuditResult: "Success",
            AffectedEntityId: Guid.NewGuid(),
            AffectedEntityType: "UserAccount",
            RootTenantId: Guid.NewGuid(),
            Metadata: "{}");

        var handler = new RecordAuditCommandHandler(_repo.Object, _userContext.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
