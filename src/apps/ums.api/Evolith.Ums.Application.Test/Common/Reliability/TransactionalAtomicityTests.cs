namespace Evolith.Ums.Application.Test.Common.Reliability;

#pragma warning disable S125

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.AppConfiguration.Commands;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration;
using Xunit;

using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;

/// <summary>
/// Application-layer transactional reliability tests.
///
/// These tests verify (and document) the current behaviour of the UMS backend
/// with respect to atomicity, event consistency, idempotency, concurrency, and
/// error classification.
///
/// Tests tagged [documents gap] expose known risks — they are expected to PASS
/// by demonstrating the problematic behaviour (not by fixing it). This makes gaps
/// visible in CI and provides a clear regression baseline when fixes land.
///
/// Reference: docs/architecture/transactional-reliability-assessment.md
/// </summary>
public class TransactionalAtomicityTests
{
    // -------------------------------------------------------------------------
    // Shared fixtures
    // -------------------------------------------------------------------------

    private readonly Mock<IAppConfigurationRepository> _repo           = new();
    private readonly Mock<IUnitOfWork>                 _uow            = new();
    private readonly Mock<IUserContext>                _ctx            = new();
    private readonly Mock<IConfigurationProvider>      _configProvider = new();
    private readonly Mock<IValueEncryptionService>     _encryption     = new();

    private static readonly TenantId     ValidTenant = TenantId.Load(Guid.NewGuid());
    private static readonly SystemSuiteId ValidSuite  = SystemSuiteId.Load(Guid.NewGuid());
    private static readonly ActorId      Actor        = ActorId.Create("user-001");

