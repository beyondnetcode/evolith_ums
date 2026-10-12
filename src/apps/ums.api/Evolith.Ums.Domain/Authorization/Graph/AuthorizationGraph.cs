namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// The complete, immutable authorization graph for an authenticated user.
///
/// This is the core output of the UMS authentication engine. After a user
/// authenticates (Local BCrypt or external IDP), UMS resolves their tenant,
/// system suite, role, branch, and profile, then builds this graph containing
/// every permission, menu access node, domain resource authorization,
/// feature flag evaluation, and effective configuration value needed by the
/// client system to operate without re-querying UMS on subsequent requests.
///
/// The graph is intentionally self-contained and stateless — clients cache it
/// for the duration of the session (until ValidUntil) and use it to make
/// authorization decisions locally.
/// </summary>
public sealed record AuthorizationGraph
{
    /// <summary>
    /// Schema version of this payload. Governed by ADR-0074 and aligned with the canonical
    /// `auth-graph.schema.json` in <c>src/libs/sdk/contracts/</c>.
    ///
    /// <para>NO es una constante única: la elige <see cref="Build"/> según haya o no
    /// <see cref="Projection"/>. Una respuesta COMPLETA declara
    /// <c>SchemaVersion.Current</c> (2.4.0) y una PROYECTADA declara
    /// <c>SchemaVersion.Projected</c> (2.5.0). El motivo está escrito entero en
    /// <c>Evolith.Ums.Sdk.Contracts.SchemaVersion.Current</c>: subir la versión también en las
    /// completas rompía a cualquier satélite que valide contra un esquema 2.4.0 vendorizado, cuyo
    /// <c>const</c> compara por IGUALDAD — y lo rompía sin que hubiera pedido jamás una
    /// proyección.</para>
    ///
    /// <para>El valor por defecto del <c>init</c> es el de la respuesta completa, porque un grafo
    /// construido sin pasar por <see cref="Build"/> es, por definición, uno sin proyección.</para>
    /// </summary>
    public string SchemaVersion { get; init; } = global::Evolith.Ums.Sdk.Contracts.SchemaVersion.Current;

    /// <summary>Who is authenticated and in which context (tenant, suite, role, branch).</summary>
    public GraphContext Context { get; init; }

    /// <summary>
    /// G-043 — true cuando el usuario está autenticado y aprobado pero AÚN NO tiene un perfil activo
    /// (onboarding pendiente). En ese caso el grafo es un "lobby": <see cref="Context"/> trae User y
    /// Tenant reales pero SystemSuite/Role/Profile en null, y Actions/MenuAccess/DomainPermissions/Scopes
    /// vacíos. El cliente debe detectar esta bandera y mostrar el flujo de onboarding en lugar de la app,
    /// en vez de tratar el login como fallido. Por defecto false (usuarios con perfil).
    /// </summary>
    public bool OnboardingPending { get; init; }

    /// <summary>
    /// Discriminador cerrado del estado de acceso (ADR-0156 §5.1). Existe porque
    /// <see cref="OnboardingPending"/> no basta desde que la autenticación admite filtrar por
    /// sistema: «el usuario no tiene ningún perfil» y «tiene perfiles, pero ninguno en el sistema
    /// que se pidió» son estados distintos, y responder el segundo como el primero le mostraría un
    /// flujo de alta a alguien que ya está de alta.
    ///
    /// <see cref="OnboardingPending"/> se conserva y equivale a
    /// <see cref="GraphAccessState.OnboardingPending"/>: un consumidor de 2.0–2.3 sigue
    /// funcionando sin tocarlo. Los dos no pueden divergir porque <see cref="Build"/> los deriva
    /// de la misma entrada.
    /// </summary>
    public GraphAccessState AccessState { get; init; } = GraphAccessState.Granted;

    /// <summary>How the user authenticated and session timing.</summary>
    public GraphAuthentication Authentication { get; init; }

    /// <summary>All actions registered in the SystemSuite — the full action catalogue.</summary>
    public IReadOnlyList<GraphAction> Actions { get; init; }

    /// <summary>
    /// Perfiles activos del usuario en este inquilino, con el vigente marcado. Permite al cliente
    /// ofrecer el cambio de perfil sin una llamada adicional. Vacía en el grafo lobby (G-043).
    /// </summary>
    public IReadOnlyList<GraphProfileOption> Profiles { get; init; } = [];

    /// <summary>
    /// Module → Menu → SubMenu → Option tree with resolved permission per option.
    /// Only modules that have at least one reachable (Allow) option are included.
    /// Options with Deny or NotGranted are included so the client can render them
    /// as disabled/hidden according to its own UX policy.
    /// </summary>
    public IReadOnlyList<GraphMenuModule> MenuAccess { get; init; }

    /// <summary>
    /// Domain resources (Aggregates, Entities) with resolved authorization per action.
    /// Covers fine-grained backend access control beyond UI menus.
    /// </summary>
    public IReadOnlyList<GraphDomainPermission> DomainPermissions { get; init; }

    /// <summary>Feature flags evaluated against this user's context at auth time.</summary>
    public IReadOnlyList<GraphFeatureFlag> FeatureFlags { get; init; }

    /// <summary>Effective tenant configuration resolved with tenant-level override precedence.</summary>
    public GraphEffectiveConfig EffectiveConfig { get; init; }

