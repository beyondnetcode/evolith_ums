namespace Evolith.Ums.Application.Authorization.Graph.Serializers;

using Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Forma canónica del grafo de autorización tal como viaja al cliente, en un
/// único lugar. La consumen los dos emisores del grafo: el login web
/// (<c>POST /api/v1/auth/login</c>) y la autenticación de sistemas satélite
/// (<c>POST /api/v1/client/authenticate</c>).
///
/// Existe porque hasta 2026-08-01 cada emisor tenía su propia forma y el
/// contrato publicado en <c>src/libs/sdk/contracts/auth-graph.schema.json</c>
/// no describía ninguna de las dos (G-167): el login serializaba el record de
/// dominio (con <c>sortOrder</c>, sin ids, <c>resourceCode</c>), el endpoint de
/// cliente proyectaba otro modelo (sin <c>sortOrder</c>, con ids opcionales,
/// <c>code</c>), y el SDK declaraba una tercera (ids obligatorios,
/// <c>name</c>/<c>label</c>, envoltorios <c>module</c>/<c>resource</c>). Un
/// satélite que integrara leyendo el contrato obtenía <c>undefined</c> en cada
/// nombre. La única forma de que esto no vuelva a divergir es que exista un
/// solo mapeador y que ambos endpoints pasen por él.
///
/// Convenciones de la forma canónica:
/// <list type="bullet">
///   <item>Todo nombre o etiqueta se expone como <c>value</c>.</item>
///   <item>Las jerarquías son listas planas: <c>menuAccess</c> es una lista de
///   módulos (sin envoltorio <c>module</c>) y <c>domainPermissions</c> una de
///   recursos (sin envoltorio <c>resource</c>).</item>
///   <item>Los identificadores técnicos (<c>id</c>, <c>moduleId</c>,
///   <c>systemSuiteId</c>…) son OPCIONALES: solo aparecen con
///   <see cref="GraphSerializationOptions.IncludeTechnicalMetadata"/>, que el
///   parámetro de inquilino <c>AUTH_GRAPH_INCLUDE_TECHNICAL_METADATA</c> deja
///   en <c>false</c> por defecto. Se OMITEN, no se emiten como <c>null</c>:
///   un cliente distingue «no viene» de «viene vacío».</item>
/// </list>
///
/// Se construye sobre <see cref="Dictionary{TKey,TValue}"/> y no sobre tipos
/// anónimos precisamente por la última regla: un tipo anónimo no puede omitir
/// una propiedad según una condición.
/// </summary>
public static class AuthGraphPayload
{
    /// <summary>
    /// Proyecta el grafo a su forma canónica de transporte.
    ///
    /// <para>Cuando <see cref="AuthorizationGraph.Projection"/> es <c>null</c> —el caso de
    /// siempre, y el de los cinco emisores que no proyectan— se emiten LAS QUINCE claves en el
    /// mismo orden de siempre. La ruta de código nueva no se pisa: no hay un <c>if</c> por clave,
    /// hay un <c>if</c> por SECCIÓN que ante <c>null</c> siempre dice que sí.</para>
    ///
    /// <para>Cuando hay proyección, un bloque no pedido se OMITE, jamás se emite vacío. Es la
    /// regla central y no es cosmética: <c>domainPermissions: []</c> y «no lo pedí» son
    /// indistinguibles para todo consumidor, y en particular para el evaluador de deny-wins, que
    /// ante los dos concluye que no hay ninguna denegación. Si el productor pudiera sustituir la
    /// ausencia por un array vacío, la atomicidad de <c>permissions</c> quedaría declarada en el
    /// contrato y sin ninguna defensa observable.</para>
    /// </summary>
    public static IReadOnlyDictionary<string, object?> Build(
        AuthorizationGraph graph,
        GraphSerializationOptions? options = null)
    {
        var opts = options ?? GraphSerializationOptions.Default;
        var meta = opts.IncludeTechnicalMetadata;
        var proy = graph.Projection;

        // Se construye por inserción y no con un inicializador de colección porque ahora hay
        // claves condicionales; el ORDEN de inserción es el mismo de siempre, de modo que un grafo
        // sin proyección sale idéntico al que salía antes de que este parámetro existiera.
        var d = new Dictionary<string, object?>
        {
            ["schemaVersion"]     = graph.SchemaVersion,
            ["onboardingPending"] = graph.OnboardingPending,
            // Enumeración cerrada (ADR-0156 §5.1). Convive con `onboardingPending`, que se
            // conserva para los consumidores de 2.0–2.3; el dominio garantiza que no divergen.
            ["accessState"]       = graph.AccessState.ToString(),
        };

        // El eco de lo aplicado, presente si y solo si hubo proyección. Sin él el cliente tendría
        // que INFERIR de la ausencia qué pidió, y una sección ausente por proyección sería
        // indistinguible de una sección que este UMS no sabe emitir. Con él, comprobar que se
        // recibió lo pedido es una comparación de listas, no una arqueología.
        if (proy is not null) d["projection"] = proy.Tokens;

        d["context"]        = Context(graph.Context, meta);
        d["authentication"] = Authentication(graph.Authentication, meta);

        // `actions` viaja con `navigation`: es lo que traduce cada `actionCode` del árbol a un
        // nombre legible. Un menú sin ese catálogo se pinta con códigos.
        if (proy.LlevaNavegacion())
        {
            d["actions"] = graph.Actions
                .Select(a => Map(("code", (object?)a.Code), ("value", a.Name)))
                .ToList();
        }

        if (proy.LlevaPerfiles())   d["profiles"]   = graph.Profiles.Select(Profile).ToList();
        if (proy.LlevaNavegacion()) d["menuAccess"] = graph.MenuAccess.Select(m => Module(m, meta)).ToList();

        // ATOMICIDAD DE `permissions` (la mitad de dominio). `domainPermissions` y `scopes` se
        // gobiernan con la MISMA condición, y esa es toda la defensa: el evaluador compartido
        // combina ambas con deny-wins y lee la de dominio con un `?? []`, así que un grafo con
        // `scopes` y sin `domainPermissions` no falla —CONCEDE— acciones que UMS denegó
        // explícitamente. Un modo de fallo que abre no es aceptable ni siquiera como caso raro,
        // porque nadie lo ve; uno que cierra al menos produce un 403 que alguien reporta.
        // Por eso no hay token para pedirlas por separado, y por eso aquí no hay dos `if`.
        if (proy.LlevaPermisos())
            d["domainPermissions"] = graph.DomainPermissions.Select(r => Resource(r, meta)).ToList();

        if (proy.LlevaBanderas())
            d["featureFlags"] = graph.FeatureFlags.Select(f => FeatureFlag(f, meta)).ToList();

        if (proy.LlevaConfig()) d["effectiveConfig"] = EffectiveConfig(graph.EffectiveConfig);

        // Diccionario abierto por diseño: es el único punto del contrato donde añadir una
        // clave nueva NO es un cambio de contrato (G-178).
        if (proy.LlevaAjustes()) d["settings"] = graph.Settings;

        // La otra mitad de `permissions`. Misma condición, a propósito: ver el comentario de
        // `domainPermissions`.
        if (proy.LlevaPermisos()) d["scopes"] = graph.Scopes;

        d["generatedAt"] = graph.GeneratedAt.ToString("O");
        d["validUntil"]  = graph.ValidUntil.ToString("O");

        return d;
    }

