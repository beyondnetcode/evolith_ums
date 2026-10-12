namespace Evolith.Ums.Infrastructure.Observability;

/// <summary>
/// Constantes de observabilidad propias de UMS (ADR-0046).
///
/// Sustituyen al andamiaje que se vendorizó bajo el namespace de los shells
/// (<c>BeyondNetCode.Shell.Aop.Aspects.Logger.Serilog</c>, gap G-015): ese shim se
/// retiró al unificar la correlación en W3C TraceContext. La correlación deja de
/// generarse a mano (ya no hay <c>X-Correlation-Id</c>): el <c>trace_id</c>/<c>span_id</c>
/// se derivan de <see cref="System.Diagnostics.Activity.Current"/> / <c>traceparent</c>.
///
/// Se conserva únicamente el <b>SessionTrackingId</b>, que es un identificador de
/// producto de UMS (rastreo de sesión de negocio) y no forma parte del estándar W3C.
/// </summary>
public static class ObservabilityHeaders
{
    /// <summary>Cabecera HTTP del identificador de rastreo de sesión de negocio de UMS.</summary>
    public const string SessionTrackingId = "X-Session-Tracking-Id";
}

/// <summary>Claves de baggage/etiquetas de la <see cref="System.Diagnostics.Activity"/> propias de UMS.</summary>
public static class ObservabilityKeys
{
    /// <summary>Clave de baggage/etiqueta para el identificador de rastreo de sesión.
    /// ADR-0096 §2.1 regla 1: los atributos propios viven bajo el prefijo <c>evolith.</c>.</summary>
    public const string SessionTrackingId = "evolith.session.tracking_id";

    // --- Vocabulario de trazabilidad funcional (ADR-0096 §2.2–§2.4) ---
    // Todos bajo el prefijo canónico `evolith.`. Son a la vez etiquetas de span (Tempo)
    // y propiedades de log estructurado (Loki), lo que habilita la búsqueda sin ID
    // (ADR-UMS-085): consulta del store de observabilidad por estos atributos.

    /// <summary>Localizador legible de la transacción funcional (<c>TX-AAAA-NNNNNN</c>).</summary>
    public const string TransactionId = "evolith.transaction.id";

    /// <summary>Nombre funcional de la transacción (acción del actor).</summary>
    public const string TransactionName = "evolith.transaction.name";

    /// <summary>Localizador de la transacción padre, si esta es hija de otra.</summary>
    public const string TransactionParentId = "evolith.transaction.parent_id";

    /// <summary>Estado de la transacción (valores de <see cref="TransactionStateNames"/>).</summary>
    public const string TransactionState = "evolith.transaction.state";

    /// <summary>Actor que originó la transacción.</summary>
    public const string TransactionActor = "evolith.transaction.actor";

    /// <summary>Duración total de la transacción en milisegundos (en el desenlace).</summary>
    public const string TransactionDurationMs = "evolith.transaction.duration_ms";

    /// <summary>Etapas completadas al momento del desenlace.</summary>
    public const string TransactionStagesCompleted = "evolith.transaction.stages_completed";

    /// <summary>Etapas fallidas al momento del desenlace.</summary>
    public const string TransactionStagesFailed = "evolith.transaction.stages_failed";

    /// <summary>Marca el evento de log/span como el <b>desenlace</b> de la transacción.</summary>
    public const string TransactionOutcome = "evolith.transaction.outcome";

    /// <summary>Nombre de la etapa de negocio en curso.</summary>
    public const string Stage = "evolith.stage";

    /// <summary>Estado de la etapa (<c>completed</c> | <c>failed</c>).</summary>
    public const string StageStatus = "evolith.stage.status";

    /// <summary>Decisión de negocio tomada.</summary>
    public const string Decision = "evolith.decision";

    /// <summary>El porqué de la decisión.</summary>
    public const string DecisionReason = "evolith.decision.reason";

    /// <summary>Tipo de efecto sobre el mundo externo (p. ej. <c>message.publish</c>).</summary>
    public const string EffectType = "evolith.effect.type";

    /// <summary>Objetivo del efecto (recurso afectado).</summary>
    public const string EffectTarget = "evolith.effect.target";

    /// <summary>Referencia del efecto (id, clave o identificador del resultado).</summary>
    public const string EffectRef = "evolith.effect.ref";

    /// <summary>Reversibilidad del efecto (<c>compensado</c> | <c>pendiente-de-compensar</c> | <c>irreversible</c>).</summary>
    public const string EffectReversibility = "evolith.effect.reversibility";
}

/// <summary>
/// Nombres canónicos de los estados de transacción (ADR-0095, referidos por ADR-0096 §2.3).
/// Son los valores que viajan en la etiqueta <see cref="ObservabilityKeys.TransactionState"/>.
/// </summary>
public static class TransactionStateNames
{
    public const string Started = "STARTED";
    public const string InProgress = "IN_PROGRESS";
    public const string Waiting = "WAITING";
    public const string Retrying = "RETRYING";
    public const string Completed = "COMPLETED";
    public const string PartiallyCompleted = "PARTIALLY_COMPLETED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
    public const string TimedOut = "TIMED_OUT";
}
