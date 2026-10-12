namespace Evolith.Ums.Presentation.Endpoints.Identity.Auth;

using System.Security.Claims;
using BeyondNetCode.Shell.Factory.Interfaces;
using Evolith.Ums.Application.Authorization.Graph;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Application.Identity.Auth.Commands;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Presentation.Services;
using UserAccountAgregado = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

/// <summary>
/// Public external API for client system authentication.
///
/// POST /api/v1/client/authenticate
///   - No session cookie (stateless for external systems)
///   - Returns the full AuthorizationGraph serialized in the tenant's configured format
///   - JWT embeds graph as claims (permissions, scopes, features)
///   - El formato se puede forzar con el CAMPO `format` DEL CUERPO o con la cabecera `Accept`.
///
/// GET /api/v1/client/graph
///   - Reconstruye el grafo vigente del portador.
///   - `?include=` (v2.5.0) acota los bloques opcionales. Omitirlo devuelve el grafo completo.
///
/// This endpoint is the primary integration point for downstream systems
/// that need to authenticate users and receive their full authorization context.
///
/// DOS ADVERTENCIAS SOBRE PARÁMETROS, escritas aquí porque este comentario mentía:
///
///  1. `?format=xml` NUNCA ha existido. `ClientAuthRequest.Format` es un campo del CUERPO, y en
///     minimal APIs un tipo complejo se enlaza del cuerpo: la cadena de consulta no se lee jamás.
///     Se corrige el texto y no el comportamiento — cablear `?format=` sería un cambio de contrato
///     ajeno a este, y hacerlo de paso lo escondería dentro de otro.
///
///  2. `ClientAuthRequest.RequestedScopes` se acepta y se IGNORA en silencio desde el primer día
///     («future use»). Es la degradación muda que `?include=` evita a propósito respondiendo 400.
///     Convivirán hasta que se decida retirarlo o cablearlo: queda anotado para que la incoherencia
///     sea visible y no se lea como que el silencio es la convención de esta superficie.
/// </summary>
public static class ClientAuthEndpoints
{
    public static void MapClientAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/client")
            .WithTags("Client Authentication");

        group.MapPost("/authenticate", HandleClientAuthenticateAsync)
            .WithName("ClientAuthenticate")
            .WithSummary("Authenticate a user and receive the full authorization graph")
            .AllowAnonymous()
            .Produces<ClientAuthResponse>(StatusCodes.Status200OK)
            .Produces<ClientAuthErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ClientAuthErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ClientAuthErrorResponse>(StatusCodes.Status404NotFound);

        // El grafo, aparte de la autenticación y con el portador que ya tiene el satélite.
        //
        // Existe porque el grafo NO viaja dentro del JWT (D-031): meterlo ahí serían decenas de KB
        // en cada cabecera y, sobre todo, un grafo irrevocable —un permiso retirado seguiría
        // valiendo hasta que expirara el token, porque el token sería la fuente. Aquí la fuente
        // sigue siendo UMS: el satélite revalida cuando su copia caduca, sin volver a pedir
        // credenciales al usuario.
        //
        // Sirve además para responder «¿esta sesión sigue viva?»: un 401 aquí es la respuesta.
        // G-191: la política `Satelite` fija el esquema PORTADOR. Con `RequireAuthorization()` a
        // secas se autenticaba por el esquema por defecto —la cookie—, y un satélite con un
        // portador válido recibía 302 al formulario de acceso en vez de 200 o 401.
        //
        // `?include=` (v2.5.0) acota los bloques OPCIONALES del grafo. Es el primer parámetro de
        // consulta real de este endpoint: `?format=` nunca se enlazó aquí, pese a lo que promete el
        // comentario de cabecera de esta clase. Omitirlo devuelve el grafo completo, byte por byte
        // como siempre. El 400 es nuevo y por eso se declara: un valor fuera del vocabulario se
        // rechaza en vez de ignorarse.
        group.MapGet("/graph", HandleClientGraphAsync)
            .WithName("ClientGraph")
            .WithSummary("Devuelve el grafo de autorización vigente del portador")
            .RequireAuthorization(UmsAuthPolicies.Satelite)
            .Produces<IReadOnlyDictionary<string, object?>>(StatusCodes.Status200OK)
            .Produces<ClientAuthErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ClientAuthErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ClientAuthErrorResponse>(StatusCodes.Status404NotFound);