    private static Dictionary<string, object?> Map(params (string Key, object? Value)[] pairs)
    {
        var d = new Dictionary<string, object?>(pairs.Length);
        foreach (var (key, value) in pairs) d[key] = value;
        return d;
    }

    /// <summary>Antepone el id técnico solo cuando el inquilino lo habilitó.</summary>
    private static Dictionary<string, object?> WithId(bool meta, string name, Guid? id, Dictionary<string, object?> rest)
    {
        if (!meta || id is null) return rest;

        var d = new Dictionary<string, object?> { [name] = id.Value.ToString() };
        foreach (var (k, v) in rest) d[k] = v;
        return d;
    }

    private static object Context(GraphContext ctx, bool meta) => Map(
        ("user", WithId(meta, "id", ctx.User.Id, Map(
            ("email", (object?)ctx.User.Email),
            ("username", ctx.User.Username),
            ("value", ctx.User.DisplayName),
            ("status", ctx.User.Status)))),
        ("tenant", WithId(meta, "id", ctx.Tenant.Id, Map(
            ("code", (object?)ctx.Tenant.Code),
            ("value", ctx.Tenant.Name),
            ("status", ctx.Tenant.Status),
            ("isManagementOwner", ctx.Tenant.IsManagementOwner)))),
        // G-043: en el grafo lobby (onboarding pendiente) suite, rol y perfil son
        // null y viajan como null — el cliente reacciona a `onboardingPending`.
        ("systemSuite", ctx.SystemSuite is null ? null : WithId(meta, "id", ctx.SystemSuite.Id, Map(
            ("code", (object?)ctx.SystemSuite.Code),
            ("value", ctx.SystemSuite.Name),
            ("status", ctx.SystemSuite.Status)))),
        ("role", ctx.Role is null ? null : WithId(meta, "id", ctx.Role.Id, Map(
            ("code", (object?)ctx.Role.Code),
            ("value", ctx.Role.Name),
            ("hierarchyLevel", ctx.Role.HierarchyLevel)))),
        ("profile", ctx.Profile is null ? null : WithId(meta, "id", ctx.Profile.Id, Map(
            ("scope", (object?)ctx.Profile.Scope),
            ("isActive", ctx.Profile.IsActive)))),
        ("branch", ctx.Branch is null ? null : WithId(meta, "id", ctx.Branch.Id, Map(
            ("code", (object?)ctx.Branch.Code),
            ("value", ctx.Branch.Name)))),
        // Eco literal del sistema pedido, ya normalizado, o null si no se pidió ninguno. Se emite
        // SIEMPRE la clave: un cliente no debería tener que distinguir «no se pidió» de «la clave
        // no existe en esta versión».
        ("requestedSystem", ctx.RequestedSystem is null ? null : Map(
            ("code", (object?)ctx.RequestedSystem.Code))));

