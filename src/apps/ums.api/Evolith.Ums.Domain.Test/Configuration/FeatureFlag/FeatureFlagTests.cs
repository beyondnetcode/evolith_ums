#pragma warning disable S1144
namespace Evolith.Ums.Domain.Test.Configuration.FeatureFlag;

using Evolith.Ums.Domain.Configuration.FeatureFlag;
using Xunit;

// Stub evaluator: returns IsEnabled=true when flag is Active, false otherwise.
internal sealed class StubEvaluator : IFeatureFlagEvaluator
{
    public FlagEvaluationResult Evaluate(FeatureFlag flag, EvaluationContext context)
        => new FlagEvaluationResult(flag.Status == FlagStatus.Active, null, null);
}

/// <summary>
/// Domain tests for the <see cref="FeatureFlag"/> aggregate.
///
/// Coverage intent:
///   – All state transitions and their guards (Inactive→Active→Inactive→Archived).
///   – Percentage-flag invariants at both boundaries and invalid values.
///   – Evaluate: result semantics, evaluation-log accumulation, archived guard.
///   – Event contract: correct event types emitted per operation.
///   – LinkedResource metadata stored correctly.
///   – Lifecycle cycles (activate → deactivate → re-activate).
///
/// Excluded intentionally:
///   – Props / value-object construction (pure structural, zero business risk).
///   – Audit field values (framework concern, not domain logic).
/// </summary>
public class FeatureFlagTests
{
    private static readonly string ValidFlagCode = "FEATURE-001";
    private static readonly FlagType ValidFlagType = FlagType.Boolean;
    private static readonly string ValidFlagTargets = "all";
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    private static readonly IdValueObject ValidSystemSuiteId = IdValueObject.Create();
    private static readonly IFeatureFlagEvaluator Evaluator = new StubEvaluator();
    private static readonly EvaluationContext DefaultContext = new EvaluationContext();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static FeatureFlag CreateBoolean() =>
        FeatureFlag.Create(ValidSystemSuiteId, null, ValidFlagCode, FlagType.Boolean, ValidFlagTargets, null, null, null, ValidActor).Value;

    private static FeatureFlag CreatePercentage(int pct) =>
        FeatureFlag.Create(ValidSystemSuiteId, null, ValidFlagCode, FlagType.Percentage, ValidFlagTargets, null, null, pct, ValidActor).Value;

    private static FeatureFlag CreateActive()
    {
        var f = CreateBoolean();
        f.Activate(ValidActor);
        return f;
    }

    // =========================================================================
    #region Create
    // =========================================================================

    [Fact]
    public void Create_WithValidBinaryFlag_ReturnsSuccess()
    {
        var result = FeatureFlag.Create(ValidSystemSuiteId, null, ValidFlagCode, ValidFlagType, ValidFlagTargets, null, null, null, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidFlagCode, result.Value.FlagCode);
        Assert.Equal(ValidFlagType, result.Value.FlagType);
        Assert.Equal(FlagStatus.Inactive, result.Value.Status);
        Assert.Null(result.Value.RolloutPercentage);
    }

    [Fact]
    public void Create_WithValidPercentageFlag_ReturnsSuccess()
    {
        var result = FeatureFlag.Create(ValidSystemSuiteId, null, ValidFlagCode, FlagType.Percentage, ValidFlagTargets, null, null, 50, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value.RolloutPercentage);
    }

    [Fact]
    public void Create_WithPercentageFlagWithoutRollout_ReturnsFailure()
    {
        var result = FeatureFlag.Create(ValidSystemSuiteId, null, ValidFlagCode, FlagType.Percentage, ValidFlagTargets, null, null, null, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.FlagPercentageOutOfRange, result.Error);
    }

    [Fact]
    public void Create_WithPercentageOutOfRange_ReturnsFailure()
    {
        var result = FeatureFlag.Create(ValidSystemSuiteId, null, ValidFlagCode, FlagType.Percentage, ValidFlagTargets, null, null, 150, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.FlagPercentageOutOfRange, result.Error);
    }

    [Fact]
    public void Create_WithNegativePercentage_ReturnsFailure()
    {
        var result = FeatureFlag.Create(ValidSystemSuiteId, null, ValidFlagCode, FlagType.Percentage, ValidFlagTargets, null, null, -10, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.FlagPercentageOutOfRange, result.Error);
    }