        // Cambio de perfil por el carril de satélite (ADR-0156 §8, G-206).
        //
        // El grafo publica `profiles[]` con su `id` desde 2.4.0, es decir, invita al cliente a
        // cambiarse de perfil. Hasta ahora la única operación que lo hacía era
        // `POST /api/v1/auth/switch-profile`, que el portador semántico NO puede usar: valida el
        // token a mano exigiendo un `sub` GUID y un claim `tenant_id` que ese token no lleva.
        //
        // NO se extiende aquel endpoint, y no es por comodidad. Valida el token a mano con
        // `ValidateIssuer=false` y `ValidateAudience=false` (G-201): encaminar el carril de
        // satélite por ahí lo haría entrar por la puerta más floja de la API justo cuando lo que
        // se quiere es acotarlo. Además devuelve la forma del portal y reescribe una cookie de
        // sesión que un satélite no tiene ni debe recibir.
        //
        // Lo único nuevo es este adaptador HTTP: `SwitchProfileCommand` y `BuildForProfileAsync`
        // se reutilizan sin tocarlos, así que las comprobaciones de pertenencia —las que impiden
        // que enviar el id del perfil de un administrador sea una escalada de una línea— son
        // exactamente las mismas que ya protegen al portal.
        group.MapPost("/switch-profile", HandleClientSwitchProfileAsync)
            .WithName("ClientSwitchProfile")
            .WithSummary("Cambia el perfil vigente del portador y devuelve el grafo del perfil nuevo")
            .RequireAuthorization(UmsAuthPolicies.Satelite)
            .Produces<ClientAuthResponse>(StatusCodes.Status200OK)
            .Produces<ClientAuthErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ClientAuthErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ClientAuthErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ClientAuthErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> HandleClientGraphAsync(
        IUserAccountRepository   userAccounts,
        ITenantRepository        tenants,
        IAuthorizationGraphBuilder graphBuilder,
        Application.Common.Interfaces.ICacheDeGrafo cacheDeGrafo,
        HttpContext              httpContext,
        CancellationToken        cancellationToken,
        // Se enlaza EXPLÍCITAMENTE del query string. Es un tipo simple, así que las minimal APIs ya
        // lo harían solo, pero declararlo evita que un refactor que lo convierta en un tipo
        // complejo lo mande al cuerpo sin que nadie lo note — que es exactamente lo que le pasó a
        // `format`, prometido como `?format=xml` y enlazado del cuerpo desde el primer día.
        [FromQuery] string?      include = null)
    {
        var supportReferenceId = httpContext.TraceIdentifier;
        var principal = httpContext.User;

        // La proyección se resuelve ANTES de tocar la base de datos. Un `include` mal escrito debe
        // costar un 400 barato, no un grafo construido y tirado; y sobre todo debe costar un error,
        // no un silencio: si el servidor ignorase el token con errata, el satélite creería haber
        // pedido una sección que nunca recibió y leería su ausencia como «no tengo permisos» —o,
        // peor, se saltaría la comprobación. Vale más un fallo ruidoso al integrar que una
        // degradación muda en producción.
        //
        // Precedente en contra, y por eso conviene decirlo: `ClientAuthRequest.RequestedScopes` se
        // acepta y se ignora desde siempre. Es el anti-patrón que esto no repite.
        if (!GraphProjection.TryParse(include, out var proyeccion, out var tokenDesconocido))
        {
            // AUTH_001 es el código de «entrada obligatoria o inválida» del grupo, y el grupo usa
            // `ClientAuthErrorResponse`, no ProblemDetails: mezclarlos dejaría a los satélites con
            // dos formas de error en la misma superficie.
            //
            // Un nombre de sección desconocido no revela nada del inquilino, así que aquí no aplica
            // la anti-enumeración que hace callar a los errores vecinos (G-053, ADR-0156 §4): esos
            // se colapsan porque distinguirlos permitiría sondear qué existe, y un token del
            // vocabulario no es un dato de nadie.
            return Results.Json(new ClientAuthErrorResponse("AUTH_001",
                GraphProjection.MensajeDeValorDesconocido(tokenDesconocido!), supportReferenceId),
                statusCode: StatusCodes.Status400BadRequest);
        }

        // G-191: el token que entrega `/client/authenticate` es el SEMÁNTICO — lleva el inquilino
        // por CÓDIGO y su `sub` es el nombre de usuario, no un GUID, porque el token para sistemas
        // externos evita identificadores internos a propósito. Exigir un GUID aquí devolvía 401
        // incluso con un portador válido.
        //
        // La resolución vive en un solo sitio y la comparten los dos endpoints de portador: si
        // cada uno la escribiera, acabarían divergiendo en el status o en el mensaje, y esa
        // diferencia es precisamente lo que un atacante mide.
        var (tenant, user, errorDeToken) = await ResolverContextoDelPortadorAsync(
            userAccounts, tenants, principal, supportReferenceId, cancellationToken);

        if (errorDeToken is not null) return errorDeToken;

        // Se devuelve el grafo de AHORA, no el de cuando el usuario entró. Eso ya no obliga a
        // reconstruirlo en cada petición: lo retenido lleva en su clave los contadores de versión
        // global, del inquilino y del perfil, y CUALQUIER comando con éxito avanza el que
        // corresponda (`InvalidacionDeGrafoBehavior`). Una copia solo sobrevive mientras nada haya
        // cambiado; en cuanto algo cambia, su clave deja de existir y se reconstruye.
        //
        // Lo que NO se hace es fiarlo a una caducidad: eso sí serviría permisos viejos durante la
        // ventana. La caducidad absoluta de 15 minutos es una red, no el mecanismo.
        // El método de autenticación solo describe CÓMO entró el usuario y no altera qué permisos
        // tiene, que es lo único que se pide aquí. Reconstruir el proveedor IdP exacto exigiría un
        // claim que el token no lleva, así que no se finge: se declara local.
        //
        // El sistema sale del claim `sys_suite` del propio portador, que es el que se emitió al
        // autenticar. Sin él, un satélite que entró pidiendo `SDLC` recibiría al revalidar un
        // grafo de otro alcance —y por tanto otros menús y otros permisos— sin haber pedido nada
        // distinto. No se acepta el sistema por parámetro aquí a propósito: el portador ya dice de
        // qué sistema es, y dejar que el llamante lo contradiga abriría un camino para pedir el
        // grafo de un sistema distinto con un token acotado a otro.
        //
        // Un token de `NoProfileInSystem` NO lleva `sys_suite` (ADR-0156 §5.4). Ahí el eco se
        // pierde y la revalidación devuelve el grafo sin acotar, que es lo correcto: en cuanto un
        // administrador le asigne el perfil, el satélite lo verá sin volver a pedir credenciales.
        var suiteDelToken = principal.FindFirst("sys_suite")?.Value;

        // El PERFIL VIGENTE también sale del token (ADR-0156 §8). Reconstruir por desempate
        // ignoraba la conmutación: un usuario que acababa de cambiarse a PMO revalidaba y recibía
        // DIRECTORIO otra vez, porque el desempate no sabe que hubo una elección. Con el claim, la
        // conmutación sobrevive a la revalidación, que es lo único que la hace útil.
        //
        // El id NO se toma al pie de la letra: `BuildForProfileAsync` vuelve a comprobar que el
        // perfil pertenezca a este usuario y a este inquilino y que esté activo. Un token cuyo
        // perfil se desactivó desde que se emitió cae al desempate, que es el comportamiento
        // correcto —tenía acceso, se le retiró ese sombrero, le quedan los demás— y no un 500.
        var perfilDelToken = principal.FindFirst("profile_id")?.Value;

        // La proyección viaja a los DOS caminos de construcción. Si solo se proyectara uno, un
        // token con `profile_id` y otro sin él recibirían grafos de forma distinta ante la misma
        // petición, y el síntoma aparecería lejos de la causa.
        var hayPerfilEnElToken = Guid.TryParse(perfilDelToken, out var perfilId);

        // La caché SOLO entra con perfil en el token. Sin él, el perfil vigente lo elige el
        // desempate DENTRO de la construcción, así que aquí no se sabe bajo qué clave buscar — y
        // adivinarla serviría el grafo de otro perfil del mismo usuario. Ese camino es además el
        // residual: un token viejo o uno cuyo perfil se desactivó.
        var claveDelGrafo = hayPerfilEnElToken
            ? await cacheDeGrafo.ClaveAsync(
                  tenant.Props.Id.GetValue(), perfilId, suiteDelToken, proyeccion?.Canonico, cancellationToken)
            : null;

        if (claveDelGrafo is not null)
        {
            var retenido = await cacheDeGrafo.ObtenerAsync(claveDelGrafo, cancellationToken);

            // Se devuelve TAL CUAL, sin deserializar ni volver a serializar: lo retenido se guardó
            // ya con las opciones del cable, así que estos bytes son la respuesta. Pasarlo por
            // `Results.Ok` exigiría deserializarlo a `AuthGraphPayload` para que ASP.NET lo
            // reescribiera igual — dos conversiones para llegar al mismo texto.
            //
            // La CODIFICACIÓN se pasa explícita: sin ella el `Content-Type` sale
            // `application/json` a secas, mientras `Results.Ok` emite
            // `application/json; charset=utf-8`. Un cliente vería una cabecera distinta según
            // hubiera acierto o no — una diferencia observable entre dos respuestas que deben ser
            // indistinguibles.
            if (retenido is not null)
            {
                return Results.Content(retenido, "application/json", System.Text.Encoding.UTF8);
            }
        }

        var porPerfil = hayPerfilEnElToken
            ? await graphBuilder.BuildForProfileAsync(
                  user, tenant.Props.Id.GetValue(), perfilId, AuthMethod.Local(),
                  suiteDelToken, cancellationToken, proyeccion)
            : null;

        // Sin perfil en el token —o con uno que ya no resuelve— se cae al desempate, UNA vez. No
        // es un atajo: un token emitido antes de que existiera el claim, o cuyo perfil se desactivó
        // desde entonces, debe seguir sirviendo el grafo que le corresponda hoy, no un 500.
        var grafo = porPerfil is { IsSuccess: true }
            ? porPerfil
            : await graphBuilder.BuildAsync(
                  user, tenant.Props.Id.GetValue(), AuthMethod.Local(), suiteDelToken, cancellationToken,
                  proyeccion);

        if (grafo.IsFailure)
        {
            return Results.Json(new ClientAuthErrorResponse("AUTH_021",
                "No se pudo construir el grafo de autorización.", supportReferenceId),
                statusCode: StatusCodes.Status500InternalServerError);
        }

        // Se retiene bajo la clave que corresponde al perfil EFECTIVO, no al pedido: si el del token
        // no resolvió y se cayó al desempate, guardar bajo la clave del pedido serviría después un
        // grafo que no es el suyo. Con `null` no hay dónde guardarlo y se sirve sin retener.
        var claveParaGuardar = claveDelGrafo ?? await ClaveDelGrafoEfectivoAsync(
            cacheDeGrafo, tenant.Props.Id.GetValue(), grafo.Value, suiteDelToken, proyeccion, cancellationToken);

        if (claveParaGuardar is not null)
        {
            await cacheDeGrafo.GuardarAsync(claveParaGuardar, grafo.Value, cancellationToken);
        }

        return Results.Ok(AuthGraphPayload.Build(grafo.Value));
    }