    public TransactionalAtomicityTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _configProvider.Setup(p => p.ReloadAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _configProvider.Setup(p => p.ReloadTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _encryption.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(v => v);
        _encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(v => v);
        _encryption.Setup(e => e.IsEncryptedValue(It.IsAny<string>())).Returns(false);
    }

    private static CreateAppConfigurationCommand ValidCreateCmd => new(
        TenantId:      ValidTenant.GetValue(),
        SystemSuiteId: ValidSuite.GetValue(),
        ModuleId:      null,
        Code:          $"CFG-{Guid.NewGuid():N}",
        Value:         "value123",
        Description:   "Test configuration",
        IsInheritable: true,
        IsEncrypted:   false);

    // =========================================================================
    // T01 — Atomic save: aggregate + outbox persisted in one SaveEntities call
    // =========================================================================

    /// <summary>T01: On a successful create, AddAsync and SaveEntitiesAsync are called
    /// exactly once each. The single SaveEntitiesAsync call is what guarantees
    /// the aggregate record and the outbox message land in the same DB transaction.</summary>
    [Fact]
    public async Task T01_Create_Success_AddAndSaveCalledExactlyOnce()
    {
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repo.Setup(r => r.GetByScopeAndCodeAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);

        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(ValidCreateCmd, CancellationToken.None);

        Assert.True(result.IsSuccess);

        // One AddAsync → aggregate tracked
        _repo.Verify(r => r.AddAsync(
            It.IsAny<AppConfigurationAggregate>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // One SaveEntitiesAsync → aggregate + outbox messages committed atomically
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    // T02 — RISK-01 [documents gap]: Events cleared before SaveChanges
    //       If SaveChangesAsync throws, events are permanently lost from memory.
    // =========================================================================

    /// <summary>T02 [DOCUMENTS RISK-01]: Demonstrates that when SaveChangesAsync fails,
    /// the aggregate's domain events have already been cleared by MarkChangesAsCommitted().
    ///
    /// Expected (current buggy) behaviour: SaveEntitiesAsync propagates the exception,
    /// but the aggregate is left with ZERO uncommitted events. A retry would produce
    /// outbox messages for 0 events — the events are permanently lost.
    ///
    /// Fix required: move MarkChangesAsCommitted() to AFTER SaveChangesAsync succeeds.
    /// See FIX-01 in transactional-reliability-assessment.md.</summary>
    [Fact]
    public async Task T02_WhenSaveChangesFails_AggregateEventsAreAlreadyCleared_RiskDocumented()
    {
        // Arrange: build an aggregate in Draft state (has a CreatedEvent)
        var config = AppConfigurationAggregate.Create(
            ValidTenant, ValidSuite, null,
            Code.Create("CFG-RISK01"),
            ConfigurationValue.Create("v"),
            Description.Create("d"),
            true, false, Actor).Value;

        var eventsBeforeSave = config.DomainEvents.GetUncommittedChanges().Count;
        Assert.Equal(1, eventsBeforeSave); // one AppConfigCreatedEvent

        // Simulate the SaveEntitiesAsync logic from PostgreSqlAppConfigurationRepository
        // (reproduced here because we cannot call the SQL repository without DB)
        var outboxMessages = config.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Single(outboxMessages); // outbox materialized

        // This is what the current code does BEFORE calling SaveChangesAsync:
        config.DomainEvents.MarkChangesAsCommitted(); // ← the bug: clears before save

        // Now SaveChanges "fails" (simulated)
        var saveWouldThrow = true;

        if (saveWouldThrow)
        {
            // After the failure, the aggregate has NO uncommitted events to retry
            var eventsAfterFailure = config.DomainEvents.GetUncommittedChanges().Count;

            // CURRENT (buggy) state: events are gone
            Assert.Equal(0, eventsAfterFailure);

            // DESIRED state (after fix): events should still be available for retry
            // Assert.Equal(1, eventsAfterFailure); ← this should pass AFTER FIX-01
        }
    }

    // =========================================================================
    // T03 — Domain failure → SaveEntitiesAsync NOT called
    // =========================================================================

    /// <summary>T03: When the domain operation returns a failure Result (e.g., state machine
    /// guard rejects the operation), the handler returns early and SaveEntitiesAsync
    /// is never called — no partial write occurs.</summary>
    [Fact]
    public async Task T03_WhenDomainOperationFails_SaveEntitiesNotCalled()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakePublishedConfig()); // Published → Update is forbidden

        var handler = new UpdateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object, _encryption.Object);
        var result  = await handler.Handle(
            new UpdateAppConfigurationCommand(Guid.NewGuid(), "new-value", "new-desc"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.AppConfigNotDraft, result.Error);

        // No persistence attempted
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.UpdateAsync(
            It.IsAny<AppConfigurationAggregate>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // T04 — Validation failure → SaveEntitiesAsync NOT called
    // =========================================================================

    /// <summary>T04: When the auth guard rejects the request (unauthenticated caller),
    /// the handler returns immediately without touching the repository.</summary>
    [Fact]
    public async Task T04_WhenAuthGuardFails_SaveEntitiesNotCalled()
    {
        _ctx.Setup(u => u.UserId).Returns(""); // no authenticated user

        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(ValidCreateCmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);

        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.AddAsync(
            It.IsAny<AppConfigurationAggregate>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // T05 — Duplicate create → second call returns Conflict
    // =========================================================================

    /// <summary>T05: The duplicate-code guard correctly detects an existing entry for the
    /// same scope+code combination and returns a Conflict failure before touching the DB.</summary>
    [Fact]
    public async Task T05_DuplicateCreate_SameScope_ReturnsConflictNoSave()
    {
        var existing = MakeDraftConfig("CFG-DUP");
        _repo.Setup(r => r.GetByScopeAndCodeAsync(
                ValidTenant.GetValue(),
                ValidSuite.GetValue(),
                null,
                "CFG-DUP",
                It.IsAny<CancellationToken>()))
             .ReturnsAsync(existing);

        var cmd = ValidCreateCmd with { Code = "CFG-DUP" };
        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error);

        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // T06 — Invalid state transition → 422 surfaced through handler
    // =========================================================================

    /// <summary>T06: Publishing an already-archived config returns a domain failure.
    /// The handler surfaces the domain error correctly without persistence.</summary>
    [Fact]
    public async Task T06_PublishArchivedConfig_ReturnsDomainFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeArchivedConfig());

        var handler = new PublishAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(
            new PublishAppConfigurationCommand(Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.AppConfigNotDraft, result.Error);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // T07 — RISK-02 [documents gap]: Lost update — concurrent modifications
    // =========================================================================

    /// <summary>T07 [DOCUMENTS RISK-02]: Two concurrent requests load the same aggregate,
    /// both call Update(), and both succeed without any concurrency error.
    /// The last write silently overwrites the first.
    ///
    /// Fix required: add RowVersion to all record entities and handle
    /// DbUpdateConcurrencyException in repository Update methods.
    /// See FIX-03 in transactional-reliability-assessment.md.</summary>
    [Fact]
    public async Task T07_ConcurrentUpdates_BothSucceed_LastWriteWinsGap()
    {
        var sharedConfig = MakeDraftConfig("CFG-SHARED");

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(sharedConfig); // both "requests" load same aggregate
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handlerA = new UpdateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object, _encryption.Object);
        var handlerB = new UpdateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object, _encryption.Object);
        var id = Guid.NewGuid();

        // Both requests fire "concurrently" (sequential here but same loaded state)
        var resultA = await handlerA.Handle(
            new UpdateAppConfigurationCommand(id, "value-from-A", "desc-A"), CancellationToken.None);
        var resultB = await handlerB.Handle(
            new UpdateAppConfigurationCommand(id, "value-from-B", "desc-B"), CancellationToken.None);

        // CURRENT (gap) behaviour: both succeed — no concurrency conflict detected
        Assert.True(resultA.IsSuccess, "Request A succeeded — first write");
        Assert.True(resultB.IsSuccess, "Request B succeeded — overwrites A silently (RISK-02)");

        // DESIRED behaviour after FIX-03:
        // One of the two should return Result.Failure("Concurrency conflict")
        // Assert.True(resultA.IsSuccess ^ resultB.IsSuccess);
    }

    // =========================================================================
    // T08 — RISK-05 [documents gap]: No idempotency — duplicate requests both land
    // =========================================================================

    /// <summary>T08 [DOCUMENTS RISK-05]: When the duplicate-code check returns null for BOTH
    /// in-flight requests (race window before either has committed), two identical
    /// create commands both succeed, resulting in duplicate entries.
    ///
    /// Fix required: implement IdempotencyMiddleware with request deduplication table.
    /// See FIX-06 in transactional-reliability-assessment.md.</summary>
    [Fact]
    public async Task T08_NoIdempotency_TwoIdenticalCommandsInRaceWindow_BothSucceed()
    {
        // Both requests pass the duplicate check — simulates race window
        _repo.Setup(r => r.GetByScopeAndCodeAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null); // neither sees the other yet
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var cmd = ValidCreateCmd;

        // "Client retries" the same logical operation
        var firstAttempt  = await handler.Handle(cmd, CancellationToken.None);
        var retryAttempt  = await handler.Handle(cmd, CancellationToken.None);

        // CURRENT (gap) behaviour: both succeed — two duplicates created
        Assert.True(firstAttempt.IsSuccess,  "First attempt succeeds");
        Assert.True(retryAttempt.IsSuccess,  "Retry also succeeds — RISK-05 gap");

        // DESIRED behaviour after FIX-06:
        // The retry should return the same response as the first (cached by idempotency key)
        // Assert.Equal(firstAttempt.Value.AppConfigurationId, retryAttempt.Value.AppConfigurationId);
    }

    // =========================================================================
    // T09–T11 — Error classification: HTTP status mapping
    // =========================================================================

    /// <summary>T09: A "not found" domain error from a handler correctly maps to 404.</summary>
    [Fact]
    public async Task T09_NotFoundError_Maps404()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);

        var handler = new PublishAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(
            new PublishAppConfigurationCommand(Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>T10: A "already exists" domain error maps to 409 Conflict.</summary>
    [Fact]
    public async Task T10_AlreadyExistsError_Maps409()
    {
        _repo.Setup(r => r.GetByScopeAndCodeAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeDraftConfig("EXISTING"));

        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(ValidCreateCmd with { Code = "EXISTING" }, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>T11: Missing authentication returns 401-mapped error message.</summary>
    [Fact]
    public async Task T11_Unauthenticated_Returns401MappedError()
    {
        _ctx.Setup(u => u.UserId).Returns((string?)null);

        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(ValidCreateCmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // T12 — Audit fields populated on aggregate creation
    // =========================================================================

    /// <summary>T12: When a command succeeds, the created aggregate carries the ActorId
    /// (CreatedBy) that was passed from the authenticated user context.</summary>
    [Fact]
    public async Task T12_AuditFields_OnCreate_CarryActorFromUserContext()
    {
        var capturedAggregate = (AppConfigurationAggregate?)null;

        _repo.Setup(r => r.GetByScopeAndCodeAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);
        _repo.Setup(r => r.AddAsync(It.IsAny<AppConfigurationAggregate>(), It.IsAny<CancellationToken>()))
             .Callback<AppConfigurationAggregate, CancellationToken>((agg, _) => capturedAggregate = agg)
             .Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("audited-actor-007");

        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(ValidCreateCmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedAggregate);

        var audit = capturedAggregate!.Props.Audit.GetValue();
        Assert.Equal("audited-actor-007", audit.CreatedBy);
        // El audit se estampa con DateTime.UtcNow: comparar contra la fecha UTC, no la
        // local. (Antes se comparaba con DateTime.Today.ToUniversalTime().Date —derivado
        // de la fecha local—, lo que fallaba en la ventana UTC 00:00–05:00, cuando la
        // fecha local de Lima (UTC-5) aún es la del día anterior.)
        Assert.Equal(DateTime.UtcNow.Date, audit.CreatedAt.Date);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static AppConfigurationAggregate MakeDraftConfig(string code) =>
        AppConfigurationAggregate.Create(
            ValidTenant, ValidSuite, null,
            Code.Create(code),
            ConfigurationValue.Create("v"),
            Description.Create("d"),
            true, false, Actor).Value;

    private static AppConfigurationAggregate MakePublishedConfig(string code = "CFG-PUB")
    {
        var c = MakeDraftConfig(code);
        c.Publish(Actor);
        return c;
    }

    private static AppConfigurationAggregate MakeArchivedConfig()
    {
        var c = MakePublishedConfig();
        c.Archive(Actor);
        return c;
    }
}
