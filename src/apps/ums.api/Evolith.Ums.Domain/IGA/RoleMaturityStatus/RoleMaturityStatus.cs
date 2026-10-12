namespace Evolith.Ums.Domain.IGA.RoleMaturityStatus;

using Evolith.Ums.Domain.Events;
using Evolith.Ums.Domain.IGA.RoleMaturityStatus.Events;

/// <summary>
/// Agregado raíz de elegibilidad del contexto acotado IGA (ADR-UMS-093, FR-062).
/// Rastrea el nivel de madurez de un usuario en un rol y decide la elegibilidad
/// para promoción (fuente de verdad de elegibilidad que consume
/// <see cref="RolePromotionRequest.RolePromotionRequest"/> al hacer <c>Submit</c>).
///
/// Invariantes:
///   INV-RMS1 — PerformanceScore ∈ [0, 5].
///   INV-RMS2 — una actualización de nivel debe apuntar a un nivel distinto del actual.
///   INV-RMS3 — elegibilidad: sin incidencias de cumplimiento, desempeño ≥ 3.0 y
///              tiempo mínimo en nivel por salto (6/12/18/24 meses); Principal no promocionable.
///
/// Las operaciones devuelven <see cref="Result"/> (nunca excepción) y las transiciones
/// relevantes emiten un evento de dominio auditable.
/// </summary>
public sealed class RoleMaturityStatus : AggregateRoot<RoleMaturityStatus, RoleMaturityStatusProps>
{
    public const decimal MinPerformanceScore = 0m;
    public const decimal MaxPerformanceScore = 5m;
    public const decimal MinEligibleScore = 3.0m;

    public new RoleMaturityStatusEventsManager DomainEvents { get; }