    /// <summary>
    /// La clave del grafo REALMENTE construido, para el camino en que el perfil salió del desempate
    /// y no del token. Devuelve <c>null</c> si el grafo no trae perfil —un usuario sin ninguno en
    /// este inquilino—, porque entonces no hay nada bajo lo que retenerlo.
    /// </summary>
    private static async Task<string?> ClaveDelGrafoEfectivoAsync(
        Application.Common.Interfaces.ICacheDeGrafo cacheDeGrafo,
        Guid                                        tenantId,
        Domain.Authorization.Graph.AuthorizationGraph grafo,
        string?                                     suite,
        GraphProjection?                            proyeccion,
        CancellationToken                           cancellationToken)
    {
        var perfil = grafo.Context.Profile?.Id;
        if (perfil is null) return null;

        return await cacheDeGrafo.ClaveAsync(
            tenantId, perfil.Value, suite, proyeccion?.Canonico, cancellationToken);
    }

    private static async Task<IResult> HandleClientSwitchProfileAsync(
        ClientSwitchProfileRequest request,
        IUserAccountRepository     userAccounts,
        ITenantRepository          tenants,
        IMediator                  mediator,
        IJwtTokenService           jwtService,
        IAuthGraphFormatProvider   formatProvider,
        IFactory                   factory,
        HttpContext                httpContext,
        CancellationToken          cancellationToken)
    {
        var supportReferenceId = httpContext.TraceIdentifier;

        if (request.ProfileId == Guid.Empty)
        {
            return Results.Json(new ClientAuthErrorResponse("AUTH_001",
                "ProfileId es obligatorio.", supportReferenceId),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var (tenant, user, errorDeToken) = await ResolverContextoDelPortadorAsync(
            userAccounts, tenants, httpContext.User, supportReferenceId, cancellationToken);

        if (errorDeToken is not null) return errorDeToken;

        // El sistema que acota el bloque `profiles` sale del claim del PORTADOR, no del cuerpo:
        // el `systemCode` del cuerpo es la guarda de coherencia y el llamante podría omitirlo o
        // contradecirlo, mientras que el claim lo emitió UMS al autenticar y el cliente no puede
        // alterarlo sin invalidar la firma.
        var comando = new SwitchProfileCommand(
            ProfileId:  request.ProfileId,
            UserId:     user!.Props.Id.GetValue(),
            TenantId:   tenant!.Props.Id.GetValue(),
            ClientIp:   httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            SystemCode: httpContext.User.FindFirst("sys_suite")?.Value);

        var resultado = await mediator.Send(comando, cancellationToken);

        if (resultado.IsFailure)
        {
            var code = ExtractCode(resultado.Error);
            // AUTH_020 cubre «no existe» y «no es tuyo», que el manejador ya colapsa en el mismo
            // error a propósito: distinguirlos convertiría este endpoint en un detector de
            // perfiles ajenos para cualquiera con un portador válido. AUTH_021 es «inactivo».
            var statusCode = code switch
            {
                "AUTH_020" => StatusCodes.Status404NotFound,
                "AUTH_021" => StatusCodes.Status409Conflict,
                "AUTH_005" => StatusCodes.Status401Unauthorized,
                _          => StatusCodes.Status401Unauthorized,
            };
            return Results.Json(new ClientAuthErrorResponse(
                    code, SpanishMessage(code), supportReferenceId),
                statusCode: statusCode);
        }

        var grafo = resultado.Value.Graph;

        // Guarda de coherencia de sistema (ADR-0156 §8.5). `systemCode` es opcional y el satélite
        // lo envía siempre. En el flujo normal NUNCA dispara, porque el bloque `profiles` que el
        // cliente leyó ya venía acotado a su sistema; existe para que esa propiedad la garantice
        // el SERVIDOR y no la disciplina del cliente, por si alguna vez llega un `profileId` que
        // no salió de su propio grafo.
        //
        // Se comprueba DESPUÉS del cambio porque el sistema sale del grafo resultante, que es la
        // fuente autoritativa. La consecuencia, declarada y no escondida: el manejador ya habrá
        // registrado `Auth.Profile.Switch` como exitoso cuando esta guarda rechaza la respuesta.
        // El perfil vigente NO cambia en la base —no hay estado de sesión que mutar, el perfil
        // viaja en el token— así que el efecto es solo esa imprecisión de auditoría. Registrada
        // como G-225.
        var sistemaPedido = request.SystemCode?.Trim();
        if (!string.IsNullOrWhiteSpace(sistemaPedido) &&
            !string.Equals(grafo.Context.SystemSuite?.Code, sistemaPedido, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(new ClientAuthErrorResponse("AUTH_036",
                "El perfil solicitado no pertenece al sistema indicado.", supportReferenceId),
                statusCode: StatusCodes.Status409Conflict);
        }

        var (formato, serializado) = await SerializarGrafoAsync(
            grafo, request.Format, formatProvider, factory, httpContext, cancellationToken);

        // El token anterior NO se revoca (ADR-0156 §8.6): `ITokenRevocationStore` revoca por
        // usuario y ventana de tiempo, no por token, así que revocar aquí dejaría al usuario fuera
        // inmediatamente después de cambiarse —incluido el token recién emitido—. No hay escalada:
        // el usuario poseía legítimamente ambos perfiles. Quien debe descartar su copia anterior
        // es el satélite, reemplazando la entrada de su caché en vez de añadir otra.
        var token = jwtService.GenerateSemanticGraphToken(grafo);

        httpContext.Response.Headers["X-Graph-Format"] = formato;
        return Results.Ok(new ClientAuthResponse(
            Token:     token,
            TokenType: "Bearer",
            ExpiresIn: resultado.Value.ExpiresIn,
            IssuedAt:  resultado.Value.IssuedAt,
            Format:    formato,
            Graph:     serializado,
            RequestId: httpContext.TraceIdentifier));
    }

    /// <summary>
    /// Inquilino y cuenta del portador, con el mismo criterio que `GET /client/graph`: el token de
    /// cliente lleva el inquilino por CÓDIGO y su `sub` puede ser un GUID o el nombre de usuario.
    /// Devuelve el resultado de error ya formado cuando no resuelve, para que los dos endpoints
    /// que dependen del portador no puedan divergir en el status ni en el mensaje.
    /// </summary>
    private static async Task<(Evolith.Ums.Domain.Identity.Tenant.Tenant? Tenant,
                               UserAccountAgregado? Usuario,
                               IResult? Error)> ResolverContextoDelPortadorAsync(
        IUserAccountRepository userAccounts,
        ITenantRepository      tenants,
        ClaimsPrincipal        principal,
        string                 supportReferenceId,
        CancellationToken      cancellationToken)
    {
        var subject    = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("sub")?.Value;
        var correo     = principal.FindFirst(ClaimTypes.Email)?.Value
                      ?? principal.FindFirst("email")?.Value;
        var tenantCode = principal.FindFirst("tenant_code")?.Value;

        if (string.IsNullOrWhiteSpace(tenantCode) ||
            (string.IsNullOrWhiteSpace(subject) && string.IsNullOrWhiteSpace(correo)))
        {
            return (null, null, Results.Json(new ClientAuthErrorResponse("AUTH_020",
                "El token no identifica un usuario y un inquilino.", supportReferenceId),
                statusCode: StatusCodes.Status401Unauthorized));
        }

        var tenant = await tenants.GetByCodeAsync(tenantCode.ToUpperInvariant(), cancellationToken);
        var user   = await ResolverUsuarioDelTokenAsync(
            userAccounts, tenant, subject, correo, cancellationToken);

        // Mismo error para «no existe» y «no pertenece»: distinguirlos convertiría esto en un
        // detector de cuentas e inquilinos para cualquiera con un token válido.
        if (tenant is null || user is null ||
            user.Props.TenantId.GetValue() != tenant.Props.Id.GetValue())
        {
            return (null, null, Results.Json(new ClientAuthErrorResponse("AUTH_020",
                "No se encontró el contexto de autorización del token.", supportReferenceId),
                statusCode: StatusCodes.Status404NotFound));
        }

        return (tenant, user, null);
    }

    /// <summary>
    /// Serializa el grafo en el formato negociado: parámetro explícito, cabecera `Accept` o el
    /// que resuelva el inquilino. Compartido por la autenticación y el cambio de perfil para que
    /// un satélite que ya sabe leer la respuesta del login sepa leer la del cambio sin aprender
    /// nada nuevo.
    /// </summary>
    private static async Task<(string Formato, string Serializado)> SerializarGrafoAsync(
        Evolith.Ums.Domain.Authorization.Graph.AuthorizationGraph grafo,
        string?                  formatoPedido,
        IAuthGraphFormatProvider formatProvider,
        IFactory                 factory,
        HttpContext              httpContext,
        CancellationToken        cancellationToken)
    {
        var solicitado = formatoPedido?.ToUpperInvariant()
            ?? GetFormatFromAcceptHeader(httpContext.Request.Headers.Accept.ToString());

        var formato = await formatProvider.ResolveFormatAsync(
            grafo.Context.Tenant.Id, solicitado, cancellationToken);

        var serializer = factory
            .Create<GraphSerializationCriteria, IAuthorizationGraphSerializer>(
                new GraphSerializationCriteria(formato))
            .SingleOrDefault();

        // Sin serializador para el formato resuelto no se inventa uno ni se calla: JSON es el
        // formato que todo consumidor del contrato entiende, y el encabezado dirá cuál viajó.
        return serializer is null
            ? ("JSON", System.Text.Json.JsonSerializer.Serialize(
                   Evolith.Ums.Application.Authorization.Graph.Serializers.AuthGraphPayload.Build(grafo)))
            : (formato, serializer.Serialize(grafo));
    }

    /// <summary>
    /// Resuelve la cuenta que representa el portador. Por id cuando el `sub` es un GUID (token de
    /// grafo del portal) y por correo dentro del inquilino cuando el `sub` es el nombre de usuario
    /// (token semántico de `/client/authenticate`, que evita identificadores internos a propósito).
    /// </summary>
    private static async Task<UserAccountAgregado?> ResolverUsuarioDelTokenAsync(
        IUserAccountRepository userAccounts,
        Evolith.Ums.Domain.Identity.Tenant.Tenant? tenant,
        string? subject,
        string? correo,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParse(subject, out var userGuid))
        {
            return await userAccounts.GetByIdAsync(userGuid, cancellationToken);
        }

        if (tenant is null || string.IsNullOrWhiteSpace(correo))
        {
            return null;
        }

        return await userAccounts.GetByTenantAndEmailAsync(
            tenant.Props.Id.GetValue(),
            Evolith.Ums.Domain.Kernel.ValueObjects.Email.Create(correo),
            cancellationToken: cancellationToken);
    }

    private static async Task<IResult> HandleClientAuthenticateAsync(
        ClientAuthRequest      request,
        IMediator              mediator,
        IJwtTokenService       jwtService,
        IAuthGraphFormatProvider formatProvider,
        IFactory               factory,
        HttpContext            httpContext,
        CancellationToken      cancellationToken)
    {
        // SD-08: los mensajes al cliente se emiten en español y siempre acompañados
        // de un supportReferenceId para trazabilidad (mismo contrato que /auth/login).
        var supportReferenceId = httpContext.TraceIdentifier;

        if (string.IsNullOrWhiteSpace(request.TenantCode) ||
            string.IsNullOrWhiteSpace(request.Username)   ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Json(new ClientAuthErrorResponse("AUTH_001",
                "TenantCode, Username y Password son obligatorios.", supportReferenceId),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var command = new AuthenticateUserCommand(
            TenantCode: request.TenantCode.Trim().ToUpperInvariant(),
            Username:   request.Username.Trim(),
            Password:   request.Password,
            ClientIp:   clientIp,
            AccessScope: Evolith.Ums.Domain.Identity.Auth.AuthAccessScope.ExternalApi,
            RememberMe: false,
            SystemCode: request.SystemCode?.Trim());

        var result = await mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            var statusCode = GetStatusCode(result.Error);
            var code       = ExtractCode(result.Error);
            return Results.Json(new ClientAuthErrorResponse(
                    code, SpanishMessage(code), supportReferenceId),
                statusCode: statusCode);
        }

        var authResult = result.Value;
        var graph      = authResult.Graph;

        // Resolve format: query param > Accept header > tenant default (already in authResult)
        var requestedFormat = request.Format?.ToUpperInvariant()
            ?? GetFormatFromAcceptHeader(httpContext.Request.Headers.Accept.ToString());
        var format     = authResult.GraphFormat;
        var serialized = authResult.SerializedGraph;

        // Override serialization if caller explicitly requests a different format
        if (!string.IsNullOrWhiteSpace(requestedFormat) &&
            requestedFormat != format)
        {
            var tenantId = graph.Context.Tenant.Id;
            var resolvedFormat = await formatProvider.ResolveFormatAsync(
                tenantId, requestedFormat, cancellationToken);

            if (resolvedFormat != format)
            {
                var criteria   = new GraphSerializationCriteria(resolvedFormat);
                var serializer = factory
                    .Create<GraphSerializationCriteria, IAuthorizationGraphSerializer>(criteria)
                    .SingleOrDefault();

                if (serializer is not null)
                {
                    format     = resolvedFormat;
                    serialized = serializer.Serialize(graph);
                }
            }
        }

        // Generate graph JWT
        var token = jwtService.GenerateSemanticGraphToken(graph);

        var response = new ClientAuthResponse(
            Token:       token,
            TokenType:   "Bearer",
            ExpiresIn:   authResult.ExpiresIn,
            IssuedAt:    authResult.IssuedAt,
            Format:      format,
            Graph:       serialized,
            RequestId:   httpContext.TraceIdentifier);  // correlates with audit record

        // Set Content-Type to match the graph format
        httpContext.Response.Headers["X-Graph-Format"] = format;
        return Results.Ok(response);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // G-053 (anti-enumeración): en este endpoint público y anónimo, «tenant no existe»
    // (AUTH_002) y «tenant inactivo» (AUTH_003) NO deben distinguirse de «credenciales
    // inválidas» (AUTH_006). Antes AUTH_002→404 y AUTH_003→400 permitían enumerar códigos
    // de inquilino comparando el status contra el 401 de credenciales. Ahora todos colapsan
    // a 401 con un mensaje genérico e indistinguible. AUTH_011/012 (infraestructura) conservan su
    // semántica: no ponen ninguna credencial en duda.
    //
    // ADR-UMS-100 §2.4: AUTH_004 se sumó al colapso, tarde. `SpanishMessage` ya devolvía para él la
    // MISMA cadena exacta que para AUTH_002/003/006/017 —aquí no hay humano a quien darle la pista—,
    // así que el 404 era la única señal que quedaba en pie, y distinguía «el IdP te autenticó pero no
    // tienes cuenta UMS en este inquilino» de «no te autenticaste». Con el cuerpo ya uniformado, el
    // status era el último resquicio del oráculo que G-053 cerró.
    private static int GetStatusCode(string error) => error switch
    {
        var e when e.StartsWith("AUTH_002") => StatusCodes.Status401Unauthorized,
        var e when e.StartsWith("AUTH_003") => StatusCodes.Status401Unauthorized,
        var e when e.StartsWith("AUTH_004") => StatusCodes.Status401Unauthorized,
        var e when e.StartsWith("AUTH_005") => StatusCodes.Status401Unauthorized,
        var e when e.StartsWith("AUTH_006") => StatusCodes.Status401Unauthorized,
        // ADR-UMS-095: bloqueo temporal de cuenta (AUTH_017). Anti-enumeración G-053: en este endpoint
        // público y anónimo NO se expone 423 Locked; colapsa a 401 e indistinguible de credenciales
        // inválidas, para no revelar que la cuenta existe y está bloqueada. (En /auth/login sí es 423.)
        var e when e.StartsWith("AUTH_017") => StatusCodes.Status401Unauthorized,
        var e when e.StartsWith("AUTH_011") => StatusCodes.Status503ServiceUnavailable,
        var e when e.StartsWith("AUTH_012") => StatusCodes.Status503ServiceUnavailable,
        // FR-042 (ADR-UMS-097 §2.3): cadena de fallback de IdP agotada por indisponibilidad → 503, no 401.
        var e when e.StartsWith("AUTH_018") => StatusCodes.Status503ServiceUnavailable,
        // G-108 (ADR-UMS-097 §2.3): token endpoint OIDC indisponible (5xx/timeout/transporte) → 503, no 401.
        // Es INFRA, no credencial (el 4xx invalid_grant es AUTH_021 y colapsa al 401 por defecto).
        var e when e.StartsWith("AUTH_035") => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status401Unauthorized,
    };

    private static string ExtractCode(string error)
    {
        var idx = error.IndexOf(':');
        return idx > 0 ? error[..idx].Trim() : "AUTH_000";
    }

    // SD-08: traducción de los códigos de error del motor de autenticación a mensajes
    // en español, seguros para el cliente (sin filtrar detalles internos). No se reusa el
    // mensaje crudo del handler (en inglés y con posible detalle técnico).
    private static string SpanishMessage(string code) => code switch
    {
        // G-053 (anti-enumeración): AUTH_002 (tenant no existe) y AUTH_003 (tenant inactivo)
        // devuelven el MISMO mensaje genérico que las credenciales inválidas, para no revelar
        // la existencia ni el estado de un inquilino desde un endpoint público.
        var c when c.StartsWith("AUTH_002") => "No pudimos autenticar. Verifique sus credenciales.",
        var c when c.StartsWith("AUTH_003") => "No pudimos autenticar. Verifique sus credenciales.",
        var c when c.StartsWith("AUTH_004") => "No pudimos autenticar. Verifique sus credenciales.",
        var c when c.StartsWith("AUTH_005") => "Su cuenta no está activa. Contacte al administrador.",
        var c when c.StartsWith("AUTH_006") => "No pudimos autenticar. Verifique sus credenciales.",
        // ADR-UMS-095: bloqueo temporal (AUTH_017) — mismo mensaje genérico que credenciales inválidas
        // (anti-enumeración G-053); el mensaje accionable de bloqueo solo se entrega en /auth/login.
        var c when c.StartsWith("AUTH_017") => "No pudimos autenticar. Verifique sus credenciales.",
        var c when c.StartsWith("AUTH_011") => "El servicio de autenticación no está disponible temporalmente. Intente más tarde.",
        var c when c.StartsWith("AUTH_012") => "El servicio de autenticación no está disponible temporalmente. Intente más tarde.",
        var c when c.StartsWith("AUTH_018") => "El servicio de autenticación no está disponible temporalmente. Intente más tarde.",
        var c when c.StartsWith("AUTH_035") => "El servicio de autenticación no está disponible temporalmente. Intente más tarde.",
        // Cambio de perfil (ADR-0156 §8.4). AUTH_020 cubre a la vez «el perfil no existe» y «el
        // perfil no es tuyo»: el mensaje es deliberadamente el mismo, porque distinguirlos
        // permitiría enumerar perfiles ajenos preguntando por identificadores.
        var c when c.StartsWith("AUTH_020") => "No se encontró el perfil solicitado.",
        var c when c.StartsWith("AUTH_021") => "El perfil solicitado no está activo.",
        var c when c.StartsWith("AUTH_036") => "El perfil solicitado no pertenece al sistema indicado.",
        _ => "No pudimos autenticar. Intente nuevamente.",
    };

    private static string? GetFormatFromAcceptHeader(string acceptHeader) =>
        acceptHeader switch
        {
            var h when h.Contains("application/xml",  StringComparison.OrdinalIgnoreCase) => "XML",
            var h when h.Contains("text/yaml",        StringComparison.OrdinalIgnoreCase) => "YAML",
            var h when h.Contains("application/yaml", StringComparison.OrdinalIgnoreCase) => "YAML",
            var h when h.Contains("text/csv",         StringComparison.OrdinalIgnoreCase) => "CSV",
            _ => null,
        };
}

// ── Request / Response models ─────────────────────────────────────────────────

/// <summary>
/// Request body for POST /api/v1/client/authenticate.
/// </summary>
public record ClientAuthRequest(
    string   TenantCode,                    // e.g. "LOGISTICS_CORE"
    string   Username,                      // email or identity reference
    string   Password,                      // plaintext (Local) or MOCK-* (stub IDP)
    string?  Format           = null,       // override graph format: JSON|XML|YAML|CSV
    string[]? RequestedScopes = null,       // optional scope filter (future use)
    // ADR-0156 §3.2 — código del sistema que pide el grafo. OPCIONAL a propósito: un portal
    // multiproducto lo omite y recibe los perfiles de todos sus sistemas; un satélite como el
    // Tablero SDLC envía el suyo y recibe solo los de ese sistema.
    //
    // No se reutiliza `RequestedScopes` para esto: es un filtro de ÁMBITOS, no de sistema, y
    // darle una segunda semántica lo dejaría inservible para la primera (§3.3).
    string?  SystemCode       = null);

/// <summary>
/// Cuerpo de <c>POST /api/v1/client/switch-profile</c> (ADR-0156 §8.2).
/// </summary>
/// <param name="ProfileId">
/// Obligatorio. Sale de <c>graph.profiles[].id</c>, que el contrato emite siempre desde 2.4.0.
/// Nunca se confía en él: el manejador comprueba que el perfil pertenezca al usuario y a su
/// inquilino antes de construir nada.
/// </param>
/// <param name="SystemCode">
/// Opcional. Guarda de coherencia: si viene, el perfil debe pertenecer a ese sistema. En el flujo
/// normal no dispara, porque el bloque <c>profiles</c> ya viaja acotado; existe para que esa
/// propiedad la garantice el servidor y no la disciplina del cliente.
/// </param>
/// <param name="Format">Igual que en la autenticación: JSON|XML|YAML|CSV.</param>
public record ClientSwitchProfileRequest(
    Guid    ProfileId,
    string? SystemCode = null,
    string? Format     = null);

/// <summary>
/// Response from POST /api/v1/client/authenticate.
/// The client system uses Token for subsequent API calls and Graph for
/// authorization decisions without re-querying UMS.
/// </summary>
public record ClientAuthResponse(
    string   Token,         // JWT embedding graph claims
    string   TokenType,     // "Bearer"
    int      ExpiresIn,     // seconds until token expiry
    DateTime IssuedAt,
    string   Format,        // serialization format used: "JSON"|"XML"|"YAML"|"CSV"
    string   Graph,         // full authorization graph serialized in Format
    string   RequestId);    // correlates with the audit record for traceability

public record ClientAuthErrorResponse(
    string   Code,
    string   Message,
    string?  SupportReferenceId);