    private static object Authentication(GraphAuthentication auth, bool meta) => Map(
        ("method", (object?)auth.Method),
        ("provider", auth.Provider is null ? null : WithId(meta, "id", auth.Provider.Id, Map(
            ("code", (object?)auth.Provider.Code),
            ("name", auth.Provider.Name),
            ("value", auth.Provider.Strategy)))),
        ("mfaRequired", auth.MfaRequired),
        ("issuedAt", auth.IssuedAt.ToString("O")),
        ("sessionExpiresAt", auth.SessionExpiresAt.ToString("O")));

    /// <summary>
    /// Perfil disponible para el selector del cliente. `system` y `role` se anidan aunque en el
    /// dominio viajen planos: es la forma en que el cliente los consume, y aplanarlos aquí
    /// obligaría a cada consumidor a recomponerlos.
    ///
    /// El `id` se emite SIEMPRE, fuera de <see cref="WithId"/> (ADR-0156 §5.3). Aquí no es un
    /// metadato técnico decorativo como en módulos, nodos, recursos y banderas —donde el `code` es
    /// la clave de negocio y la regla se mantiene intacta—: es la clave de una operación que el
    /// propio grafo invita a ejecutar, `POST /client/switch-profile`. Un contrato que ofrece una
    /// operación y retiene su clave no es un contrato.
    ///
    /// No se usa un selector semántico `{ systemCode, roleCode, branchCode }`: el índice
    /// (TenantId, UserId, RoleId, BranchId) no es único, así que dos perfiles pueden coincidir en
    /// los tres campos y el selector obligaría a un conflicto que el cliente no puede resolver.
    /// </summary>
    private static object Profile(GraphProfileOption p) => Map(
        ("id", (object?)p.Id.ToString()),
        ("system", Map(("code", (object?)p.SystemCode), ("value", p.SystemName))),
        ("role", Map(
            ("code", (object?)p.RoleCode),
            ("value", p.RoleName),
            ("hierarchyLevel", p.HierarchyLevel))),
        ("branch", p.BranchCode is null ? null : Map(
            ("code", (object?)p.BranchCode),
            ("value", p.BranchName))),
        ("scope", p.Scope),
        ("isCurrent", p.IsCurrent));

