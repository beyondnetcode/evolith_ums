namespace Evolith.Ums.Application.Test.Configuration.FeatureFlag;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.FeatureFlag.Commands;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.FeatureFlag;
using Xunit;

/// <summary>
/// Application-layer tests for FeatureFlag command handlers.
///
/// Coverage intent:
///   – Auth guard: unauthenticated callers are rejected before touching the domain.
///   – Duplicate-code guard in CreateFeatureFlagCommandHandler.
///   – Invalid enum values (FlagType, LinkedResourceType) rejected at handler boundary.
///   – Domain failure (percentage out of range) surfaced through handler.
///   – EvaluateFeatureFlagCommandHandler: not-found, archived, invalid actor GUID.
///   – Lifecycle handlers (Activate, Deactivate, Archive): not-found and domain guards.
///   – Repository interactions (AddAsync / UpdateAsync / SaveEntities) called exactly once on success.
///
/// Excluded intentionally:
///   – Query handlers (read-only projections — no business logic).
///   – FluentValidation pipeline (tested via ValidationBehaviorTests).
/// </summary>
public class FeatureFlagCommandHandlerTests
{
    // -------------------------------------------------------------------------
    // Shared mocks & helpers
    // -------------------------------------------------------------------------

    private readonly Mock<IFeatureFlagRepository>  _repo      = new();
    private readonly Mock<IUnitOfWork>             _uow       = new();
    private readonly Mock<IUserContext>            _ctx       = new();
    private readonly Mock<IFeatureFlagEvaluator>   _evaluator = new();

    private static readonly Guid ValidSystemSuiteId = Guid.NewGuid();
    private static readonly IdValueObject SystemSuiteIdVo = IdValueObject.Load(ValidSystemSuiteId);

