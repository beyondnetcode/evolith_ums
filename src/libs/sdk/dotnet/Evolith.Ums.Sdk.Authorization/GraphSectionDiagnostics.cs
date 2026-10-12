namespace Evolith.Ums.Sdk.Authorization;

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Nombres canónicos de las secciones del grafo que el índice consume, tal como viajan en el JSON.
///
/// <para>Se declaran como constantes y no como cadenas sueltas porque son lo que aparece en el
/// registro: una errata en el nombre convierte un aviso útil en ruido que nadie sabe correlacionar
/// con el <c>?include=</c> que lo causó.</para>
/// </summary>
public static class GraphSections
{
    public const string MenuAccess        = "menuAccess";
    public const string DomainPermissions = "domainPermissions";
    public const string Scopes            = "scopes";
    public const string FeatureFlags      = "featureFlags";

    /// <summary>
    /// Sublistas anidadas. No las puede omitir la proyección —viajan dentro de su sección— así que
    /// verlas ausentes significa otra cosa: un productor que no es este UMS, o un grafo construido
    /// a mano. Se nombran igual de explícitamente para que el registro distinga los dos casos.
    /// </summary>
    public const string MenuAccessNodes    = "menuAccess[].nodes";
    public const string MenuNodeActions    = "menuAccess[].nodes[].actions";
    public const string MenuNodeChildren   = "menuAccess[].nodes[].children";
    public const string DomainResourceActions = "domainPermissions[].actions";
}

/// <summary>
/// Una sección del grafo llegó AUSENTE donde el consumidor la necesitaba.
///
/// <para>Ausente no es lo mismo que vacía, y esa es toda la razón de que este tipo exista.
/// <c>domainPermissions: []</c> significa «no hay ninguna denegación explícita»; la clave ausente
/// significa «no sé si las hay». El evaluador de deny-wins concluye lo mismo ante las dos —CONCEDE—
/// y solo una de las dos conclusiones está justificada.</para>
///
/// <para>Solo se lanza si el consumidor lo pide con
/// <see cref="UmsGraphDiagnostics.ThrowOnAbsentSection"/>. Por defecto el índice TOLERA la ausencia
/// —cae a lista vacía, que es fail-closed— para no tumbar a un satélite por un grafo recortado;
/// pero la tolerancia no es silencio: la ausencia se registra siempre.</para>
/// </summary>
public sealed class GraphSectionMissingException : InvalidOperationException
{
    /// <summary>Nombre de la sección ausente, tal como viaja en el JSON (ver <see cref="GraphSections"/>).</summary>
    public string SectionName { get; }

    public GraphSectionMissingException(string sectionName)
        : base($"La sección '{sectionName}' del grafo de autorización llegó AUSENTE, no vacía. " +
               "Una sección ausente y una vacía llevan al evaluador a la misma conclusión —no hay " +
               "denegaciones— y solo una de las dos es cierta. Si el grafo se pidió con `?include=`, " +
               $"comprueba que '{sectionName}' esté en la proyección que se solicitó.")
    {
        SectionName = sectionName;
    }
}

/// <summary>Datos del aviso de sección ausente.</summary>
public sealed class GraphSectionAbsentEventArgs : EventArgs
{
    public GraphSectionAbsentEventArgs(string sectionName) => SectionName = sectionName;

    /// <summary>Nombre de la sección ausente, tal como viaja en el JSON.</summary>
    public string SectionName { get; }
}