    private static object Module(GraphMenuModule m, bool meta) => WithId(meta, "id", m.Id, Map(
        ("code", (object?)m.Code),
        ("value", m.Name),
        // `sortOrder` es dato de presentación, no metadato técnico: sin él un satélite no puede
        // pintar el menú en el orden que el tenant configuró.
        ("sortOrder", m.SortOrder),
        ("status", m.Status),
        // Mismo criterio que en el nodo: se emite siempre, también como null. Sin él el cliente
        // sabe cómo se llama el módulo pero no con qué pintarlo, y acaba resolviendo el icono por
        // código —que es exactamente la tabla estática que el grafo vino a eliminar (G-181).
        ("icon", m.Icon),
        ("nodes", m.Nodes.Select(n => Node(n, meta)).ToList())));

    /// <summary>
    /// Nodo de navegación, recursivo. `children` se emite siempre, aunque venga vacío: un cliente
    /// que recorra el árbol no debería tener que distinguir entre «sin hijos» y «clave ausente».
    /// </summary>
    private static object Node(GraphNavigationNode n, bool meta) => WithId(meta, "id", n.Id, Map(
        ("code", (object?)n.Code),
        ("value", n.Name),
        ("kind", n.Kind),
        ("sortOrder", n.SortOrder),
        // Se emiten siempre, también como null: un cliente que recorra el árbol no debería tener
        // que distinguir «sin icono» de «clave ausente».
        ("icon", n.Icon),
        ("route", n.Route),
        ("actions", n.Actions.Select(a => Map(
            ("actionCode", (object?)a.ActionCode),
            ("effect", a.Effect.ToString()),
            ("source", a.Source.ToString()))).ToList()),
        ("children", n.Children.Select(h => Node(h, meta)).ToList())));

    private static object Resource(GraphDomainPermission res, bool meta)
    {
        var body = Map(
            ("resourceType", (object?)res.ResourceType),
            // `resourceCode` y no `code`: el mismo dato se llamaba de dos formas
            // según el endpoint que lo emitiera (G-167).
            ("resourceCode", res.ResourceCode),
            ("value", res.ResourceName),
            ("actions", res.Actions.Select(a => Map(
                ("actionCode", (object?)a.ActionCode),
                ("value", a.ActionName),
                ("effect", a.Effect.ToString()),
                ("source", a.Source.ToString()))).ToList()));

        if (!meta) return body;

        var d = new Dictionary<string, object?> { ["resourceId"] = res.ResourceId.ToString() };
        foreach (var (k, v) in body) d[k] = v;
        d["moduleId"] = res.ModuleId?.ToString();
        d["parentResourceId"] = res.ParentResourceId?.ToString();
        return d;
    }

    private static object FeatureFlag(GraphFeatureFlag flag, bool meta)
    {
        var body = Map(
            ("flagCode", (object?)flag.FlagCode),
            ("isEnabled", flag.IsEnabled),
            ("matchedCriteriaType", flag.MatchedCriteriaType));

        return meta
            ? WithId(true, "systemSuiteId", flag.SystemSuiteId, body)
            : body;
    }

    private static object EffectiveConfig(GraphEffectiveConfig cfg) => Map(
        ("sessionTimeoutMinutes", (object?)cfg.SessionTimeoutMinutes),
        ("maxLoginAttempts", cfg.MaxLoginAttempts),
        ("minPasswordLength", cfg.MinPasswordLength),
        ("mfaRequiredForAdmin", cfg.MfaRequiredForAdmin),
        // Faltaba en la proyección del endpoint de cliente: el satélite no podía
        // saber qué métodos MFA acepta el inquilino.
        ("mfaAllowedMethods", cfg.MfaAllowedMethods),
        ("accessTokenDurationMs", cfg.AccessTokenDurationMs),
        ("authUseExternalIdp", cfg.AuthUseExternalIdp));
}