    public FeatureFlagCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _evaluator.Setup(e => e.Evaluate(It.IsAny<FeatureFlag>(), It.IsAny<EvaluationContext>()))
                  .Returns((FeatureFlag f, EvaluationContext _) =>
                      new FlagEvaluationResult(f.Status == FlagStatus.Active, null, null));
    }

    private static CreateFeatureFlagCommand ValidCreateCmd => new(
        SystemSuiteId: ValidSystemSuiteId,
        TenantId: null,
        FlagCode: "FLAG-001",
        FlagType: "Boolean",
        FlagTargets: "all",
        LinkedResourceType: null,
        LinkedResourceId: null,
        RolloutPercentage: null);

    private static FeatureFlag MakeBoolean() =>
        FeatureFlag.Create(SystemSuiteIdVo, null, "FLAG-001", global::Evolith.Ums.Domain.Enums.FlagType.Boolean, "all", null, null, null,
            ActorId.Create("user-001")).Value;

    private static FeatureFlag MakeActive()
    {
        var f = MakeBoolean();
        f.Activate(ActorId.Create("user-001"));
        return f;
    }

    private static FeatureFlag MakeArchived()
    {
        var f = MakeBoolean();
        f.Archive(ActorId.Create("user-001"));
        return f;
    }

    // =========================================================================
    #region CreateFeatureFlagCommandHandler
    // =========================================================================

    [Fact]
    public async Task Create_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetBySystemSuiteAndCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((FeatureFlag?)null);

        var handler = new CreateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(ValidCreateCmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.FeatureFlagId);
    }

    [Fact]
    public async Task Create_SavesAggregateToRepository()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetBySystemSuiteAndCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((FeatureFlag?)null);

        var handler = new CreateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        await handler.Handle(ValidCreateCmd, CancellationToken.None);

        _repo.Verify(r => r.AddAsync(It.IsAny<FeatureFlag>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenUserIdEmpty_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var handler = new CreateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(ValidCreateCmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task Create_WhenUserIdNull_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns((string?)null);

        var handler = new CreateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(ValidCreateCmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Create_WhenFlagCodeAlreadyExists_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetBySystemSuiteAndCodeAsync(ValidSystemSuiteId, "FLAG-001", It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeBoolean());

        var handler = new CreateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(ValidCreateCmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task Create_WithInvalidFlagType_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetBySystemSuiteAndCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((FeatureFlag?)null);
        var cmd = ValidCreateCmd with { FlagType = "UNKNOWN_TYPE" };

        var handler = new CreateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Invalid feature flag type", result.Error);
    }

    [Fact]
    public async Task Create_WithInvalidLinkedResourceType_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetBySystemSuiteAndCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((FeatureFlag?)null);
        var cmd = ValidCreateCmd with { LinkedResourceType = "BOGUS" };

        var handler = new CreateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Invalid linked resource type", result.Error);
    }

    [Fact]
    public async Task Create_PercentageFlagWithoutRollout_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetBySystemSuiteAndCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((FeatureFlag?)null);
        var cmd = ValidCreateCmd with { FlagType = "Percentage", RolloutPercentage = null };

        var handler = new CreateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region ActivateFeatureFlagCommandHandler
    // =========================================================================

    [Fact]
    public async Task Activate_WhenFlagExists_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var flag = MakeBoolean();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(flag);

        var handler = new ActivateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new ActivateFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(flag, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Activate_WhenFlagNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((FeatureFlag?)null);

        var handler = new ActivateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new ActivateFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task Activate_WhenFlagAlreadyActive_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeActive()); // already active

        var handler = new ActivateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new ActivateFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Activate_WhenNoAuthUser_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var handler = new ActivateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new ActivateFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    #endregion

    // =========================================================================
    #region DeactivateFeatureFlagCommandHandler
    // =========================================================================

    [Fact]
    public async Task Deactivate_WhenFlagActive_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeActive());

        var handler = new DeactivateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new DeactivateFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Deactivate_WhenFlagNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((FeatureFlag?)null);

        var handler = new DeactivateFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new DeactivateFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
    }

    #endregion

    // =========================================================================
    #region ArchiveFeatureFlagCommandHandler
    // =========================================================================

    [Fact]
    public async Task Archive_WhenFlagActive_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeActive());

        var handler = new ArchiveFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new ArchiveFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Archive_WhenAlreadyArchived_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeArchived());

        var handler = new ArchiveFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new ArchiveFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Archive_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((FeatureFlag?)null);

        var handler = new ArchiveFeatureFlagCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new ArchiveFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
    }

    #endregion

    // =========================================================================
    #region EvaluateFeatureFlagCommandHandler
    // =========================================================================

    [Fact]
    public async Task Evaluate_WhenFlagActiveAndValidUser_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        _ctx.Setup(u => u.UserId).Returns(userId.ToString());
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeActive());

        var handler = new EvaluateFeatureFlagCommandHandler(_repo.Object, _ctx.Object, _evaluator.Object);
        var result  = await handler.Handle(new EvaluateFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsEnabled);
    }

    [Fact]
    public async Task Evaluate_WhenFlagInactive_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        _ctx.Setup(u => u.UserId).Returns(userId.ToString());
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeBoolean()); // inactive

        var handler = new EvaluateFeatureFlagCommandHandler(_repo.Object, _ctx.Object, _evaluator.Object);
        var result  = await handler.Handle(new EvaluateFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsEnabled);
    }

    [Fact]
    public async Task Evaluate_WhenFlagNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((FeatureFlag?)null);

        var handler = new EvaluateFeatureFlagCommandHandler(_repo.Object, _ctx.Object, _evaluator.Object);
        var result  = await handler.Handle(new EvaluateFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task Evaluate_WhenFlagArchived_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeArchived());

        var handler = new EvaluateFeatureFlagCommandHandler(_repo.Object, _ctx.Object, _evaluator.Object);
        var result  = await handler.Handle(new EvaluateFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Evaluate_WhenUserIdIsNotGuid_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("not-a-guid");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeActive());

        var handler = new EvaluateFeatureFlagCommandHandler(_repo.Object, _ctx.Object, _evaluator.Object);
        var result  = await handler.Handle(new EvaluateFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("valid GUID", result.Error);
    }

    [Fact]
    public async Task Evaluate_WhenNoAuthUser_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var handler = new EvaluateFeatureFlagCommandHandler(_repo.Object, _ctx.Object, _evaluator.Object);
        var result  = await handler.Handle(new EvaluateFeatureFlagCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    #endregion
}
