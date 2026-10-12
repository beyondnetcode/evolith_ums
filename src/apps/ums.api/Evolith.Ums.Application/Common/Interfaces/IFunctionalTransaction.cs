namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// Estados del ciclo de vida de una transacción funcional (ADR-0095, adoptado por
/// ADR-0096 §2.3). El <b>desenlace</b> siempre cae en uno de estos estados; una
/// transacción sin desenlace es un defecto alertable.
/// </summary>
public enum TransactionState
{
    /// <summary>La transacción se abrió (apertura de la narrativa).</summary>
    Started,

    /// <summary>Hay etapas en curso.</summary>
    InProgress,

    /// <summary>A la espera de un recurso o intervención externa.</summary>
    Waiting,

    /// <summary>Reintentando una etapa fallida de forma controlada.</summary>
    Retrying,

    /// <summary>Desenlace exitoso: todas las etapas se completaron.</summary>
    Completed,

    /// <summary>Desenlace parcial: unas etapas se completaron y otras no.</summary>
    PartiallyCompleted,

    /// <summary>Desenlace fallido.</summary>
    Failed,

    /// <summary>La transacción se canceló (p. ej. el cliente abortó la petición).</summary>
    Cancelled,

    /// <summary>La transacción excedió su tiempo límite.</summary>
    TimedOut,
}

/// <summary>
/// Reversibilidad de un efecto sobre el mundo externo (ADR-0096 §2.4).
/// </summary>
public enum EffectReversibility
{
    /// <summary>El efecto ya fue compensado.</summary>
    Compensated,

    /// <summary>El efecto está pendiente de compensación.</summary>
    PendingCompensation,

    /// <summary>El efecto es irreversible.</summary>
    Irreversible,
}

/// <summary>
/// Puerto de la <b>transacción funcional</b> (ADR-0096, contrato funcional de trazabilidad;
/// decidido para UMS en <c>ADR-UMS-085</c>). Modela el proceso como un relato:
/// apertura → etapas → decisiones (el porqué) → efectos → <b>desenlace siempre</b>.
///
/// El modelo vive en un middleware que abre y cierra la transacción (garantizando el
/// desenlace por construcción) y emite la narrativa como logs estructurados enriquecidos
/// con atributos <c>evolith.transaction.*</c> / <c>evolith.stage</c> / <c>evolith.effect.*</c>
/// sobre Serilog + OpenTelemetry. La capa de aplicación usa este puerto para registrar
/// las etapas, decisiones y efectos con significado de negocio.
/// </summary>
public interface IFunctionalTransaction
{
    /// <summary>
    /// Localizador legible <c>TX-AAAA-NNNNNN</c> (ADR-UMS-084) si ya se acuñó; <c>null</c>
    /// si la transacción aún se identifica solo por su <c>traceId</c> W3C. Se acuña de forma
    /// perezosa: siempre en operaciones que mutan estado y ante cualquier fallo.
    /// </summary>
    string? Locator { get; }

    /// <summary>Nombre funcional de la transacción (acción del actor).</summary>
    string Name { get; }

    /// <summary>Actor que inició la transacción (usuario autenticado o iniciador anónimo).</summary>
    string? Actor { get; }

    /// <summary>Estado actual de la transacción.</summary>
    TransactionState State { get; }

    /// <summary>
    /// Devuelve el localizador legible, acuñándolo si aún no existe. Es idempotente dentro
    /// de la misma transacción: llamadas sucesivas devuelven el mismo valor.
    /// </summary>
    Task<string> GetOrMintLocatorAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra una <b>etapa</b> con significado de negocio (ADR-0096 §2.3). Si
    /// <paramref name="failed"/> es <c>true</c>, la etapa se contabiliza como fallida y el
    /// desenlace podrá resolverse como parcial o fallido.
    /// </summary>
    void RecordStage(string stage, string? detail = null, bool failed = false);

    /// <summary>Registra una <b>decisión</b> y su porqué (ADR-0096 §2.3).</summary>
    void RecordDecision(string decision, string reason);

    /// <summary>
    /// Registra un <b>efecto</b> sobre el mundo externo con su reversibilidad
    /// (ADR-0096 §2.4). Emite los atributos <c>evolith.effect.type/target/ref</c>.
    /// </summary>
    void RecordEffect(
        string effectType,
        string target,
        string? reference,
        EffectReversibility reversibility);

    /// <summary>Marca explícitamente el estado (p. ej. <c>WAITING</c>, <c>RETRYING</c>).</summary>
    void MarkState(TransactionState state);
}
