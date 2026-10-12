namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Proyección del grafo: QUÉ bloques opcionales pidió el llamante en <c>?include=</c>.
///
/// <para>Existe porque el grafo real está dominado por un solo bloque —<c>menuAccess</c> ocupa el
/// ~90 % del payload capturado— y no todo satélite necesita todos los bloques. Pero recortar un
/// grafo de autorización es una operación peligrosa por naturaleza: el consumidor decide
/// permisos con lo que recibe, y una sección que falta se parece muchísimo a una sección vacía.
/// De ahí las tres reglas que este tipo hace cumplir por construcción y no por disciplina.</para>
///
/// <list type="number">
///   <item>
///     <b>Sin parámetro, todo.</b> <see cref="TryParse"/> devuelve <c>null</c> cuando no hay
///     <c>include</c>, y <c>null</c> significa «grafo completo». Ningún satélite existente puede
///     enterarse de que esta clase existe: no hay ruta de código nueva para quien no la usa.
///   </item>
///   <item>
///     <b><see cref="Permisos"/> es atómico.</b> El vocabulario NO tiene un token para
///     <c>scopes</c> ni otro para <c>domainPermissions</c>: solo existe <c>permissions</c>, que
///     trae los dos. La razón no es de estilo. El evaluador compartido combina ambas secciones
///     con deny-wins y lee la de dominio con un <c>?? []</c> defensivo: si llegara <c>scopes</c>
///     sin <c>domainPermissions</c>, las denegaciones EXPLÍCITAS que UMS sí emitió dejarían de
///     existir y la comprobación pasaría a CONCEDER en silencio. Un fallo que cierra es tolerable
///     —el usuario ve un 403 y llama—; un fallo que abre no se ve nunca. Al no haber token para
///     pedir una mitad, ese estado es inalcanzable desde la petición.
///   </item>
///   <item>
///     <b>Vocabulario CERRADO.</b> Un valor desconocido no se ignora: se rechaza con 400
///     (ver <see cref="TryParse"/>). Tragarse un token mal escrito dejaría al satélite creyendo
///     que pidió algo que nunca recibió — la degradación silenciosa que ya sufre el campo hermano
///     <c>RequestedScopes</c> de <c>/client/authenticate</c>, aceptado e ignorado desde siempre.
///     Cerrado también por caché: es lo que acota la clave de una entrada proyectada (R-16 de la
///     evaluación de arquitectura de contexto de sesión).
///   </item>
/// </list>
///
/// <para>Los bloques del SOBRE —<c>schemaVersion</c>, <c>onboardingPending</c>,
/// <c>accessState</c>, <c>context</c>, <c>authentication</c>, <c>generatedAt</c> y
/// <c>validUntil</c>— no son negociables y por eso no tienen token: sin ellos el grafo no se
/// puede fechar, ni validar, ni atribuir a nadie. <c>context</c> además es un bloqueo duro aguas
/// abajo (el Tablero rechaza con 502 un grafo sin <c>context.user</c> o <c>context.tenant</c>),
/// así que ofrecer recortarlo sería ofrecer romper la sesión.</para>
/// </summary>
public sealed class GraphProjection
{
    /// <summary><c>scopes</c> + <c>domainPermissions</c>. ATÓMICO: ver la regla 2 de la clase.</summary>
    public const string Permisos = "permissions";

    /// <summary>
    /// <c>menuAccess</c> + <c>actions</c>. Van juntos porque <c>actions</c> es lo que da nombre
    /// legible a los <c>actionCode</c> del árbol: separarlos deja al cliente con un menú de
    /// códigos. Y porque salen del mismo agregado, así que separarlos tampoco ahorra nada.
    /// </summary>
    public const string Navegacion = "navigation";

    /// <summary><c>profiles</c> — el selector de perfil del cliente.</summary>
    public const string Perfiles = "profiles";

    /// <summary><c>featureFlags</c>.</summary>
    public const string Banderas = "features";

    /// <summary><c>effectiveConfig</c>.</summary>
    public const string Config = "config";

    /// <summary><c>settings</c> — ajustes visibles del sistema.</summary>
    public const string Ajustes = "settings";

    /// <summary>
    /// El vocabulario, en orden canónico. Es el orden en el que viaja el eco <c>projection</c>
    /// del grafo: dos peticiones equivalentes escritas al revés deben producir el mismo eco, o el
    /// eco no serviría como clave de caché.
    /// </summary>
    public static readonly IReadOnlyList<string> Vocabulario =
        [Permisos, Navegacion, Perfiles, Banderas, Config, Ajustes];

