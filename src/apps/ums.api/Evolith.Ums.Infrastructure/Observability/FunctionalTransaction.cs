namespace Evolith.Ums.Infrastructure.Observability;

using System.Diagnostics;
using Evolith.Ums.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Transacción funcional con alcance de petición (ADR-0096; decisión UMS en ADR-UMS-085).
/// Modela el proceso como un relato y emite la narrativa —apertura, etapas, decisiones,
/// efectos y <b>desenlace</b>— como logs estructurados enriquecidos con el vocabulario
/// <c>evolith.transaction.*</c> / <c>evolith.stage</c> / <c>evolith.effect.*</c>, además de
/// etiquetas sobre la <see cref="Activity"/> en curso para correlación en Tempo.
///
/// El <b>desenlace</b> lo garantiza el middleware por construcción
/// (<c>FunctionalTransactionMiddleware</c>): abre con <see cref="Open"/> y cierra siempre
/// con <see cref="CompleteAsync"/>, incluso en la ruta de fallo. Una apertura sin desenlace
/// es un defecto alertable, detectable en el store de observabilidad.
/// </summary>
public sealed class FunctionalTransaction : IFunctionalTransaction
{
    private readonly ILogger<FunctionalTransaction> _logger;
    private readonly ITransactionLocatorFactory _locatorFactory;
    private readonly SemaphoreSlim _mintGate = new(1, 1);

    private long _startTimestamp;
    private int _stagesCompleted;
    private int _stagesFailed;
    private bool _opened;
    private bool _completed;

    public FunctionalTransaction(
        ILogger<FunctionalTransaction> logger,
        ITransactionLocatorFactory locatorFactory)
    {
        _logger = logger;
        _locatorFactory = locatorFactory;
    }

    public string? Locator { get; private set; }

    public string Name { get; private set; } = "(sin nombre)";

    public string? Actor { get; private set; }

    public TransactionState State { get; private set; } = TransactionState.Started;

    // --- Superficie del middleware (apertura y desenlace) ---

    /// <summary>Abre la transacción: registra la apertura de la narrativa.</summary>
    public void Open(string name, string? actor)
    {
        if (_opened)
        {
            return;
        }

        _opened = true;
        Name = string.IsNullOrWhiteSpace(name) ? Name : name;
        Actor = actor;
        State = TransactionState.Started;
        _startTimestamp = Stopwatch.GetTimestamp();

        var activity = Activity.Current;
        activity?.SetTag(ObservabilityKeys.TransactionName, Name);
        activity?.SetTag(ObservabilityKeys.TransactionActor, Actor);
        activity?.SetTag(ObservabilityKeys.TransactionState, TransactionStateNames.Started);

        using (BeginNarrativeScope(TransactionStateNames.Started))
        {
            _logger.LogInformation(
                "Apertura de transacción funcional «{TransactionName}» por {Actor}.",
                Name,
                Actor ?? "(anónimo)");
        }
    }

    /// <summary>Actualiza el actor una vez resuelta la autenticación.</summary>
    public void SetActor(string? actor)
    {
        Actor = actor;
        Activity.Current?.SetTag(ObservabilityKeys.TransactionActor, actor);
    }

    /// <summary>
    /// Cierra la transacción emitiendo el <b>desenlace</b> (siempre, incluso en fallo).
    /// No lanza: el desenlace no debe convertirse él mismo en un fallo silencioso.
    /// </summary>
    public async Task CompleteAsync(
        TransactionState finalState,
        int statusCode,
        CancellationToken cancellationToken = default)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;

        // Reconciliación con las etapas: un éxito nominal con etapas fallidas es parcial.
        if (finalState == TransactionState.Completed && _stagesFailed > 0)
        {
            finalState = _stagesCompleted > 0
                ? TransactionState.PartiallyCompleted
                : TransactionState.Failed;
        }

        State = finalState;

        // El localizador legible se muestra al usuario ante un desenlace no exitoso.
        if (IsUnsuccessful(finalState))
        {
            try
            {
                await GetOrMintLocatorAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo acuñar el localizador en el desenlace de la transacción.");
            }
        }

        var durationMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
        var stateName = ToStateName(finalState);

        var activity = Activity.Current;
        activity?.SetTag(ObservabilityKeys.TransactionState, stateName);
        activity?.SetTag(ObservabilityKeys.TransactionOutcome, true);
        activity?.SetTag(ObservabilityKeys.TransactionDurationMs, durationMs);