    /// <summary>
    /// Ajustes del sistema marcados como visibles para el cliente, agrupados por espacio de
    /// nombres: branding, tema, disposición, idioma, parámetros funcionales…
    ///
    /// Es el bloque que permite al cliente inicializar la aplicación —logotipo, colores, página
    /// inicial, idioma— sin una llamada adicional, y es EXTENSIBLE: añadir un espacio de nombres
    /// nuevo no cambia la forma del contrato, solo aparece otra clave.
    ///
    /// Solo viaja lo marcado explícitamente (G-178). `AppSetting` es una bolsa clave/valor sin
    /// tipo donde junto al color de la marca puede haber una cadena de conexión.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Settings { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, string>>();

    /// <summary>
    /// OAuth2-style scopes derived from all Allow permissions.
    /// Format: "resourceCode.actionCode" (lowercase), e.g. "users.read", "inventory.write".
    /// Useful for downstream services implementing OAuth2 bearer token validation.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; }

    /// <summary>
    /// Proyección EFECTIVAMENTE aplicada, o <c>null</c> cuando el grafo es completo.
    ///
    /// Viaja DENTRO del grafo, no en una cabecera HTTP, y no es un capricho de estilo. Los
    /// satélites cachean el OBJETO grafo, no la respuesta: el Tablero guarda <c>{graph, expiraEn}</c>
    /// por sesión. Una marca en cabecera se perdería en el límite de esa caché y, en toda lectura
    /// cacheada, el consumidor volvería a no poder distinguir «esta sección no la pedí» de «esta
    /// sección llegó vacía» — que es la distinción de la que depende que un deny explícito siga
    /// existiendo. Además <c>POST /client/authenticate</c> entrega el grafo como CADENA serializada
    /// dentro del sobre: una cabecera describiría el sobre y dejaría la cadena mintiendo sobre sí
    /// misma.
    ///
    /// Se emite si y solo si hubo proyección. Ausente = grafo completo, que es la forma de
    /// siempre, byte por byte.
    /// </summary>
    public GraphProjection? Projection { get; init; }

    /// <summary>UTC timestamp when this graph was generated.</summary>
    public DateTime GeneratedAt { get; init; }

    /// <summary>
    /// UTC timestamp when this graph expires (= GeneratedAt + SessionTimeoutMinutes).
    /// Clients should re-authenticate after this point.
    /// </summary>
    public DateTime ValidUntil { get; init; }

    private AuthorizationGraph() { }

    /// <summary>Factory — builds the graph from its constituent parts.</summary>
    public static AuthorizationGraph Build(
        GraphContext                       context,
        GraphAuthentication                authentication,
        IReadOnlyList<GraphAction>         actions,
        IReadOnlyList<GraphMenuModule>     menuAccess,
        IReadOnlyList<GraphDomainPermission> domainPermissions,
        IReadOnlyList<GraphFeatureFlag>    featureFlags,
        GraphEffectiveConfig               effectiveConfig,
        IReadOnlyList<string>              scopes,
        DateTime                           generatedAt,
        bool                               onboardingPending = false,
        IReadOnlyList<GraphProfileOption>? profiles = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? settings = null,
        GraphAccessState                   accessState = GraphAccessState.Granted,
        // Último y opcional: los cinco llamantes que no proyectan —login, refresco, refresco de
        // sesión, preview y cambio de perfil— siguen compilando y emitiendo el grafo completo sin
        // tocar una línea. Es la única forma de que I1 sea cierta por construcción y no por
        // revisión.
        GraphProjection?                   projection = null)
    {
        var validUntil = generatedAt.AddMinutes(effectiveConfig.SessionTimeoutMinutes);

        // `onboardingPending` manda sobre `accessState`: son el mismo hecho contado dos veces —una
        // para los consumidores de 2.0–2.3 y otra para los de 2.4— y derivarlos aquí es lo único
        // que garantiza que ningún llamante pueda emitir un grafo donde se contradigan.
        var estado = onboardingPending ? GraphAccessState.OnboardingPending : accessState;

        return new AuthorizationGraph
        {
            // La versión la decide la FORMA de la respuesta, no la del código desplegado: 2.4.0
            // cuando viajan las quince claves de siempre, 2.5.0 solo cuando falta alguna y viaja el
            // eco `projection`. Quien no proyecta no puede observar el cambio, que es justo lo que
            // un `const` vendorizado en el otro extremo exige.
            SchemaVersion     = projection is null
                ? global::Evolith.Ums.Sdk.Contracts.SchemaVersion.Current
                : global::Evolith.Ums.Sdk.Contracts.SchemaVersion.Projected,
            Context           = context,
            OnboardingPending = onboardingPending,
            AccessState       = estado,
            Authentication    = authentication,
            Actions           = actions,
            MenuAccess        = menuAccess,
            DomainPermissions = domainPermissions,
            FeatureFlags      = featureFlags,
            EffectiveConfig   = effectiveConfig,
            Scopes            = scopes,
            Profiles          = profiles ?? [],
            Settings          = settings ?? new Dictionary<string, IReadOnlyDictionary<string, string>>(),
            Projection        = projection,
            GeneratedAt       = generatedAt,
            ValidUntil        = validUntil,
        };
    }
}
