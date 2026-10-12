namespace Evolith.Ums.Sdk.Authorization;

using System.Runtime.CompilerServices;
using Evolith.Ums.Sdk.Contracts;

/// <summary>
/// Índice de consulta O(1) sobre un <see cref="AuthorizationGraph"/>.
///
/// El grafo es inmutable durante toda la sesión, pero el validador lo recorría entero en CADA
/// comprobación de permiso: cuatro bucles anidados para resolver una opción de menú, dos para un
/// recurso de dominio. Con un catálogo de 500 opciones eso es medio millar de comparaciones de
/// cadena por llamada, y una petición HTTP normal hace varias comprobaciones. Es coste por
/// petición, no por sesión: exactamente donde más duele (G-174, R-7 del informe de arquitectura).
///
/// El índice se construye una vez por grafo y se memoiza en una
/// <see cref="ConditionalWeakTable{TKey,TValue}"/>: no hay que cambiar la API pública ni pedirle
/// al integrador que recuerde invalidar nada, y cuando el grafo se recolecta, su índice se va con
/// él. La tabla es segura para uso concurrente.
/// </summary>
internal sealed class GraphIndex
{
    private static readonly ConditionalWeakTable<AuthorizationGraph, GraphIndex> Cache = new();

    /// <summary>
    /// Primera acción encontrada por código de nodo, en el orden de recorrido del árbol.
    ///
    /// Se conserva a propósito la semántica de «primera coincidencia» del validador original:
    /// una opción con varias acciones (VIEW=Allow, DELETE=Deny) resuelve por la primera que
    /// aparece. Es discutible —deny-wins sería más prudente— pero cambiarlo aquí sería alterar
    /// una decisión de seguridad dentro de un cambio de rendimiento. Para preguntar por una
    /// acción concreta existe <see cref="OpcionAccion"/>.
    /// </summary>
    private readonly Dictionary<string, NodeAction> _opcionPorCodigo = new(StringComparer.Ordinal);

    private readonly Dictionary<(string Opcion, string Accion), NodeAction> _opcionAccion = new();
    private readonly Dictionary<(string Recurso, string Accion), DomainActionResolution> _dominio = new();
    private readonly HashSet<string> _scopes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FeatureFlagState> _flags = new(StringComparer.Ordinal);

    /// <summary>
    /// Secciones que llegaron AUSENTES en este grafo, por nombre (ver <see cref="GraphSections"/>).
    ///
    /// <para>Existe para que ausente y vacía dejen de ser el mismo estado observable. Sin esta
    /// lista, un consumidor que preguntara por un permiso de dominio sobre un grafo que llegó sin
    /// `domainPermissions` recibiría un «no concedido» idéntico al de un grafo que sí traía la
    /// sección y de verdad no concedía — y no habría forma de distinguirlos ni después.</para>
    /// </summary>
    public IReadOnlyList<string> SeccionesAusentes => _ausentes;

    private readonly List<string> _ausentes = [];

    /// <summary>
    /// Cae a lista vacía ante una sección AUSENTE, y lo REGISTRA con el nombre de la sección.
    ///
    /// <para>Desde 2.5.0 el grafo puede llegar PROYECTADO: los bloques que el llamante no pidió se
    /// omiten del JSON. <c>AuthorizationGraph</c> es un record posicional con colecciones NO
    /// anulables, pero System.Text.Json no se queja de una clave ausente: deja <c>null</c> y sigue.
    /// El resultado era una <c>NullReferenceException</c> en la PRIMERA comprobación de permiso
    /// —no al deserializar—, que bajo el interceptor AOP o el middleware acaba en 500 o se la traga
    /// el manejador del anfitrión.</para>
    ///
    /// <para>Pero caer a <c>[]</c> y callar era cambiar un fallo ruidoso por uno mudo, y encima en
    /// el índice que decide permisos: <c>null</c> y <c>[]</c> pasaban a ser el mismo estado, y «no
    /// me la mandaron» dejaba de poder distinguirse de «está vacía». La tolerancia se conserva
    /// —fail-closed, sin tumbar al satélite— pero deja rastro: el nombre de la sección va a
    /// <see cref="SeccionesAusentes"/> y a <see cref="UmsGraphDiagnostics"/>, que lo lleva a la
    /// traza ETW siempre y al registro del anfitrión si lo cabló. Quien prefiera caerse antes que
    /// autorizar a ciegas enciende
    /// <see cref="UmsGraphDiagnostics.ThrowOnAbsentSection"/>.</para>
    ///
    /// <para>Una lista VACÍA no se reporta: vacía es un dato legítimo y frecuente —un grafo lobby
    /// las trae todas así—. Lo que se reporta es la AUSENCIA, que es lo que no se podía ver.</para>
    /// </summary>
    private IReadOnlyList<T> OVacia<T>(IReadOnlyList<T>? lista, string seccion)
    {
        if (lista is not null) return lista;

        _ausentes.Add(seccion);
        UmsGraphDiagnostics.ReportarAusencia(seccion);
        return [];
    }