/// <summary>
/// Registro de las secciones que llegaron AUSENTES en un grafo.
///
/// <para>Existe porque la guarda de nulos del índice, sola, era un arreglo peor que el fallo que
/// cerraba. Colapsar una sección ausente a <c>[]</c> evita la <c>NullReferenceException</c>, sí,
/// pero a cambio vuelve indistinguibles «no me la mandaron» y «está vacía» justo en la estructura
/// que decide permisos: cierra, y cierra en silencio. Un satélite que pidiera
/// <c>?include=navigation</c> por error y luego preguntara por un permiso de dominio recibiría un
/// «no concedido» perfectamente plausible y ninguna pista de que la pregunta no se llegó a
/// hacer.</para>
///
/// <para>Tres salidas, de menos a más exigente, y ninguna sustituye a las otras:</para>
/// <list type="bullet">
///   <item>
///     <b>Siempre</b>: un evento ETW/EventPipe por <see cref="UmsAuthorizationEventSource"/>. No
///     hay que configurar nada y un <c>dotnet-trace</c> lo ve aunque el anfitrión no haya cableado
///     nada.
///   </item>
///   <item>
///     <b>Una línea</b>: asignar <see cref="Logger"/> lleva el aviso al registro normal del
///     anfitrión (Serilog incluido), que es donde alguien lo va a leer de verdad.
///   </item>
///   <item>
///     <b>Opt-in duro</b>: <see cref="ThrowOnAbsentSection"/> convierte la ausencia en
///     <see cref="GraphSectionMissingException"/>. Es para el satélite que NECESITA una sección y
///     prefiere caerse a autorizar a ciegas.
///   </item>
/// </list>
///
/// <para>Lo que NO se hace: fallar por defecto. La tolerancia es deliberada — el orden de
/// despliegue no se puede garantizar entre organizaciones, y un SDK que reviente ante un grafo
/// recortado convierte un `curl` con `?include=` contra un entorno compartido en una caída
/// ajena.</para>
/// </summary>
public static class UmsGraphDiagnostics
{
    /// <summary>
    /// Destino del aviso en el registro del anfitrión. Por defecto no registra nada —un SDK no
    /// decide dónde escribe el proceso que lo hospeda— pero cablearlo es una línea en el arranque:
    /// <c>UmsGraphDiagnostics.Logger = loggerFactory.CreateLogger("Ums.Graph");</c>
    /// </summary>
    public static ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>
    /// Cuando es <c>true</c>, una sección ausente lanza <see cref="GraphSectionMissingException"/>
    /// en vez de tolerarse. Apagado por defecto: ver el último párrafo de la clase.
    /// </summary>
    public static bool ThrowOnAbsentSection { get; set; }

    /// <summary>
    /// Se dispara una vez por sección ausente y por grafo — el índice se construye una sola vez
    /// por grafo, así que esto no puede inundar nada.
    /// </summary>
    public static event EventHandler<GraphSectionAbsentEventArgs>? SectionAbsent;

    internal static void ReportarAusencia(string sectionName)
    {
        UmsAuthorizationEventSource.Log.GraphSectionAbsent(sectionName);

        Logger.LogWarning(
            "El grafo de autorización llegó SIN la sección '{Section}'. Ausente no es vacía: se " +
            "trata como vacía para no tumbar el proceso, así que toda comprobación que dependa de " +
            "esa sección va a resolver NO CONCEDIDO. Si el grafo se pidió con `?include=`, revisa " +
            "que esa sección estuviera en la proyección solicitada.",
            sectionName);

        SectionAbsent?.Invoke(null, new GraphSectionAbsentEventArgs(sectionName));

        if (ThrowOnAbsentSection) throw new GraphSectionMissingException(sectionName);
    }
}

/// <summary>
/// Traza ETW/EventPipe del SDK de autorización. Es el registro que existe sin que el anfitrión
/// configure nada, que es justo lo que hacía falta: el defecto original era que la tolerancia
/// fuese MUDA, y un aviso que solo aparece si alguien lo cableó de antemano lo seguiría siendo.
/// </summary>
[EventSource(Name = "Evolith.Ums.Sdk.Authorization")]
internal sealed class UmsAuthorizationEventSource : EventSource
{
    internal static readonly UmsAuthorizationEventSource Log = new();

    private UmsAuthorizationEventSource() { }

    [Event(1, Level = EventLevel.Warning,
        Message = "Sección '{0}' ausente en el grafo de autorización; se trata como vacía.")]
    internal void GraphSectionAbsent(string sectionName)
    {
        if (IsEnabled()) WriteEvent(1, sectionName);
    }
}