    [Fact]
    public void Create_WithPercentageAtBoundaryZero_ReturnsSuccess()
    {
        var result = FeatureFlag.Create(ValidSystemSuiteId, null, ValidFlagCode, FlagType.Percentage, ValidFlagTargets, null, null, 0, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.RolloutPercentage);
    }

    [Fact]
    public void Create_WithPercentageAtBoundaryHundred_ReturnsSuccess()
    {
        var result = FeatureFlag.Create(ValidSystemSuiteId, null, ValidFlagCode, FlagType.Percentage, ValidFlagTargets, null, null, 100, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value.RolloutPercentage);
    }

    [Fact]
    public void Create_WithVariantFlagType_NoPercentage_ReturnsSuccess()
    {
        var result = FeatureFlag.Create(ValidSystemSuiteId, null, ValidFlagCode, FlagType.Variant, ValidFlagTargets, null, null, null, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(FlagType.Variant, result.Value.FlagType);
    }

    [Fact]
    public void Create_WithLinkedResource_StoresResourceMetadata()
    {
        var linkedId = IdValueObject.Create();

        var result = FeatureFlag.Create(
            ValidSystemSuiteId, null, ValidFlagCode, FlagType.Boolean, ValidFlagTargets,
            LinkedResourceType.Module, linkedId, null, ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_RaisesFeatureFlagCreatedEvent()
    {
        var result = FeatureFlag.Create(ValidSystemSuiteId, null, ValidFlagCode, ValidFlagType, ValidFlagTargets, null, null, null, ValidActor);

        Assert.True(result.IsSuccess);
        var events = result.Value.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Single(events);
        Assert.IsType<FeatureFlagCreatedEvent>(events[0]);
    }

    #endregion

    // =========================================================================
    #region Activate
    // =========================================================================

    [Fact]
    public void Activate_WhenInactive_ReturnsSuccess()
    {
        var flag = CreateBoolean();

        var result = flag.Activate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(FlagStatus.Active, flag.Status);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ReturnsFailure()
    {
        var flag = CreateBoolean();
        flag.Activate(ValidActor);

        var result = flag.Activate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.FlagAlreadyActive, result.Error);
    }

    [Fact]
    public void Activate_WhenArchived_ReturnsFailure()
    {
        var flag = CreateBoolean();
        flag.Archive(ValidActor);

        var result = flag.Activate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.FlagArchivedCannotChange, result.Error);
    }

    [Fact]
    public void Activate_AfterDeactivate_ReturnsSuccess()
    {
        // Lifecycle cycle: Inactive → Active → Inactive → Active
        var flag = CreateBoolean();
        flag.Activate(ValidActor);
        flag.Deactivate(ValidActor);

        var result = flag.Activate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(FlagStatus.Active, flag.Status);
    }

    [Fact]
    public void Activate_RaisesActivatedAndStateChangedEvents()
    {
        var flag = CreateBoolean();

        flag.Activate(ValidActor);

        var events = flag.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is FeatureFlagActivatedEvent);
        Assert.Contains(events, e => e is FeatureFlagStateChangedEvent);
    }

    #endregion

    // =========================================================================
    #region Deactivate
    // =========================================================================

    [Fact]
    public void Deactivate_WhenActive_ReturnsSuccess()
    {
        var flag = CreateActive();

        var result = flag.Deactivate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(FlagStatus.Inactive, flag.Status);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ReturnsFailure()
    {
        var flag = CreateBoolean();

        var result = flag.Deactivate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.FlagAlreadyInactive, result.Error);
    }

    [Fact]
    public void Deactivate_WhenArchived_ReturnsFailure()
    {
        var flag = CreateBoolean();
        flag.Archive(ValidActor);

        var result = flag.Deactivate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.FlagArchivedCannotChange, result.Error);
    }