    private GraphIndex(AuthorizationGraph graph)
    {
        // El árbol es recursivo desde v2.0.0: se aplana una vez y se indexa por (código, acción).
        var modulos = OVacia(graph.MenuAccess, GraphSections.MenuAccess);

        foreach (var nodo in Aplanar(modulos.SelectMany(m => OVacia(m.Nodes, GraphSections.MenuAccessNodes))))
        {
            foreach (var accion in OVacia(nodo.Actions, GraphSections.MenuNodeActions))
            {
                _opcionPorCodigo.TryAdd(nodo.Code, accion);
                _opcionAccion.TryAdd((nodo.Code, accion.ActionCode), accion);
            }
        }

        foreach (var recurso in OVacia(graph.DomainPermissions, GraphSections.DomainPermissions))
        {
            foreach (var accion in OVacia(recurso.Actions, GraphSections.DomainResourceActions))
            {
                _dominio.TryAdd((recurso.ResourceCode, accion.ActionCode), accion);
            }
        }

        foreach (var scope in OVacia(graph.Scopes, GraphSections.Scopes)) _scopes.Add(scope);
        foreach (var flag in OVacia(graph.FeatureFlags, GraphSections.FeatureFlags))
            _flags.TryAdd(flag.FlagCode, flag);
    }

    public static GraphIndex For(AuthorizationGraph graph)
        => Cache.GetValue(graph, g => new GraphIndex(g));

    private IEnumerable<NavigationNode> Aplanar(IEnumerable<NavigationNode> nodos)
    {
        foreach (var nodo in nodos)
        {
            yield return nodo;
            foreach (var hijo in Aplanar(OVacia(nodo.Children, GraphSections.MenuNodeChildren)))
                yield return hijo;
        }
    }

    public NodeAction? Opcion(string codigo)
        => _opcionPorCodigo.TryGetValue(codigo, out var opcion) ? opcion : null;

    public NodeAction? OpcionAccion(string codigo, string accion)
        => _opcionAccion.TryGetValue((codigo, accion), out var opcion) ? opcion : null;

    public DomainActionResolution? Dominio(string recurso, string accion)
        => _dominio.TryGetValue((recurso, accion), out var resolucion) ? resolucion : null;

    public FeatureFlagState? Flag(string codigo)
        => _flags.TryGetValue(codigo, out var flag) ? flag : null;

    /// <summary>Los scopes viajan en minúsculas; se comparan sin distinguir mayúsculas (G-167).</summary>
    public bool TieneScope(string scope) => _scopes.Contains(scope);

    /// <summary>
    /// Denegación explícita del par (código, acción), mirando las dos superficies: una opción de
    /// menú cuyo código coincida y un recurso de dominio. Refleja la precedencia de deny del
    /// validador original.
    /// </summary>
    public bool EstaDenegado(string codigo, string accion)
        => OpcionAccion(codigo, accion)?.Effect == AccessEffect.Deny
           || Dominio(codigo, accion)?.Effect == AccessEffect.Deny;
}