    private RoleMaturityStatus(RoleMaturityStatusProps props) : base(props)
    {
        DomainEvents = new RoleMaturityStatusEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new RoleMaturityStatusCreatedEvent(
                Props.TenantId.GetValue(),
                Props.Id.GetValue(),
                Props.UserId.GetValue(),
                Props.RoleId.GetValue(),
                Props.CurrentMaturityLevel.ToString()));
        }
    }

    public RoleMaturityStatusId GetId() => RoleMaturityStatusId.Load(Props.Id.GetValue());
    public TenantId TenantId => Props.TenantId;
    public UserId UserId => Props.UserId;
    public RoleId RoleId => Props.RoleId;
    public RoleMaturityLevel CurrentMaturityLevel => Props.CurrentMaturityLevel;
    public RoleMaturityLevel? NextEligibleMaturityLevel => Props.NextEligibleMaturityLevel;
    public DateTime AssignedAt => Props.AssignedAt;
    public DateTime CurrentLevelSince => Props.CurrentLevelSince;
    public DateTime? EligibleForPromotionAt => Props.EligibleForPromotionAt;
    public int CompletedCertificationsCount => Props.CompletedCertificationsCount;
    public int CompletedTrainingsCount => Props.CompletedTrainingsCount;
    public decimal PerformanceScore => Props.PerformanceScore;
    public bool HasNoComplianceIssues => Props.HasNoComplianceIssues;
    public TextValueObject? BlockingFactor => Props.BlockingFactor;
    public DateTime? LastReviewedAt => Props.LastReviewedAt;

    public static Result<RoleMaturityStatus> Create(
        TenantId tenantId,
        UserId userId,
        RoleId roleId,
        RoleMaturityLevel currentMaturityLevel,
        DateTime assignedAtUtc,
        ActorId createdBy)
    {
        var props = new RoleMaturityStatusProps(
            RoleMaturityStatusId.Create(),
            tenantId,
            userId,
            roleId,
            currentMaturityLevel,
            assignedAtUtc,
            createdBy);

        var status = new RoleMaturityStatus(props);

        if (!status.IsValid())
        {
            return Result<RoleMaturityStatus>.Failure(status.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<RoleMaturityStatus>.Success(status);
    }

    /// <summary>INV-RMS1: registra una puntuación de desempeño dentro de [0, 5].</summary>
    public Result UpdatePerformanceScore(decimal performanceScore, ActorId actorId)
    {
        if (performanceScore < MinPerformanceScore || performanceScore > MaxPerformanceScore)
        {
            BrokenRules.Add(new BrokenRule(nameof(PerformanceScore), DomainErrors.IGA.InvalidPerformanceScore));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var previous = Props.PerformanceScore;
        SetProps(Props.WithPerformanceScore(performanceScore));
        DomainEvents.RaiseEvent(new RoleMaturityPerformanceScoreUpdatedEvent(
            Props.TenantId.GetValue(), Props.Id.GetValue(), previous, performanceScore));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    /// <summary>INV-RMS2: eleva/ajusta el nivel de madurez a un nivel distinto del actual.</summary>
    public Result UpdateMaturityLevel(RoleMaturityLevel newLevel, DateTime changedAtUtc, ActorId actorId)
    {
        if (newLevel == Props.CurrentMaturityLevel)
        {
            BrokenRules.Add(new BrokenRule(nameof(CurrentMaturityLevel), DomainErrors.IGA.MaturityLevelUnchanged));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var from = Props.CurrentMaturityLevel;
        SetProps(Props.WithMaturityLevel(newLevel, changedAtUtc));
        DomainEvents.RaiseEvent(new RoleMaturityLevelChangedEvent(
            Props.TenantId.GetValue(), Props.Id.GetValue(), from.ToString(), newLevel.ToString()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    /// <summary>Marca una incidencia de cumplimiento activa (bloquea la elegibilidad, INV-RMS3).</summary>
    public Result MarkComplianceIssue(TextValueObject blockingFactor, ActorId actorId)
    {
        if (blockingFactor is null || string.IsNullOrWhiteSpace(blockingFactor.GetValue()))
        {
            BrokenRules.Add(new BrokenRule(nameof(BlockingFactor), DomainErrors.IGA.BlockingFactorRequired));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithComplianceIssue(blockingFactor!));
        DomainEvents.RaiseEvent(new RoleMaturityComplianceIssueMarkedEvent(
            Props.TenantId.GetValue(), Props.Id.GetValue(), blockingFactor!.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    /// <summary>Resuelve la incidencia de cumplimiento activa (desbloquea la elegibilidad).</summary>
    public Result ResolveComplianceIssue(ActorId actorId)
    {
        SetProps(Props.WithComplianceCleared());
        DomainEvents.RaiseEvent(new RoleMaturityComplianceIssueResolvedEvent(
            Props.TenantId.GetValue(), Props.Id.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// INV-RMS3: evalúa la elegibilidad para promoción al siguiente nivel a la fecha
    /// <paramref name="asOfUtc"/>. Fail-closed: cualquier condición incumplida devuelve
    /// <c>Result.Failure</c>. En éxito fija <c>NextEligibleMaturityLevel</c> y
    /// <c>EligibleForPromotionAt</c>, y emite <see cref="RoleMaturityEligibilityConfirmedEvent"/>.
    /// El instante de evaluación se recibe por parámetro para mantener el dominio determinista.
    /// </summary>
    public Result EvaluateEligibility(DateTime asOfUtc, ActorId actorId)
    {
        // INV-RMS3: Principal no es promocionable.
        if (Props.CurrentMaturityLevel == RoleMaturityLevel.Principal)
        {
            BrokenRules.Add(new BrokenRule(nameof(CurrentMaturityLevel), DomainErrors.IGA.MaturityLevelAlreadyMax));
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        if (!Props.HasNoComplianceIssues)
        {
            BrokenRules.Add(new BrokenRule(nameof(HasNoComplianceIssues), DomainErrors.IGA.ComplianceIssuesBlockPromotion));
        }

        if (Props.PerformanceScore < MinEligibleScore)
        {
            BrokenRules.Add(new BrokenRule(nameof(PerformanceScore), DomainErrors.IGA.InsufficientPerformanceScore));
        }

        var requiredMonths = RequiredMonthsInLevel(Props.CurrentMaturityLevel);
        if (WholeMonthsBetween(Props.CurrentLevelSince, asOfUtc) < requiredMonths)
        {
            BrokenRules.Add(new BrokenRule(nameof(CurrentLevelSince), DomainErrors.IGA.InsufficientTimeInLevel));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var nextLevel = NextLevelOf(Props.CurrentMaturityLevel);
        SetProps(Props.WithEligibility(nextLevel, asOfUtc));
        DomainEvents.RaiseEvent(new RoleMaturityEligibilityConfirmedEvent(
            Props.TenantId.GetValue(),
            Props.Id.GetValue(),
            Props.UserId.GetValue(),
            Props.CurrentMaturityLevel.ToString(),
            nextLevel.ToString(),
            asOfUtc));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    public Result RecordCertificationCompletion(ActorId actorId)
    {
        SetProps(Props.WithCertificationRecorded());
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    public Result RecordTrainingCompletion(ActorId actorId)
    {
        SetProps(Props.WithTrainingRecorded());
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    // ── Reglas de progresión determinista (ADR-UMS-093 §Decisiones Técnicas) ────

    private static RoleMaturityLevel NextLevelOf(RoleMaturityLevel level)
        => (RoleMaturityLevel)((int)level + 1);

    private static int RequiredMonthsInLevel(RoleMaturityLevel level) => level switch
    {
        RoleMaturityLevel.Junior       => 6,
        RoleMaturityLevel.Intermediate => 12,
        RoleMaturityLevel.Senior       => 18,
        RoleMaturityLevel.Lead         => 24,
        _                              => int.MaxValue, // Principal: no promocionable
    };

    /// <summary>Meses completos transcurridos entre dos instantes (0 si <paramref name="end"/> ≤ <paramref name="start"/>).</summary>
    private static int WholeMonthsBetween(DateTime start, DateTime end)
    {
        if (end <= start)
        {
            return 0;
        }

        var months = ((end.Year - start.Year) * 12) + end.Month - start.Month;
        if (end.Day < start.Day)
        {
            months--;
        }

        return months < 0 ? 0 : months;
    }
}