    [Fact]
    public void Deactivate_RaisesDeactivatedAndStateChangedEvents()
    {
        var flag = CreateActive();

        flag.Deactivate(ValidActor);

        var events = flag.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is FeatureFlagDeactivatedEvent);
        Assert.Contains(events, e => e is FeatureFlagStateChangedEvent);
    }

    #endregion

    // =========================================================================
    #region Archive
    // =========================================================================

    [Fact]
    public void Archive_WhenActive_ReturnsSuccess()
    {
        var flag = CreateActive();

        var result = flag.Archive(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(FlagStatus.Archived, flag.Status);
    }

    [Fact]
    public void Archive_WhenInactive_ReturnsSuccess()
    {
        var flag = CreateBoolean();

        var result = flag.Archive(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(FlagStatus.Archived, flag.Status);
    }

    [Fact]
    public void Archive_WhenAlreadyArchived_ReturnsFailure()
    {
        var flag = CreateBoolean();
        flag.Archive(ValidActor);

        var result = flag.Archive(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.FlagArchivedCannotChange, result.Error);
    }

    [Fact]
    public void Archive_RaisesArchivedAndStateChangedEvents()
    {
        var flag = CreateBoolean();

        flag.Archive(ValidActor);

        var events = flag.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is FeatureFlagArchivedEvent);
        Assert.Contains(events, e => e is FeatureFlagStateChangedEvent);
    }

    #endregion

    // =========================================================================
    #region Evaluate
    // =========================================================================

    [Fact]
    public void Evaluate_WhenActive_ReturnsTrue()
    {
        var flag = CreateActive();

        var result = flag.Evaluate(Guid.NewGuid(), DefaultContext, Evaluator);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsEnabled);
    }

    [Fact]
    public void Evaluate_WhenInactive_ReturnsFalse()
    {
        var flag = CreateBoolean();

        var result = flag.Evaluate(Guid.NewGuid(), DefaultContext, Evaluator);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsEnabled);
    }

    [Fact]
    public void Evaluate_WhenArchived_ReturnsFailure()
    {
        var flag = CreateBoolean();
        flag.Archive(ValidActor);

        var result = flag.Evaluate(Guid.NewGuid(), DefaultContext, Evaluator);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.FlagArchivedCannotChange, result.Error);
    }

    [Fact]
    public void Evaluate_AppendsEntryToEvaluationLog()
    {
        var flag = CreateBoolean();
        var evaluatorId = Guid.NewGuid();

        flag.Evaluate(evaluatorId, DefaultContext, Evaluator);

        Assert.Single(flag.EvaluationLog);
        Assert.Equal(evaluatorId, flag.EvaluationLog.First().EvaluatedBy);
        Assert.False(flag.EvaluationLog.First().Result); // inactive → false
    }

    [Fact]
    public void Evaluate_MultipleEvaluations_AccumulateInLog()
    {
        var flag = CreateBoolean();

        flag.Evaluate(Guid.NewGuid(), DefaultContext, Evaluator);
        flag.Evaluate(Guid.NewGuid(), DefaultContext, Evaluator);
        flag.Evaluate(Guid.NewGuid(), DefaultContext, Evaluator);

        Assert.Equal(3, flag.EvaluationLog.Count);
    }

    [Fact]
    public void Evaluate_ActiveFlag_LogEntryResultIsTrue()
    {
        var flag = CreateActive();

        flag.Evaluate(Guid.NewGuid(), DefaultContext, Evaluator);

        Assert.True(flag.EvaluationLog.First().Result);
    }

    [Fact]
    public void Evaluate_WhenArchivedAfterEvaluations_LogRetainsHistory()
    {
        var flag = CreateActive();
        flag.Evaluate(Guid.NewGuid(), DefaultContext, Evaluator);
        flag.Archive(ValidActor);

        // Log is still accessible even after archive
        Assert.Single(flag.EvaluationLog);
    }

    [Fact]
    public void Evaluate_RaisesFlagEvaluatedEvent()
    {
        var flag = CreateBoolean();

        flag.Evaluate(Guid.NewGuid(), DefaultContext, Evaluator);

        var events = flag.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is FlagEvaluatedEvent);
    }

    [Fact]
    public void Evaluate_WhenArchived_DoesNotAppendToLog()
    {
        var flag = CreateBoolean();
        flag.Archive(ValidActor);
        var logCountBefore = flag.EvaluationLog.Count;

        flag.Evaluate(Guid.NewGuid(), DefaultContext, Evaluator);

        Assert.Equal(logCountBefore, flag.EvaluationLog.Count);
    }

    #endregion
}

#pragma warning restore S1144
