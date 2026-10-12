namespace Evolith.Ums.Domain.Test.IGA;

using Evolith.Ums.Domain.IGA.RoleMaturityStatus;
using Xunit;

/// <summary>
/// Pruebas de dominio del agregado <see cref="RoleMaturityStatus"/> (ADR-UMS-093, IGA).
///
/// Cobertura por invariante:
///   INV-RMS1 — PerformanceScore ∈ [0, 5] (límites y fuera de rango).
///   INV-RMS2 — actualización de nivel debe apuntar a un nivel distinto.
///   INV-RMS3 — elegibilidad: sin incidencias, desempeño ≥ 3.0, tiempo mínimo en nivel;
///              Principal no promocionable.
/// Se verifica además la emisión de eventos de dominio por transición.
/// </summary>
public class RoleMaturityStatusTests
{
    private static readonly TenantId Tenant = TenantId.Create();
    private static readonly UserId User = UserId.Create();
    private static readonly RoleId Role = RoleId.Create();
    private static readonly ActorId Actor = ActorId.Create("actor-001");
    private static readonly DateTime Now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static RoleMaturityStatus Make(
        RoleMaturityLevel level = RoleMaturityLevel.Junior,
        DateTime? assignedAt = null)
        => RoleMaturityStatus.Create(Tenant, User, Role, level, assignedAt ?? Now, Actor).Value;

    // ── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = RoleMaturityStatus.Create(Tenant, User, Role, RoleMaturityLevel.Junior, Now, Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RoleMaturityLevel.Junior, result.Value.CurrentMaturityLevel);
        Assert.True(result.Value.HasNoComplianceIssues);
        Assert.Equal(0m, result.Value.PerformanceScore);
        Assert.Null(result.Value.NextEligibleMaturityLevel);
    }

    [Fact]
    public void Create_RaisesRoleMaturityStatusCreatedEvent()
    {
        var status = Make();

        var events = status.DomainEvents.GetUncommittedChanges();
        Assert.Contains(events, e => e is RoleMaturityStatusCreatedEvent);
    }

    // ── INV-RMS1 — PerformanceScore ∈ [0, 5] ──────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(2.5)]
    [InlineData(5)]
    public void UpdatePerformanceScore_WithinRange_ReturnsSuccess(double score)
    {
        var status = Make();

        var result = status.UpdatePerformanceScore((decimal)score, Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal((decimal)score, status.PerformanceScore);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(5.1)]
    [InlineData(100)]
    public void UpdatePerformanceScore_OutOfRange_ReturnsFailure(double score)
    {
        var status = Make();

        var result = status.UpdatePerformanceScore((decimal)score, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.InvalidPerformanceScore, result.Error);
    }

    // ── INV-RMS2 — nivel distinto ─────────────────────────────────────────────

    [Fact]
    public void UpdateMaturityLevel_ToDifferentLevel_ReturnsSuccess()
    {
        var status = Make(RoleMaturityLevel.Junior);

        var result = status.UpdateMaturityLevel(RoleMaturityLevel.Intermediate, Now, Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RoleMaturityLevel.Intermediate, status.CurrentMaturityLevel);
        Assert.Contains(status.DomainEvents.GetUncommittedChanges(), e => e is RoleMaturityLevelChangedEvent);
    }

    [Fact]
    public void UpdateMaturityLevel_ToSameLevel_ReturnsFailure()
    {
        var status = Make(RoleMaturityLevel.Junior);

        var result = status.UpdateMaturityLevel(RoleMaturityLevel.Junior, Now, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.MaturityLevelUnchanged, result.Error);
    }

    [Fact]
    public void UpdateMaturityLevel_ResetsEligibilityMarkers()
    {
        // Elegible primero, luego un cambio de nivel debe limpiar los marcadores.
        var status = Make(RoleMaturityLevel.Junior, Now.AddMonths(-7));
        status.UpdatePerformanceScore(4.0m, Actor);
        status.EvaluateEligibility(Now, Actor);
        Assert.NotNull(status.NextEligibleMaturityLevel);

        status.UpdateMaturityLevel(RoleMaturityLevel.Intermediate, Now, Actor);

        Assert.Null(status.NextEligibleMaturityLevel);
        Assert.Null(status.EligibleForPromotionAt);
    }

    // ── INV-RMS3 — elegibilidad ───────────────────────────────────────────────

    [Fact]
    public void EvaluateEligibility_WhenAllConditionsMet_ReturnsSuccess()
    {
        var status = Make(RoleMaturityLevel.Junior, Now.AddMonths(-7)); // > 6 meses
        status.UpdatePerformanceScore(3.0m, Actor);

        var result = status.EvaluateEligibility(Now, Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RoleMaturityLevel.Intermediate, status.NextEligibleMaturityLevel);
        Assert.Equal(Now, status.EligibleForPromotionAt);
        Assert.Contains(status.DomainEvents.GetUncommittedChanges(), e => e is RoleMaturityEligibilityConfirmedEvent);
    }

    [Fact]
    public void EvaluateEligibility_WithActiveComplianceIssue_ReturnsFailure()
    {
        var status = Make(RoleMaturityLevel.Junior, Now.AddMonths(-7));
        status.UpdatePerformanceScore(4.0m, Actor);
        status.MarkComplianceIssue(TextValueObject.Create("sancion abierta"), Actor);

        var result = status.EvaluateEligibility(Now, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.ComplianceIssuesBlockPromotion, result.Error);
    }

    [Fact]
    public void EvaluateEligibility_WithInsufficientScore_ReturnsFailure()
    {
        var status = Make(RoleMaturityLevel.Junior, Now.AddMonths(-7));
        status.UpdatePerformanceScore(2.9m, Actor);

        var result = status.EvaluateEligibility(Now, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.InsufficientPerformanceScore, result.Error);
    }

    [Fact]
    public void EvaluateEligibility_WithInsufficientTimeInLevel_ReturnsFailure()
    {
        var status = Make(RoleMaturityLevel.Junior, Now.AddMonths(-2)); // < 6 meses
        status.UpdatePerformanceScore(4.0m, Actor);

        var result = status.EvaluateEligibility(Now, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.InsufficientTimeInLevel, result.Error);
    }

    [Theory]
    [InlineData(2, RoleMaturityLevel.Intermediate, 12)] // Intermediate → Senior: 12m
    [InlineData(3, RoleMaturityLevel.Senior, 18)]       // Senior → Lead: 18m
    [InlineData(4, RoleMaturityLevel.Lead, 24)]         // Lead → Principal: 24m
    public void EvaluateEligibility_RespectsPerLevelTimeThreshold(int _, RoleMaturityLevel level, int requiredMonths)
    {
        var justUnder = Make(level, Now.AddMonths(-(requiredMonths - 1)));
        justUnder.UpdatePerformanceScore(4.0m, Actor);
        Assert.True(justUnder.EvaluateEligibility(Now, Actor).IsFailure);

        var justOver = Make(level, Now.AddMonths(-(requiredMonths + 1)));
        justOver.UpdatePerformanceScore(4.0m, Actor);
        Assert.True(justOver.EvaluateEligibility(Now, Actor).IsSuccess);
    }

    [Fact]
    public void EvaluateEligibility_WhenPrincipal_ReturnsFailure()
    {
        var status = Make(RoleMaturityLevel.Principal, Now.AddMonths(-60));
        status.UpdatePerformanceScore(5.0m, Actor);

        var result = status.EvaluateEligibility(Now, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.MaturityLevelAlreadyMax, result.Error);
    }

    // ── Cumplimiento: marcar / resolver ───────────────────────────────────────

    [Fact]
    public void MarkComplianceIssue_SetsBlockAndRaisesEvent()
    {
        var status = Make();

        var result = status.MarkComplianceIssue(TextValueObject.Create("documento vencido"), Actor);

        Assert.True(result.IsSuccess);
        Assert.False(status.HasNoComplianceIssues);
        Assert.Contains(status.DomainEvents.GetUncommittedChanges(), e => e is RoleMaturityComplianceIssueMarkedEvent);
    }

    [Fact]
    public void ResolveComplianceIssue_ClearsBlockAndRaisesEvent()
    {
        var status = Make();
        status.MarkComplianceIssue(TextValueObject.Create("documento vencido"), Actor);

        var result = status.ResolveComplianceIssue(Actor);

        Assert.True(result.IsSuccess);
        Assert.True(status.HasNoComplianceIssues);
        Assert.Contains(status.DomainEvents.GetUncommittedChanges(), e => e is RoleMaturityComplianceIssueResolvedEvent);
    }

    [Fact]
    public void MarkComplianceIssue_WithEmptyFactor_ReturnsFailure()
    {
        var status = Make();

        var result = status.MarkComplianceIssue(TextValueObject.Create(""), Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.BlockingFactorRequired, result.Error);
    }
}