        using (BeginDesenlaceScope(stateName, durationMs))
        {
            var level = IsUnsuccessful(finalState) ? LogLevel.Warning : LogLevel.Information;
            _logger.Log(
                level,
                "Desenlace de transacción funcional «{TransactionName}»: {TransactionOutcomeState} "
                + "(HTTP {StatusCode}) en {DurationMs:F1} ms; etapas {StagesCompleted} completadas, "
                + "{StagesFailed} fallidas; referencia {TransactionLocator}.",
                Name,
                stateName,
                statusCode,
                durationMs,
                _stagesCompleted,
                _stagesFailed,
                Locator ?? "(sin localizador — identificada por traceId)");
        }
    }

    // --- Puerto IFunctionalTransaction (uso desde la capa de aplicación) ---

    public async Task<string> GetOrMintLocatorAsync(CancellationToken cancellationToken = default)
    {
        if (Locator is not null)
        {
            return Locator;
        }

        await _mintGate.WaitAsync(cancellationToken);
        try
        {
            if (Locator is null)
            {
                Locator = await _locatorFactory.NextAsync(cancellationToken);
                Activity.Current?.SetTag(ObservabilityKeys.TransactionId, Locator);
            }
        }
        finally
        {
            _mintGate.Release();
        }

        return Locator;
    }

    public void RecordStage(string stage, string? detail = null, bool failed = false)
    {
        if (failed)
        {
            _stagesFailed++;
        }
        else
        {
            _stagesCompleted++;
        }

        var status = failed ? "failed" : "completed";
        var activity = Activity.Current;
        activity?.AddEvent(new ActivityEvent(
            stage,
            tags: new ActivityTagsCollection
            {
                [ObservabilityKeys.Stage] = stage,
                [ObservabilityKeys.StageStatus] = status,
            }));

        using (BeginScope(new Dictionary<string, object>
        {
            [ObservabilityKeys.Stage] = stage,
            [ObservabilityKeys.StageStatus] = status,
        }))
        {
            _logger.Log(
                failed ? LogLevel.Warning : LogLevel.Information,
                "Etapa «{Stage}» {StageStatus}{StageDetail}.",
                stage,
                status,
                string.IsNullOrWhiteSpace(detail) ? string.Empty : $": {detail}");
        }
    }

    public void RecordDecision(string decision, string reason)
    {
        using (BeginScope(new Dictionary<string, object>
        {
            [ObservabilityKeys.Decision] = decision,
            [ObservabilityKeys.DecisionReason] = reason,
        }))
        {
            _logger.LogInformation(
                "Decisión «{Decision}» porque {DecisionReason}.",
                decision,
                reason);
        }
    }

    public void RecordEffect(
        string effectType,
        string target,
        string? reference,
        EffectReversibility reversibility)
    {
        var reversibilityName = ToReversibilityName(reversibility);

        var activity = Activity.Current;
        activity?.AddEvent(new ActivityEvent(
            "effect",
            tags: new ActivityTagsCollection
            {
                [ObservabilityKeys.EffectType] = effectType,
                [ObservabilityKeys.EffectTarget] = target,
                [ObservabilityKeys.EffectRef] = reference,
                [ObservabilityKeys.EffectReversibility] = reversibilityName,
            }));

        using (BeginScope(new Dictionary<string, object>
        {
            [ObservabilityKeys.EffectType] = effectType,
            [ObservabilityKeys.EffectTarget] = target,
            [ObservabilityKeys.EffectRef] = reference ?? string.Empty,
            [ObservabilityKeys.EffectReversibility] = reversibilityName,
        }))
        {
            _logger.LogInformation(
                "Efecto «{EffectType}» sobre {EffectTarget} (ref {EffectRef}, {EffectReversibility}).",
                effectType,
                target,
                reference ?? "(sin ref)",
                reversibilityName);
        }
    }

    public void MarkState(TransactionState state)
    {
        State = state;
        Activity.Current?.SetTag(ObservabilityKeys.TransactionState, ToStateName(state));
    }

    // --- Auxiliares ---

    private IDisposable? BeginNarrativeScope(string stateName)
        => BeginScope(new Dictionary<string, object>
        {
            [ObservabilityKeys.TransactionName] = Name,
            [ObservabilityKeys.TransactionState] = stateName,
            [ObservabilityKeys.TransactionActor] = Actor ?? "(anónimo)",
        });

    private IDisposable? BeginDesenlaceScope(string stateName, double durationMs)
        => BeginScope(new Dictionary<string, object>
        {
            [ObservabilityKeys.TransactionName] = Name,
            [ObservabilityKeys.TransactionState] = stateName,
            [ObservabilityKeys.TransactionOutcome] = true,
            [ObservabilityKeys.TransactionActor] = Actor ?? "(anónimo)",
            [ObservabilityKeys.TransactionDurationMs] = durationMs,
            [ObservabilityKeys.TransactionStagesCompleted] = _stagesCompleted,
            [ObservabilityKeys.TransactionStagesFailed] = _stagesFailed,
            [ObservabilityKeys.TransactionId] = Locator ?? string.Empty,
        });

    private IDisposable? BeginScope(Dictionary<string, object> state)
        => _logger.BeginScope(state);

    private static bool IsUnsuccessful(TransactionState state)
        => state is TransactionState.Failed
            or TransactionState.PartiallyCompleted
            or TransactionState.TimedOut
            or TransactionState.Cancelled;

    private static string ToStateName(TransactionState state) => state switch
    {
        TransactionState.Started => TransactionStateNames.Started,
        TransactionState.InProgress => TransactionStateNames.InProgress,
        TransactionState.Waiting => TransactionStateNames.Waiting,
        TransactionState.Retrying => TransactionStateNames.Retrying,
        TransactionState.Completed => TransactionStateNames.Completed,
        TransactionState.PartiallyCompleted => TransactionStateNames.PartiallyCompleted,
        TransactionState.Failed => TransactionStateNames.Failed,
        TransactionState.Cancelled => TransactionStateNames.Cancelled,
        TransactionState.TimedOut => TransactionStateNames.TimedOut,
        _ => TransactionStateNames.Failed,
    };

    private static string ToReversibilityName(EffectReversibility reversibility) => reversibility switch
    {
        EffectReversibility.Compensated => "compensado",
        EffectReversibility.PendingCompensation => "pendiente-de-compensar",
        EffectReversibility.Irreversible => "irreversible",
        _ => "pendiente-de-compensar",
    };
}