    /// <summary>
    /// Nombres de SECCIÓN que un integrador escribiría por intuición, con el token que de verdad
    /// los trae. No amplían el vocabulario —siguen dando 400— pero permiten que el mensaje de
    /// error diga «querías esto» en vez de limitarse a listar seis palabras.
    ///
    /// <c>scopes</c> y <c>domainPermissions</c> están aquí a propósito: es el error que más
    /// probablemente cometería alguien buscando pedir solo permisos, y es justo el que la
    /// atomicidad prohíbe.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> SeccionesPorToken =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scopes"]            = Permisos,
            ["domainpermissions"] = Permisos,
            ["menuaccess"]        = Navegacion,
            ["actions"]           = Navegacion,
            ["featureflags"]      = Banderas,
            ["effectiveconfig"]   = Config,
        };

    /// <summary>
    /// Cota del valor desconocido que se devuelve y se registra.
    ///
    /// <para>Sesenta y cuatro caracteres es holgado para cualquier token del vocabulario —el más
    /// largo mide once— y de sobra para que quien escribió la errata reconozca su propia palabra,
    /// que es lo único para lo que sirve devolverla.</para>
    ///
    /// <para>La cota no es higiene: sin ella, el valor CRUDO del llamante viaja íntegro al cuerpo
    /// del 400 y de ahí al registro de Serilog. Medido, un `include` de 8000 caracteres volvía
    /// entero por las dos vías. Eso es un amplificador gratis —el atacante elige cuántos bytes
    /// escribe en el disco de UMS por petición— y además un vertedero: cualquier cosa que quepa en
    /// una cadena de consulta acaba en un fichero de registro que alguien archiva.</para>
    /// </summary>
    private const int MaximoDelValorEcoado = 64;

    /// <summary>
    /// Recorta al límite y lo DICE. El sufijo importa: un valor cortado en seco parece el valor que
    /// el llamante escribió, y le haría buscar una errata en un sitio donde no está.
    /// </summary>
    private static string Acotar(string valor)
        => valor.Length <= MaximoDelValorEcoado
            ? valor
            : valor[..MaximoDelValorEcoado] + "… (recortado)";

    private readonly HashSet<string> _tokens;

    private GraphProjection(HashSet<string> tokens)
    {
        _tokens = tokens;
        Tokens  = Vocabulario.Where(tokens.Contains).ToList();
    }

    /// <summary>Los tokens pedidos, deduplicados y en el orden canónico del vocabulario.</summary>
    public IReadOnlyList<string> Tokens { get; }

    /// <summary>
    /// La proyección como UNA cadena estable, para usarla de clave.
    ///
    /// <para>Se apoya en que <see cref="Tokens"/> ya viene deduplicado y en el orden del
    /// vocabulario, no en el que trajo el <c>?include=</c>. Por eso
    /// <c>permissions,navigation</c> y <c>navigation,permissions</c> —que producen el MISMO
    /// grafo— dan la misma cadena, y lo retenido para uno sirve al otro. Sin esa normalización
    /// no habría error, solo un fallo de acierto silencioso y permanente.</para>
    /// </summary>
    public string Canonico => string.Join(',', Tokens);

    public bool IncluyePermisos   => _tokens.Contains(Permisos);
    public bool IncluyeNavegacion => _tokens.Contains(Navegacion);
    public bool IncluyePerfiles   => _tokens.Contains(Perfiles);
    public bool IncluyeBanderas   => _tokens.Contains(Banderas);
    public bool IncluyeConfig     => _tokens.Contains(Config);
    public bool IncluyeAjustes    => _tokens.Contains(Ajustes);

    /// <summary>
    /// Interpreta el valor de <c>?include=</c>.
    ///
    /// <para>Tolerante en la forma: insensible a mayúsculas, admite espacios alrededor de cada
    /// token y segmentos vacíos (<c>"permissions , NAVIGATION"</c> y <c>"navigation,,permissions"</c>
    /// son la misma proyección). Intransigente en el fondo: cualquier palabra fuera del
    /// vocabulario aborta con <paramref name="tokenDesconocido"/> poblado, y el llamante debe
    /// responder 400.</para>
    ///
    /// <para>Un <c>include</c> ausente, vacío o compuesto solo de separadores se trata como
    /// AUSENTE —<paramref name="proyeccion"/> a <c>null</c>, grafo completo— y no como «solo el
    /// sobre». Es deliberado: el sobre a secas no le sirve a ningún consumidor, y entre
    /// interpretar una cadena vacía como «todo» o como «nada», solo la primera es segura. Dar de
    /// más nunca rompe a un cliente; dar de menos sí.</para>
    /// </summary>
    /// <param name="include">Valor crudo del parámetro de consulta, tal cual llegó.</param>
    /// <param name="proyeccion">
    /// La proyección pedida, o <c>null</c> si no se pidió ninguna (= grafo completo).
    /// </param>
    /// <param name="tokenDesconocido">
    /// El PRIMER valor no reconocido, tal como lo escribió el llamante —sin normalizar, para que
    /// vea su propia errata— pero ACOTADO a <see cref="MaximoDelValorEcoado"/> caracteres, o
    /// <c>null</c> si todo se reconoció. Se acota aquí, en el origen, y no solo al componer el
    /// mensaje: así ninguna ruta aguas abajo —cuerpo del 400, registro, métricas— puede reflejar
    /// el valor crudo por descuido.
    /// </param>
    /// <returns><c>false</c> solo cuando hay un valor desconocido.</returns>
    public static bool TryParse(string? include, out GraphProjection? proyeccion, out string? tokenDesconocido)
    {
        proyeccion       = null;
        tokenDesconocido = null;

        if (string.IsNullOrWhiteSpace(include)) return true;

        var tokens = new HashSet<string>(StringComparer.Ordinal);

        foreach (var crudo in include.Split(','))
        {
            var limpio = crudo.Trim();
            if (limpio.Length == 0) continue;

            var normalizado = limpio.ToLowerInvariant();

            if (!Vocabulario.Contains(normalizado))
            {
                tokenDesconocido = Acotar(limpio);
                return false;
            }

            tokens.Add(normalizado);
        }

        // Solo separadores: indistinguible de no haber pedido nada. Ver el comentario del método.
        if (tokens.Count == 0) return true;

        proyeccion = new GraphProjection(tokens);
        return true;
    }

    /// <summary>
    /// Mensaje de 400 para un valor desconocido. Dice QUÉ no se reconoce y CUÁLES valen, y cuando
    /// el llamante escribió el nombre de una sección en vez del de su token, dice cuál la trae.
    ///
    /// <para>El texto vive aquí y no en el endpoint porque es la mitad útil de la regla 3: un 400
    /// que solo dijera «valor inválido» obligaría a leer el código fuente de UMS para
    /// arreglarlo.</para>
    ///
    /// <para>El valor recibido se refleja ACOTADO a <see cref="MaximoDelValorEcoado"/> caracteres.
    /// Sin cota, el llamante elige cuántos bytes escribe UMS en su propio registro y en el cuerpo
    /// de la respuesta: medido, 8000 caracteres volvían íntegros por las dos vías.</para>
    /// </summary>
    public static string MensajeDeValorDesconocido(string tokenDesconocido)
    {
        // Se vuelve a acotar aunque `TryParse` ya lo haga. El método es público y su argumento es
        // una cadena: nada impide que mañana lo llame otra ruta con el valor crudo del llamante, y
        // entonces la cota tendría que existir aquí. Acotar dos veces no cuesta nada; que el límite
        // dependa de quién llama, sí.
        tokenDesconocido = Acotar(tokenDesconocido);

        var validos = string.Join(", ", Vocabulario);
        var pista   = string.Empty;

        if (SeccionesPorToken.TryGetValue(tokenDesconocido.ToLowerInvariant(), out var token))
        {
            // Para las dos mitades del par atómico el mensaje dice además POR QUÉ no se pueden
            // pedir sueltas: sin esa frase, «usa permissions» parece una rareza de nomenclatura y
            // no la regla de seguridad que es.
            var motivo = token == Permisos
                ? ", que trae siempre 'scopes' y 'domainPermissions' juntos: separarlos haría que una denegación explícita desapareciera en silencio."
                : ".";

            pista = $" La sección '{tokenDesconocido}' viaja dentro de '{token}'{motivo}";
        }

        return $"El valor '{tokenDesconocido}' del parámetro 'include' no se reconoce. " +
               $"Valores válidos: {validos}.{pista}";
    }
}

/// <summary>
/// Lectura de una proyección OPCIONAL. <c>null</c> significa «grafo completo», así que todas
/// estas devuelven <c>true</c> ante <c>null</c>.
///
/// Existen para que el código de construcción y de serialización no repita
/// <c>proyeccion?.IncluyeX ?? true</c> en veinte sitios: es exactamente el punto donde un
/// <c>?? false</c> despistado convertiría un recorte en una denegación silenciosa.
/// </summary>
public static class GraphProjectionExtensions
{
    public static bool LlevaPermisos(this GraphProjection? p)   => p is null || p.IncluyePermisos;
    public static bool LlevaNavegacion(this GraphProjection? p) => p is null || p.IncluyeNavegacion;
    public static bool LlevaPerfiles(this GraphProjection? p)   => p is null || p.IncluyePerfiles;
    public static bool LlevaBanderas(this GraphProjection? p)   => p is null || p.IncluyeBanderas;
    public static bool LlevaConfig(this GraphProjection? p)     => p is null || p.IncluyeConfig;
    public static bool LlevaAjustes(this GraphProjection? p)    => p is null || p.IncluyeAjustes;
}
