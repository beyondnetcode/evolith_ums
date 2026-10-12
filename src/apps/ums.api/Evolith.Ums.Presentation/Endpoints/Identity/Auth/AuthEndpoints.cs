namespace Evolith.Ums.Presentation.Endpoints.Identity.Auth;

#pragma warning disable S125

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Application.Identity.Auth.Commands;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Presentation.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MsConfigProvider = Microsoft.Extensions.Configuration.IConfigurationProvider;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        // Login endpoint
        group.MapPost("/login", HandleLoginAsync)
            .WithName("Login")
            .WithSummary("Authenticate user with tenant, username and password");

        // Logout endpoint
        group.MapPost("/logout", (Delegate)HandleLogoutAsync)
            .WithName("Logout")
            .WithSummary("Terminate the current session");

        // Refresh token endpoint
        group.MapPost("/refresh", HandleRefreshTokenAsync)
            .WithName("RefreshToken")
            .WithSummary("Refresh access token using session cookie")
            .RequireAuthorization();

        // Renovación por refresh token opaco (ADR-UMS-091 / FR-015). Anónimo a propósito:
        // el refresh token ES la credencial y se usa cuando el access token ya expiró,
        // por lo que no puede exigir una sesión viva. Solo funciona si el inquilino
        // activó la capacidad (fail-closed en el handler).
        group.MapPost("/refresh-token", HandleRefreshTokenGrantAsync)
            .WithName("RefreshTokenGrant")
            .WithSummary("Renew the session with an opaque refresh token (ADR-UMS-091, tenant opt-in)")
            .AllowAnonymous();

        // Get current session info
        group.MapGet("/session", HandleGetSessionAsync)
            .WithName("GetSession")
            .WithSummary("Get current authenticated session information")
            .RequireAuthorization();

        // Switch tenant context (internal admins only)
        group.MapPost("/switch-profile", HandleSwitchProfileAsync)
            .WithName("SwitchProfile")
            .WithSummary("Cambia el perfil vigente de la sesión sin volver a autenticarse")
            .Produces<LoginSuccessResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
            // Sin RequireAuthorization(): el token se valida en el manejador, igual que en
            // switch-tenant, para no depender de que el esquema JWT esté configurado.

        group.MapPost("/switch-tenant", HandleSwitchTenantAsync)
            .WithName("SwitchTenant")
            .WithSummary("Switch current tenant context (internal admins only)");
            // No RequireAuthorization() - we validate the JWT token directly in the handler

        // Solicitud de restablecimiento — pública. NO cambia la contraseña ni devuelve credencial
        // alguna (G-188): solo emite un token de un solo uso hacia el buzón del titular. La
        // respuesta es idéntica exista o no la cuenta, en cuerpo, código y tiempo.
        group.MapPost("/forgot-password", async (
            ForgotPasswordCommand command,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToOk(context);
        })
        .WithName("ForgotPassword")
        .WithSummary("Solicita un restablecimiento de contraseña. Siempre 200 y siempre la misma respuesta.")
        .AllowAnonymous()
        .Produces<ForgotPasswordResponse>(StatusCodes.Status200OK);

        // Canje del token — pública porque el token ES la credencial: quien lo presenta acaba de
        // probar la posesión del buzón y por definición no tiene sesión. Aquí, y solo aquí,
        // cambia la contraseña (G-188).
        group.MapPost("/reset-password", HandleResetPasswordAsync)
        .WithName("ResetPassword")
        .WithSummary("Canjea el token de restablecimiento y fija la nueva contraseña.")
        .AllowAnonymous()
        .Produces<ResetPasswordResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/user-signup", async (
            SignupUserCommand command,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => "/api/v1/auth/user-signup", context);
        })
        .WithName("UserSignup")
        .WithSummary("Request access to an existing tenant.")
        .AllowAnonymous()
        .Produces<UserSignupResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/tenant-signup", async (
            Evolith.Ums.Application.Identity.Tenant.SignupRequests.Commands.RequestTenantSignupCommand command,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => "/api/v1/auth/tenant-signup", context);
        })
        .WithName("TenantSignup")
        .WithSummary("Request onboarding for a new tenant.")
        .AllowAnonymous()
        .Produces<Evolith.Ums.Application.Identity.Tenant.SignupRequests.DTOs.RequestTenantSignupResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    /// <summary>
    /// Delegates authentication to AuthenticateUserCommandHandler (full graph engine).
    /// Sets a session cookie for the web frontend and returns the full AuthorizationGraph
    /// in the response body so the UI can build its navigation and permission model.
    /// </summary>
    private static async Task<IResult> HandleLoginAsync(
        LoginRequest  request,
        IMediator     mediator,
        IJwtTokenService jwtTokenService,
        Evolith.Ums.Application.Configuration.Services.IConfigurationProvider configProvider,
        Evolith.Ums.Application.Identity.Auth.IRefreshTokenPolicyProvider refreshPolicyProvider,
        Evolith.Ums.Application.Identity.Auth.IRefreshTokenStore refreshTokenStore,
        Evolith.Ums.Application.Common.Interfaces.IFunctionalTransaction functionalTransaction,
        HttpContext   httpContext,
        CancellationToken cancellationToken)
    {
        var supportReferenceId = httpContext.TraceIdentifier;

        if (string.IsNullOrWhiteSpace(request.TenantCode) ||
            string.IsNullOrWhiteSpace(request.Username)   ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            // ADR-0096 §2.2 / ADR-UMS-084: al usuario se le entrega el localizador legible,
            // no el TraceIdentifier crudo.
            return Results.BadRequest(new LoginErrorResponse(
                ErrorCodes.ValidationError,
                "Tenant code, username and password are required.",
                SupportReferenceId: await functionalTransaction.GetOrMintLocatorAsync(cancellationToken)));
        }

        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        var command = new AuthenticateUserCommand(
            TenantCode: request.TenantCode.Trim().ToUpperInvariant(),
            Username:   request.Username.Trim(),
            Password:   request.Password,
            ClientIp:   clientIp,
            AccessScope: Evolith.Ums.Domain.Identity.Auth.AuthAccessScope.ExternalApi,
            RememberMe: request.RememberMe,
            SystemCode: string.IsNullOrWhiteSpace(request.SystemCode) ? null : request.SystemCode.Trim());

        var result = await mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return MapAuthError(result.Error, await functionalTransaction.GetOrMintLocatorAsync(cancellationToken));
        }

        var authResult = result.Value;
        var graph      = authResult.Graph;
        var ctx        = graph.Context;

        // G-247: la sesión se acuña UNA vez y viaja en los dos portadores —cookie y JWT—, porque
        // un mismo dispositivo puede hablar por cualquiera de los dos y cerrar sesión debe cerrarlo
        // entero. Es lo que permite que el logout cierre este dispositivo y solo este: la clave de
        // cierre es la sesión, no el usuario.
        var sessionId = Guid.NewGuid().ToString("N");

        // Generate graph JWT
        var jwtToken = jwtTokenService.GenerateGraphToken(graph, sessionId);

        // Set session cookie for web frontend
        var isInternalAdmin = ctx.Tenant.IsManagementOwner;
        var cookieClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, ctx.User.Id.ToString()),
            new(ClaimTypes.Name,           ctx.User.Email),
            new("tenant_id",               ctx.Tenant.Id.ToString()),
            new("tenant_code",             ctx.Tenant.Code),
            new("username",                ctx.User.Username),
            new("auth_method",             graph.Authentication.Method),
            new("is_internal_admin",       isInternalAdmin ? "true" : "false"),
            new("sid",                     sessionId),
        };
        // G-122: en el grafo lobby (onboarding pendiente, cuenta activa SIN perfil) Role/Profile/
        // SystemSuite son null; añadir esos claims incondicionalmente provocaba NullReferenceException
        // → 500 en el login. Se agregan solo si existen (igual que Branch), preservando el flujo lobby.
        if (ctx.Role is not null)
        {
            cookieClaims.Add(new Claim(ClaimTypes.Role, ctx.Role.Code));
            cookieClaims.Add(new Claim("role_name", ctx.Role.Name));
        }
        if (ctx.Profile is not null)
            cookieClaims.Add(new Claim("profile_id", ctx.Profile.Id.ToString()));
        if (ctx.SystemSuite is not null)
            cookieClaims.Add(new Claim("sys_suite", ctx.SystemSuite.Code));
        // G-297: la cookie recuerda el sistema PEDIDO, además del resuelto. Es lo que el refresco
        // deslizante necesita para reconstruir el grafo como lo pidió este ingreso; sin ello, la
        // primera renovación automática —que el cliente hace solo— devolvía la sesión al sistema
        // del desempate. Solo se estampa si hubo petición: su ausencia significa «multiproducto».
        if (ctx.RequestedSystem is not null)
            cookieClaims.Add(new Claim("req_sys", ctx.RequestedSystem.Code));
        if (ctx.Branch is not null)
            cookieClaims.Add(new Claim("branch_id", ctx.Branch.Id.ToString()));

        // Los scopes del grafo, igual que en el portador (G-280). Sin esto la cookie identifica sin
        // autorizar: el token JWT llevaba los permisos y la sesion del portal no, de modo que en
        // cuanto el aspecto empezo a aplicar, el administrador de UMS dejo de poder usar su propio
        // portal —medido: `view_tenants` denegado 25 veces en una sola corrida del carril A—. La
        // fuente es la misma y la regla tambien; lo que fallaba era que solo una de las dos vias
        // la leia.
        // UN solo claim con los scopes separados por espacio, no uno por permiso.
        //
        // Cada claim de la cookie de sesion cuesta su nombre, su valor y el andamiaje del ticket
        // cifrado, asi que un claim por permiso multiplica el tamano por algo mas que el texto. Con
        // 83 scopes el `Set-Cookie` resultante desborda el buffer de cabecera del proxy y el login
        // responde **502**: medido contra el stage el 2026-08-21 con el administrador del Tablero
        // —`upstream sent too big header`—, mientras que un rol de 49 scopes entraba sin problema.
        // Y por encima del proxy espera el limite de 4096 bytes por cookie que imponen los
        // navegadores, que es la misma pared un paso mas alla.
        //
        // El lector ya sabia leerlo asi: `UserContext.HasPermission` parte el valor por espacios
        // desde antes que esto existiera. El formato es ademas el de OAuth 2 para `scope`.
        if (graph.Scopes.Count > 0)
            cookieClaims.Add(new Claim("scope", string.Join(' ', graph.Scopes)));

        var identity  = new ClaimsIdentity(cookieClaims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = request.RememberMe,
                ExpiresUtc   = request.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : DateTimeOffset.UtcNow.AddHours(1),
            });

        // Resolve tenant-specific parameters from the in-memory cache (loaded at startup).
        // These are included in the session so the frontend never needs a separate round-trip.
        var tenantId = ctx.Tenant.Id;
        var cfg = configProvider.ForTenant(tenantId);
        var sessionParams = new SessionParameters(
            SessionTimeoutMinutes:  cfg.SessionTimeoutMinutes,
            AccessTokenDurationMs:  cfg.AccessTokenDurationMs,
            RefreshTokenDurationMs: cfg.RefreshTokenDurationMs,
            MaxLoginAttempts:       cfg.MaxLoginAttempts,
            MinPasswordLength:      cfg.MinPasswordLength,
            MfaRequiredForAdmin:    cfg.MfaRequiredForAdmin,
            MfaAllowedMethods:      cfg.MfaAllowedMethods.Select(method => method.Name).ToArray(),
            CustomBrandingEnabled:  cfg.CustomBrandingEnabled,
            DefaultLanguage:        cfg.DefaultLanguage,
            DefaultTimezone:        cfg.DefaultTimezone);

        // Build enriched permissions array from Allow options for backward compat
        var permissions = GraphNavigation.AllowedPairs(graph)
            .Select(p => $"{p.Code}:{p.ActionCode}")
            .ToArray();

        // Refresh token (ADR-UMS-091/FR-015): solo si el inquilino activó la capacidad.
        // Fail-closed: política deshabilitada ⇒ no se emite (modelo de ADR-UMS-088 intacto).
        string? refreshTokenPlaintext = null;
        var refreshExpiresIn = request.RememberMe ? 604800 : 86400;
        var refreshPolicy = refreshPolicyProvider.Resolve(tenantId);
        if (refreshPolicy.Enabled)
        {
            refreshTokenPlaintext = jwtTokenService.GenerateRefreshToken();
            var issuedAtUtc = DateTime.UtcNow;
            await refreshTokenStore.IssueAsync(
                tenantId,
                ctx.User.Id,
                Guid.NewGuid(),
                Evolith.Ums.Application.Identity.Auth.RefreshTokenHasher.Hash(refreshTokenPlaintext),
                issuedAtUtc,
                issuedAtUtc.AddMinutes(refreshPolicy.LifetimeMinutes),
                // G-295/G-297: la familia nace sabiendo a qué sesión pertenece y qué sistema se
                // pidió. Es el único sitio donde puede aprenderlo: el canje posterior es anónimo.
                sessionId,
                ctx.RequestedSystem?.Code,
                cancellationToken);
            refreshExpiresIn = refreshPolicy.LifetimeMinutes * 60;
        }

        return Results.Ok(new LoginSuccessResponse(
            SessionId:        supportReferenceId,
            SessionTrackingId: $"{supportReferenceId}-session",
            UserId:           ctx.User.Id.ToString(),
            Username:         ctx.User.Username,
            Email:            ctx.User.Email,
            TenantId:         tenantId.ToString(),
            TenantCode:       ctx.Tenant.Code,
            TenantName:       ctx.Tenant.Name,
            // G-122: en el grafo lobby (onboarding pendiente) Role/Profile son null; se serializan como
            // null y el cliente reacciona a AuthorizationGraph.onboardingPending (antes NRE → 500).
            Role:             ctx.Role?.Code,
            RoleName:         ctx.Role?.Name,
            ProfileId:        ctx.Profile?.Id.ToString(),
            Permissions:      permissions,
            Language:         sessionParams.DefaultLanguage,
            Token:            jwtToken,
            TokenType:        "Bearer",
            ExpiresIn:        authResult.ExpiresIn,
            RefreshExpiresIn: refreshExpiresIn,
            IsInternalAdmin:  isInternalAdmin,
            SessionParameters: sessionParams,
            AuthorizationGraph: AuthGraphPayload.Build(graph),
            GraphFormat:      authResult.GraphFormat,
            RefreshToken:     refreshTokenPlaintext));
    }

    /// <summary>
    /// Canje del token de restablecimiento. Todo fallo del canje se traduce al MISMO 400 con el
    /// mismo texto: el detalle interno (token inexistente, vencido, ya gastado o cuenta
    /// inhabilitada) diría al atacante qué tokens y qué cuentas existen, que es justo lo que el
    /// flujo anónimo oculta. Se exceptúan los fallos de validación de entrada —la política de
    /// contraseña— porque hablan de lo que el llamante acaba de escribir, no del estado del
    /// servidor, y ocultarlos dejaría al usuario legítimo sin saber por qué se le rechaza.
    /// </summary>
    private static async Task<IResult> HandleResetPasswordAsync(
        ResetPasswordCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var esFalloDeValidacion = result.Error.StartsWith("Validation.Failed", StringComparison.Ordinal);

        return Results.Json(new LoginErrorResponse(
            esFalloDeValidacion ? ErrorCodes.ValidationError : ErrorCodes.InvalidResetToken,
            esFalloDeValidacion
                ? result.Error["Validation.Failed:".Length..].Trim()
                : "El código de restablecimiento no es válido o ha expirado. Solicite uno nuevo.",
            SupportReferenceId: null), statusCode: StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// Traduce el fallo del handler al desenlace HTTP del login (ADR-UMS-100).
    ///
    /// <para>NINGUNA rama devuelve 404. Un 404 aquí es un oráculo: distingue desenlaces que los
    /// mensajes de abajo evitan distinguir. <c>AUTH_004</c> y <c>AUTH_006</c> comparten mensaje
    /// palabra por palabra —alguien lo escribió así a propósito— y comparten ahora también el 401,
    /// que es lo que hace que la intención se cumpla: con estados distintos, el estado delataba lo
    /// que el mensaje callaba. Es la misma conclusión que G-053 ya aplicó en
    /// <see cref="ClientAuthEndpoints"/>, el endpoint anónimo de máquina.</para>
    ///
    /// <para><c>AUTH_004</c> no es «el usuario no existe»: la rama local responde <c>AUTH_006</c> a
    /// un usuario inexistente, igual que a una contraseña equivocada, y ahí nunca hubo nada que
    /// filtrar. <c>AUTH_004</c> sólo se emite tras autenticar contra el IdP, así que su 404 era un
    /// oráculo para quien ya está dentro —qué identidades federadas tienen cuenta UMS en qué
    /// inquilino—, no un barrido anónimo. Ver ADR-UMS-100 §2.2.</para>
    ///
    /// <para><c>AUTH_002</c> va a 400 y no a 401 porque aquí el CUERPO ya revela la existencia del
    /// inquilino: el mensaje es «Verifique el código del tenant», distinto del de credenciales y
    /// elegido así para quien teclea mal el código de su organización. Colapsar el estado no
    /// ocultaría nada que el cuerpo no siga diciendo. En <c>/client/authenticate</c> —sin humano al
    /// otro lado— sí se colapsan los dos (G-053). Ver ADR-UMS-100 §2.3.</para>
    /// </summary>
    private static IResult MapAuthError(string error, string supportReferenceId) => error switch
    {
        var e when e.StartsWith("AUTH_002") => Results.BadRequest(new LoginErrorResponse(
            ErrorCodes.TenantNotFound, "No pudimos iniciar sesión. Verifique el código del tenant.", supportReferenceId)),
        var e when e.StartsWith("AUTH_003") => Results.BadRequest(new LoginErrorResponse(
            ErrorCodes.TenantInactive, "El tenant no está activo. Contacte al administrador.", supportReferenceId)),
        var e when e.StartsWith("AUTH_004") => Results.Json(new LoginErrorResponse(
            ErrorCodes.UserNotFound, "No pudimos iniciar sesión. Verifique sus credenciales.", supportReferenceId), statusCode: 401),
        var e when e.StartsWith("AUTH_005") => Results.Json(new LoginErrorResponse(
            ErrorCodes.UserNotActive, "Su cuenta no está activa. Contacte al administrador.", supportReferenceId), statusCode: 401),
        var e when e.StartsWith("AUTH_006") => Results.Json(new LoginErrorResponse(
            ErrorCodes.InvalidCredentials, "No pudimos iniciar sesión. Verifique sus credenciales.", supportReferenceId), statusCode: 401),
        var e when e.StartsWith("AUTH_011") => Results.Json(new LoginErrorResponse(
            ErrorCodes.MfaEnrollmentRequired, "Se requiere MFA. Registre y verifique un método MFA para continuar.", supportReferenceId), statusCode: 403),
        // ADR-UMS-095 (UMS-066): cuenta bloqueada temporalmente por intentos fallidos → 423 Locked.
        // Mensaje accionable en español, sin revelar la validez de las credenciales.
        // AUTH_017 (AUTH_012 ya está tomado con el significado «no IDP adapter registered»).
        var e when e.StartsWith("AUTH_017") => Results.Json(new LoginErrorResponse(
            ErrorCodes.AccountLocked, "Su cuenta fue bloqueada temporalmente por varios intentos fallidos. Espere unos minutos e intente nuevamente, o contacte al administrador.", supportReferenceId), statusCode: 423),
        // FR-042 (ADR-UMS-097 §2.3): cadena de fallback de IdP agotada por indisponibilidad de infraestructura
        // → 503 Service Unavailable (NO 401): el servicio de auth federada no está disponible, las credenciales
        // no se pusieron en duda. AUTH_012 (sin adaptador) comparte la misma semántica de indisponibilidad.
        // G-108: AUTH_035 (token endpoint OIDC 5xx/timeout/transporte) es INFRA → 503; el 4xx invalid_grant
        // sigue en AUTH_021 (credencial), que colapsa al 401 por defecto y jamás llega aquí.
        var e when e.StartsWith("AUTH_018") || e.StartsWith("AUTH_012") || e.StartsWith("AUTH_035") => Results.Json(new LoginErrorResponse(
            "AUTH_018", "El servicio de autenticación no está disponible temporalmente. Intente más tarde.", supportReferenceId), statusCode: 503),
        _ => Results.Json(new LoginErrorResponse("AUTH_000", "No pudimos iniciar sesión. Intente nuevamente.", supportReferenceId), statusCode: 401),
    };

    /// <summary>
    /// Renovación por refresh token opaco (ADR-UMS-091/FR-015). Valida el token, regenera
    /// el grafo COMPLETO, rota el refresh y devuelve el nuevo access token + grafo, para
    /// que el frontend actualice su modelo de permisos sin re-login.
    /// </summary>
    private static async Task<IResult> HandleRefreshTokenGrantAsync(
        RefreshTokenRequest request,
        IMediator     mediator,
        IJwtTokenService jwtTokenService,
        Evolith.Ums.Application.Common.Interfaces.IFunctionalTransaction functionalTransaction,
        HttpContext   httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            // ADR-0096 §2.2 / ADR-UMS-084: referencia legible para el usuario final.
            return Results.BadRequest(new LoginErrorResponse(
                ErrorCodes.ValidationError,
                "Refresh token is required.",
                SupportReferenceId: await functionalTransaction.GetOrMintLocatorAsync(cancellationToken)));
        }

        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        var result = await mediator.Send(
            new RefreshAuthenticationCommand(request.RefreshToken, clientIp),
            cancellationToken);

        if (result.IsFailure)
        {
            return MapRefreshError(result.Error, await functionalTransaction.GetOrMintLocatorAsync(cancellationToken));
        }

        var value = result.Value;
        var graph = value.Graph;

        // G-295: la sesión del ingreso, recuperada de la familia de refresco. Aquí no se pasaba
        // NADA, así que cada canje acuñaba un `sid` nuevo: bastaba una renovación —que el cliente
        // hace solo— para que el portador vivo dejara de estar atado a la sesión que el logout
        // revoca. `POST /auth/logout` seguía respondiendo 200 y el portador renovado seguía
        // abriendo la puerta hasta caducar.
        //
        // Null solo para familias emitidas antes de este cambio: se acuña una sesión nueva, que es
        // exactamente lo que hacían todas hasta ahora.
        var jwtToken = jwtTokenService.GenerateGraphToken(graph, value.SessionId);

        var permissions = GraphNavigation.AllowedPairs(graph)
            .Select(p => $"{p.Code}:{p.ActionCode}")
            .ToArray();

        return Results.Ok(new RefreshTokenGrantResponse(
            Token:            jwtToken,
            TokenType:        "Bearer",
            ExpiresIn:        value.ExpiresIn,
            RefreshToken:     value.NewRefreshToken,
            RefreshExpiresIn: value.RefreshExpiresIn,
            Permissions:      permissions,
            AuthorizationGraph: AuthGraphPayload.Build(graph),
            GraphFormat:      value.GraphFormat));
    }

    private static IResult MapRefreshError(string error, string supportReferenceId)
    {
        // La capacidad apagada es una decisión de política ⇒ 403; el resto son
        // credenciales de renovación inválidas ⇒ 401.
        var code = error.Split(':', 2)[0];
        var statusCode = code == RefreshErrorCodes.Disabled ? 403 : 401;
        var clientCode = code == RefreshErrorCodes.Disabled
            ? ErrorCodes.AccessDenied
            : ErrorCodes.SessionExpired;
        return Results.Json(
            new LoginErrorResponse(clientCode, "No se pudo renovar la sesión. Inicie sesión nuevamente.", supportReferenceId),
            statusCode: statusCode);
    }

    /// <summary>
    /// Refresh deslizante por cookie de sesión (D-019 / ADR-UMS-091). Espeja el login:
    /// resuelve el principal autenticado desde la cookie, <b>regenera el grafo de autorización
    /// vigente</b> (RefreshSessionCommand → IAuthorizationGraphBuilder) y emite un GRAPH JWT que
    /// porta los permisos actuales, en vez de re-firmar un JWT con permisos vacíos. Cierra la
    /// brecha de latencia de permisos del refresh (los permisos ya no quedan estancados hasta el
    /// próximo re-login). No es el flujo de refresh token opaco (/refresh-token), que sigue intacto.
    /// </summary>
    private static async Task<IResult> HandleRefreshTokenAsync(
        IMediator mediator,
        IJwtTokenService jwtTokenService,
        Evolith.Ums.Application.Configuration.Services.IConfigurationProvider configProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
        {
            return Results.Json(new LoginErrorResponse(
                ErrorCodes.SessionExpired,
                "Session expired or invalid",
                SupportReferenceId: null), statusCode: 401);
        }

        var userIdStr = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tenantIdStr = httpContext.User.FindFirstValue("tenant_id");

        if (!Guid.TryParse(userIdStr, out var userId) || !Guid.TryParse(tenantIdStr, out var tenantId))
        {
            return Results.Unauthorized();
        }

        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        // G-295/G-297: la sesión y el sistema pedido viajan en la credencial que se acaba de
        // presentar —la cookie del portal o el portador de un satélite—, así que se LEEN de ella en
        // vez de dejar que la renovación se los invente. Es la diferencia entre renovar una sesión y
        // abrir una distinta con la misma cara.
        var sessionId = httpContext.User.FindFirstValue("sid");
        var sistemaPedido = httpContext.User.FindFirstValue("req_sys");

        // Regenera el grafo vigente (espejo del login) en vez de re-firmar claims vacíos.
        var result = await mediator.Send(
            new RefreshSessionCommand(userId, tenantId, clientIp, sistemaPedido),
            cancellationToken);

        if (result.IsFailure)
        {
            // El principal ya no puede renovar (inquilino/usuario inactivo o grafo irreconstruible).
            return Results.Json(new LoginErrorResponse(
                ErrorCodes.SessionExpired,
                "Session expired or invalid",
                SupportReferenceId: null), statusCode: 401);
        }

        var value = result.Value;
        var graph = value.Graph;

        // Graph JWT: el token ES el grafo, portando los permisos actuales (igual que el login), y la
        // MISMA sesión que la credencial que lo pidió (G-295). Antes salía con un `sid` recién
        // inventado y el logout dejaba de alcanzarlo.
        var newToken = jwtTokenService.GenerateGraphToken(graph, sessionId);

        var permissions = GraphNavigation.AllowedPairs(graph)
            .Select(p => $"{p.Code}:{p.ActionCode}")
            .ToArray();

        // Valores derivados de la config efectiva del inquilino, como en el login (no hardcodeados).
        var cfg = configProvider.ForTenant(tenantId);
        var refreshExpiresIn = cfg.RefreshTokenDurationMs / 1000;

        var sessionTrackingId = httpContext.User.FindFirstValue("session_tracking_id") ?? httpContext.TraceIdentifier;

        return Results.Ok(new RefreshTokenResponse(
            Token: newToken,
            TokenType: "Bearer",
            ExpiresIn: value.ExpiresIn,
            RefreshExpiresIn: refreshExpiresIn,
            SessionTrackingId: sessionTrackingId,
            Permissions: permissions,
            Language: cfg.DefaultLanguage,
            AuthorizationGraph: AuthGraphPayload.Build(graph),
            GraphFormat: value.GraphFormat));
    }

    /// <summary>
    /// Cierra la sesión ACTUAL, y solo esa.
    ///
    /// <para>Hasta G-247 esto no cerraba nada: revocaba las familias de refresh y llamaba a
    /// <c>SignOutAsync</c>, que solo le pide al navegador que borre la cookie. El portador seguía
    /// siendo criptográficamente válido, así que quien tuviera una copia —una máquina compartida,
    /// una captura de red— conservaba el acceso hasta que caducara solo. Se midió en vivo: tras el
    /// logout, la misma cookie seguía devolviendo 200, también contra el mismo pod.</para>
    ///
    /// <para>La sesión se anota como cerrada en un almacén compartido, y
    /// <c>TokenRevocationMiddleware</c> la consulta en cada petición autenticada. La clave es la
    /// SESIÓN y no el usuario: cerrar sesión en el portátil no debe echar a nadie del móvil. Para
    /// «esta cuenta queda fuera de todas partes» está <c>ITokenRevocationStore</c>, que es por
    /// usuario y lo usan el bloqueo, el borrado y el cambio de contraseña.</para>
    /// </summary>
    private static async Task<IResult> HandleLogoutAsync(
        HttpContext httpContext,
        Evolith.Ums.Application.Identity.Auth.IRefreshTokenStore refreshTokenStore,
        ISessionRevocationStore sessionRevocationStore,
        CancellationToken cancellationToken)
    {
        // El plazo: hasta donde puede llegar el portador más largo que emite el login
        // (`RememberMe` = 7 días). Recordar la sesión más allá no protege de nada —el portador ya
        // no valdría— y solo haría crecer la lista.
        var sessionId = httpContext.User.FindFirstValue("sid");
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            await sessionRevocationStore.RevocarAsync(
                sessionId, DateTime.UtcNow.AddDays(7), cancellationToken);
        }

        // Logout real (ADR-UMS-091/FR-016): además de cerrar la cookie, revoca todas las
        // familias vivas de refresh del usuario para que ningún refresh token siga sirviendo.
        var userIdStr   = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tenantIdStr = httpContext.User.FindFirstValue("tenant_id");
        if (Guid.TryParse(userIdStr, out var userId) && Guid.TryParse(tenantIdStr, out var tenantId))
        {
            await refreshTokenStore.RevokeAllForUserAsync(
                tenantId,
                userId,
                reason: "logout",
                revokedAtUtc: DateTime.UtcNow,
                cancellationToken);
        }

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Ok(new { message = "Logged out successfully" });
    }

    private static IResult HandleGetSessionAsync(HttpContext httpContext)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
        {
            return Results.Unauthorized();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        // G-191/G-187: este endpoint ya no lo consume solo el portal. Con portador, el principal
        // viene del JWT, donde el correo viaja en `email` y el nombre en `name` —no en el claim
        // `ClaimTypes.Name` que estampa la cookie—, así que sin estos respaldos la sesión de un
        // satélite se respondía con usuario y correo vacíos.
        var email = httpContext.User.FindFirstValue(ClaimTypes.Name)
                 ?? httpContext.User.FindFirstValue(ClaimTypes.Email)
                 ?? httpContext.User.FindFirstValue("email");
        var tenantId = httpContext.User.FindFirstValue("tenant_id");
        var tenantCode = httpContext.User.FindFirstValue("tenant_code");
        var tenantName = httpContext.User.FindFirstValue("tenant_name");
        var role = httpContext.User.FindFirstValue(ClaimTypes.Role);
        var roleName = httpContext.User.FindFirstValue("role_name");
        var profileId = httpContext.User.FindFirstValue("profile_id");
        var sessionTrackingId = httpContext.User.FindFirstValue("session_tracking_id") ?? httpContext.TraceIdentifier;
        var isInternalAdmin = httpContext.User.FindFirstValue("is_internal_admin")?.ToLower() == "true";

        return Results.Ok(new LoginSuccessResponse(
            SessionId: httpContext.TraceIdentifier,
            SessionTrackingId: sessionTrackingId,
            UserId: userId ?? "",
            Username: httpContext.User.FindFirstValue("username")
                   ?? httpContext.User.FindFirstValue("name")
                   ?? email ?? "",
            Email: email ?? "",
            TenantId: tenantId ?? "",
            TenantCode: tenantCode ?? "",
            TenantName: tenantName ?? "",
            Role: role,
            RoleName: roleName,
            ProfileId: profileId,
            Permissions: Array.Empty<string>(),
            Language: "en",
            Token: null,
            TokenType: null,
            ExpiresIn: null,
            RefreshExpiresIn: null,
            IsInternalAdmin: isInternalAdmin
        ));
    }

    /// <summary>
    /// Cambia el perfil vigente y devuelve la MISMA forma que el login, para que el cliente
    /// reutilice tal cual su código de inicialización: grafo nuevo, token nuevo y cookie reescrita.
    ///
    /// Nota deliberada sobre el token anterior: NO se revoca. `ITokenRevocationStore` revoca por
    /// usuario y ventana de tiempo, no por token, así que revocar aquí dejaría al usuario fuera de
    /// la aplicación inmediatamente después de cambiarse de perfil —incluido el token que se acaba
    /// de emitir—. Y no hay escalada: el usuario poseía legítimamente ambos perfiles, de modo que
    /// mantener el token viejo vivo hasta que expire equivale a dos sesiones con dos sombreros.
    /// Un solo perfil activo a la vez exigiría revocación por `jti`, que hoy no existe.
    /// </summary>
    private static async Task<IResult> HandleSwitchProfileAsync(
        SwitchProfileRequest request,
        IMediator mediator,
        IJwtTokenService jwtTokenService,
        Evolith.Ums.Application.Configuration.Services.IConfigurationProvider configProvider,
        Services.MaterialDeFirma material,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var principal = LeerTokenDeGrafo(httpContext, material);
        if (principal is null) return Results.Unauthorized();

        var (userId, tenantId, sessionId, sistemaPedido) = principal.Value;

        if (!Guid.TryParse(request.ProfileId, out var profileId))
        {
            return Results.BadRequest(new LoginErrorResponse(
                ErrorCodes.ValidationError, "Identificador de perfil inválido.", SupportReferenceId: null));
        }

        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        // G-297: el sistema pedido se arrastra desde el portador que acaba de presentarse. Cambiar
        // de sombrero no es cambiar de producto: si el ingreso acotó la sesión a un sistema, el
        // grafo resultante debe seguir acotado, o el cambio de perfil devolvería en su bloque
        // `profiles` el inventario de los demás sistemas que el ingreso había ocultado
        // (ADR-0156 §2.5). Null —el ingreso no pidió sistema— deja el comportamiento de siempre.
        var result = await mediator.Send(
            new SwitchProfileCommand(profileId, userId, tenantId, clientIp, sistemaPedido), cancellationToken);

        if (result.IsFailure)
        {
            var codigo = result.Error switch
            {
                var e when e.StartsWith("AUTH_020") => StatusCodes.Status404NotFound,
                var e when e.StartsWith("AUTH_021") => StatusCodes.Status409Conflict,
                var e when e.StartsWith("AUTH_005") => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status400BadRequest,
            };

            return Results.Json(new LoginErrorResponse(
                ErrorCodes.AccessDenied,
                codigo == StatusCodes.Status404NotFound
                    ? "El perfil solicitado no existe o no le pertenece."
                    : "No se pudo cambiar de perfil.",
                SupportReferenceId: null), statusCode: codigo);
        }

        var graph = result.Value.Graph;
        var ctx   = graph.Context;

        // G-295: la MISMA sesión del portador que pidió el cambio. Aquí no se pasaba nada, así que
        // cambiar de perfil emitía un portador atado a una sesión que nunca existió: el logout
        // revocaba el `sid` de la cookie y este seguía vivo hasta caducar.
        var token = jwtTokenService.GenerateGraphToken(graph, sessionId);

        // La cookie se reescribe con los claims del perfil NUEVO: si no, el rol de la cookie y el
        // del token discreparían y la autorización dependería de cuál mirase cada middleware.
        await ReescribirCookieDeSesionAsync(httpContext, ctx, graph, sessionId);

        var cfg = configProvider.ForTenant(ctx.Tenant.Id);
        var permissions = GraphNavigation.AllowedPairs(graph)
            .Select(p => $"{p.Code}:{p.ActionCode}")
            .ToArray();

        return Results.Ok(new LoginSuccessResponse(
            SessionId:         httpContext.TraceIdentifier,
            SessionTrackingId: $"{httpContext.TraceIdentifier}-session",
            UserId:            ctx.User.Id.ToString(),
            Username:          ctx.User.Username,
            Email:             ctx.User.Email,
            TenantId:          ctx.Tenant.Id.ToString(),
            TenantCode:        ctx.Tenant.Code,
            TenantName:        ctx.Tenant.Name,
            Role:              ctx.Role?.Code,
            RoleName:          ctx.Role?.Name,
            ProfileId:         ctx.Profile?.Id.ToString(),
            Permissions:       permissions,
            Language:          cfg.DefaultLanguage,
            Token:             token,
            TokenType:         "Bearer",
            ExpiresIn:         result.Value.ExpiresIn,
            RefreshExpiresIn:  null,   // el refresh token vigente no se rota: la sesión es la misma
            IsInternalAdmin:   ctx.Tenant.IsManagementOwner,
            SessionParameters: null,
            AuthorizationGraph: AuthGraphPayload.Build(graph),
            GraphFormat:       "JSON",
            RefreshToken:      null));
    }

    /// <summary>
    /// Valida el token de grafo del encabezado y devuelve (usuario, inquilino). Mismo criterio que
    /// `HandleSwitchTenantAsync`: se valida aquí y no con `RequireAuthorization` para no depender
    /// de que el esquema JWT esté configurado en el entorno.
    /// </summary>
    /// <summary>
    /// Parámetros de validación de los manejadores que verifican el portador POR SU CUENTA
    /// (<c>switch-profile</c> y <c>switch-tenant</c>, que no llevan <c>RequireAuthorization()</c>).
    ///
    /// <para>POR QUÉ EXISTE. Estos dos manejadores construían su validación con SOLO la clave
    /// simétrica de <c>Jwt:Secret</c>, que era correcto mientras UMS firmaba en HS256. Desde
    /// ADR-0157 firma en RS256 y quedaron sin actualizar: verificaban una firma RSA contra una
    /// clave HMAC, fallaban siempre y devolvían 401. Medido el 2026-08-15 sobre el despliegue de
    /// UAT — el mismo portador daba 200 en <c>/auth/session</c> y 401 aquí.</para>
    ///
    /// <para>Se acepta SOLO la clave RSA y SOLO RS256, igual que el esquema portador (ver
    /// <c>AuthenticationExtensions</c>). La rama simétrica desapareció al cerrarse la etapa E3
    /// de ADR-0157: no quedaban portadores HS256 vivos que proteger. La lista blanca de un
    /// único algoritmo es lo que impide la confusión de <c>alg</c> de RFC 8725 §2.1 — sin ella,
    /// un token que declarase HS256 podría intentar verificarse contra la clave RSA
    /// <b>pública</b>, que es material publicado.</para>
    /// </summary>
    private static TokenValidationParameters ParametrosDeValidacionDelPortador(
        Services.MaterialDeFirma material)
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = [material.ClavePublica],
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),
        };
    }

    /// <summary>
    /// Lee el portador del encabezado y devuelve lo que la SESIÓN ya sabía.
    ///
    /// <para>G-295/G-297: devuelve también <c>sid</c> y <c>req_sys</c>. Devolvía solo el par
    /// (usuario, inquilino) y por eso el cambio de perfil no tenía de dónde sacar la sesión: emitía
    /// un portador con un `sid` inventado que el logout ya no alcanzaba. Los dos claims viajan en el
    /// mismo token que se acaba de VALIDAR, así que no son datos del llamante — son datos firmados
    /// por nosotros.</para>
    /// </summary>
    private static (Guid UserId, Guid TenantId, string? SessionId, string? SistemaPedido)? LeerTokenDeGrafo(
        HttpContext httpContext,
        Services.MaterialDeFirma material)
    {
        var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var tokenString = authHeader["Bearer ".Length..].Trim();

        var handler = new JwtSecurityTokenHandler();
        try
        {
            handler.ValidateToken(tokenString, ParametrosDeValidacionDelPortador(material), out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }

        var jwt = handler.ReadJwtToken(tokenString);
        var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        var ten = jwt.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;
        var sid = jwt.Claims.FirstOrDefault(c => c.Type == "sid")?.Value;
        var req = jwt.Claims.FirstOrDefault(c => c.Type == "req_sys")?.Value;

        return Guid.TryParse(sub, out var userId) && Guid.TryParse(ten, out var tenantId)
            ? (userId, tenantId, sid, req)
            : null;
    }

    /// <summary>
    /// Reescribe la cookie de sesión con el grafo nuevo, CONSERVANDO lo que la sesión ya era.
    ///
    /// <para>G-295: la reescritura es una renovación, no un ingreso. Perdía tres cosas que el login
    /// sí ponía —la sesión (<c>sid</c>), el sistema pedido (<c>req_sys</c>) y los scopes—, así que
    /// después de cambiar de perfil el portal quedaba con una cookie que ya no se podía cerrar y que
    /// no autorizaba nada. Lo que cambia al cambiar de perfil es el sombrero: identidad, sesión y
    /// producto siguen siendo los mismos.</para>
    /// </summary>
    private static async Task ReescribirCookieDeSesionAsync(
        HttpContext httpContext,
        GraphContext ctx,
        AuthorizationGraph graph,
        string? sessionId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, ctx.User.Id.ToString()),
            new(ClaimTypes.Name,           ctx.User.Email),
            new("tenant_id",               ctx.Tenant.Id.ToString()),
            new("tenant_code",             ctx.Tenant.Code),
            new("username",                ctx.User.Username),
            new("auth_method",             graph.Authentication.Method),
            new("is_internal_admin",       ctx.Tenant.IsManagementOwner ? "true" : "false"),
        };

        // La sesión sobrevive al cambio de perfil, o el logout deja de alcanzar a esta cookie
        // (G-247/G-295). Vacía solo si el portador venía de antes de que el `sid` existiera.
        if (!string.IsNullOrWhiteSpace(sessionId)) claims.Add(new Claim("sid", sessionId));

        if (ctx.Role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, ctx.Role.Code));
            claims.Add(new Claim("role_name", ctx.Role.Name));
        }
        if (ctx.Profile is not null) claims.Add(new Claim("profile_id", ctx.Profile.Id.ToString()));
        if (ctx.SystemSuite is not null) claims.Add(new Claim("sys_suite", ctx.SystemSuite.Code));
        // El grafo se construyó con el sistema que arrastraba el portador, así que su eco es el
        // mismo que traía la cookie anterior (G-297).
        if (ctx.RequestedSystem is not null) claims.Add(new Claim("req_sys", ctx.RequestedSystem.Code));
        if (ctx.Branch is not null) claims.Add(new Claim("branch_id", ctx.Branch.Id.ToString()));

        // Los scopes del perfil NUEVO, en un solo claim separado por espacios, igual que el login
        // (G-280). Sin ellos la cookie identifica sin autorizar: `UserContext.HasPermission` lee de
        // aquí, así que el portal se quedaba sin permisos justo después de cambiar de perfil.
        if (graph.Scopes.Count > 0)
            claims.Add(new Claim("scope", string.Join(' ', graph.Scopes)));

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = false });
    }

    private static async Task<IResult> HandleSwitchTenantAsync(
        SwitchTenantRequest request,
        ITenantContext tenantContext,
        ITenantRepository tenantRepository,
        Services.MaterialDeFirma material,
        IUserAccountRepository userAccounts,
        Evolith.Ums.Domain.Authorization.Graph.IAuthorizationGraphBuilder graphBuilder,
        Services.IJwtTokenService jwtTokenService,
        IAuthAuditService auditService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Extract JWT token from Authorization header and validate it directly
        // This works regardless of whether ASP.NET Core JWT authentication is configured
        var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Unauthorized();
        }

        var tokenString = authHeader.Substring("Bearer ".Length).Trim();

        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            tokenHandler.ValidateToken(
                tokenString, ParametrosDeValidacionDelPortador(material), out _);
        }
        catch (SecurityTokenException)
        {
            return Results.Unauthorized();
        }

        var jwtToken = tokenHandler.ReadJwtToken(tokenString);
        var isInternalAdmin = jwtToken.Claims.FirstOrDefault(c => c.Type == "is_internal_admin")?.Value?.ToLower() == "true";

        // Initialize TenantContext from JWT claims (this is normally done by authentication middleware)
        var tenantIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;
        if (Guid.TryParse(tenantIdClaim, out var userTenantId))
        {
            tenantContext.Initialize(userTenantId, isInternalAdmin);
        }
        else
        {
            return Results.Unauthorized();
        }

        if (!isInternalAdmin)
        {
            return Results.Json(new LoginErrorResponse(
                ErrorCodes.AccessDenied,
                "Only internal administrators can switch tenant context.",
                SupportReferenceId: null), statusCode: 403);
        }

        if (!Guid.TryParse(request.TenantId, out var tenantId))
        {
            return Results.BadRequest(new LoginErrorResponse(
                ErrorCodes.ValidationError,
                "Invalid tenant ID format.",
                SupportReferenceId: null));
        }

        var tenantResult = await tenantRepository.GetByIdAsync(tenantId, default);
        if (tenantResult is null)
        {
            return Results.NotFound(new LoginErrorResponse(
                ErrorCodes.TenantNotFound,
                $"Tenant with ID '{request.TenantId}' was not found.",
                SupportReferenceId: null));
        }

        var tenant = tenantResult;

        if (tenant.Props.Status != Domain.Enums.TenantStatus.Active)
        {
            return Results.BadRequest(new LoginErrorResponse(
                ErrorCodes.TenantInactive,
                $"Tenant '{tenant.Props.Code.GetValue()}' is not active.",
                SupportReferenceId: null));
        }

        try
        {
            if (request.EnableCrossTenantAccess)
            {
                tenantContext.EnableCrossTenantAccess();
            }
            else
            {
                tenantContext.SetOrganizationId(tenantId);
            }
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(new LoginErrorResponse(
                ErrorCodes.AccessDenied,
                ex.Message,
                SupportReferenceId: null), statusCode: 403);
        }

        var sessionTrackingId = jwtToken.Claims.FirstOrDefault(c => c.Type == "session_tracking_id")?.Value ?? httpContext.TraceIdentifier;

        // G-295: la SESIÓN, que no es el correlador. Aquí se le pasaba `sessionTrackingId` al emisor
        // del portador, y eso no era un descuido aleatorio sino un identificador equivocado: el
        // `session_tracking_id` se acuña nuevo en CADA token y solo sirve para hilar trazas, así que
        // el portador del inquilino destino salía con un `sid` que ningún logout conoce —y que, si
        // por casualidad coincidiera con el de otra sesión, revocaría la que no toca—. Son dos
        // identificadores con vidas distintas y el código los cruzaba.
        var sesionDelPortador = jwtToken.Claims.FirstOrDefault(c => c.Type == "sid")?.Value;

        // ADR-UMS-105 D1: el cambio se materializa en un PORTADOR NUEVO. Antes esto terminaba aquí
        // mutando el contexto de la petición, y la llamada siguiente llegaba con el token de antes:
        // el endpoint describía un cambio que el sistema no recordaba.
        //
        // D3: el grafo se construye en el inquilino de ORIGEN. El administrador no tiene perfil en
        // el destino —ni debe tenerlo— así que lo que cambia es sobre qué datos actúa, no qué puede
        // hacer. Fabricarle un perfil ahí sería inventarle una identidad que nadie le concedió.
        string? portador = null;
        var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                    ?? jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(subClaim, out var idAdministrador) && Guid.TryParse(tenantIdClaim, out var idOrigen))
        {
            var usuario = await userAccounts.GetByIdAsync(idAdministrador, cancellationToken);
            if (usuario is not null)
            {
                var grafo = await graphBuilder.BuildAsync(
                    usuario, idOrigen, Evolith.Ums.Domain.Identity.Auth.AuthMethod.Local(), null, cancellationToken);
                if (grafo.IsSuccess)
                {
                    portador = jwtTokenService.GenerateGraphToken(grafo.Value, sesionDelPortador, tenantId);
                }
            }

            // D4: el cambio se audita nombrando los DOS inquilinos. Sin este registro, una
            // intervención cross-tenant es indistinguible de la operación normal del inquilino, y
            // `INV-TS03` —«auditado»— no es verificable.
            await auditService.RecordAuthEventAsync(new AuthAuditEvent(
                UserId:     idAdministrador,
                TenantId:   idOrigen,
                TenantCode: tenant.Props.Code.GetValue(),
                AuthMethod: "Local",
                EventType:  "Auth.TenantSwitch",
                Succeeded:  portador is not null,
                ClientIp:   httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                FailureReason: portador is null ? $"destino={tenantId}; no se pudo emitir portador" : $"destino={tenantId}"),
                cancellationToken);
        }

        return Results.Ok(new TenantSwitchResponse(
            PreviousTenantId: tenantContext.OriginalTenantId?.ToString() ?? "",
            CurrentTenantId: tenantId.ToString(),
            CurrentTenantCode: tenant.Props.Code.GetValue(),
            CurrentTenantName: tenant.Props.Name.GetValue(),
            CrossTenantAccessEnabled: request.EnableCrossTenantAccess,
            SessionTrackingId: sessionTrackingId,
            Token: portador
        ));
    }
}

// ─── Request/Response Models ───────────────────────────────────────────────────

public record LoginRequest(
    string TenantCode,
    string Username,
    string Password,
    bool RememberMe = false,
    // Codigo del sistema cuyo grafo se pide, OPCIONAL (G-184, ADR-0156 §3.2). Presente, el grafo se
    // acota a los perfiles del usuario en ese sistema; ausente, se conserva el desempate de siempre,
    // que es lo que necesita un portal multiproducto.
    //
    // Existe porque sin el nadie podia preguntar: el portal de UMS resolvia por desempate y caia en
    // la suite de un satelite —`SIL` para `admin@evolith.example`—, de modo que el shell, que se
    // construye del grafo, pintaba menus de otro producto o ninguno. El endpoint de maquina
    // (`/client/authenticate`) ya lo admitia desde G-204; el del portal no, y esa asimetria no tenia
    // justificacion: es la misma pregunta, hecha por el mismo motivo.
    string? SystemCode = null);

/// <summary>
/// Effective configuration values for the authenticated tenant, resolved at login time
/// from the in-memory parameter cache (loaded at startup by ConfigurationLoaderHostedService).
/// The frontend uses these values for the entire session without additional API calls.
/// TD-003: When Redis is adopted, the underlying cache swaps transparently.
/// </summary>
public record SessionParameters(
    int    SessionTimeoutMinutes,
    int    AccessTokenDurationMs,
    int    RefreshTokenDurationMs,
    int    MaxLoginAttempts,
    int    MinPasswordLength,
    bool   MfaRequiredForAdmin,
    string[] MfaAllowedMethods,
    bool   CustomBrandingEnabled,
    string DefaultLanguage,
    string DefaultTimezone);

public record LoginSuccessResponse(
    string SessionId,
    string SessionTrackingId,
    string UserId,
    string Username,
    string Email,
    string TenantId,
    string TenantCode,
    string TenantName,
    string? Role,
    string? RoleName,
    string? ProfileId,
    string[] Permissions,
    string Language,
    string? Token,
    string? TokenType,
    int? ExpiresIn,
    int? RefreshExpiresIn,
    bool IsInternalAdmin = false,
    SessionParameters? SessionParameters = null,
    // ── Graph fields (null when called from refresh/session endpoints) ──────
    IReadOnlyDictionary<string, object?>? AuthorizationGraph = null,
    string? GraphFormat = null,
    // Refresh token en claro — solo cuando el inquilino activa la capacidad (ADR-UMS-091/FR-015);
    // null en caso contrario. Se devuelve una única vez y nunca se persiste en claro.
    string? RefreshToken = null);

public record LoginErrorResponse(
    string Code,
    string Message,
    string? SupportReferenceId);

// ── Renovación por refresh token opaco (ADR-UMS-091 / FR-015) ──────────────────────
public record RefreshTokenRequest(string RefreshToken);

public record RefreshTokenGrantResponse(
    string Token,
    string TokenType,
    int ExpiresIn,
    // Nuevo refresh en claro tras la rotación; null si la política no rota (el cliente
    // conserva el suyo). Se devuelve una única vez y nunca se persiste en claro.
    string? RefreshToken,
    int RefreshExpiresIn,
    string[] Permissions,
    // Grafo recién regenerado (completo) para que la UI actualice su modelo de permisos.
    IReadOnlyDictionary<string, object?>? AuthorizationGraph = null,
    string? GraphFormat = null);

public record RefreshTokenResponse(
    string Token,
    string TokenType,
    int ExpiresIn,
    int RefreshExpiresIn,
    string SessionTrackingId,
    // ── D-019 / ADR-UMS-091: el refresh deslizante ahora espeja el login y regenera el grafo,
    //     así que el token/respuesta portan el modelo de permisos VIGENTE. Campos aditivos y
    //     opcionales: el front actual (auth.store.ts::refreshSession) los ignora sin romperse. ──
    string[]? Permissions = null,
    string? Language = null,
    IReadOnlyDictionary<string, object?>? AuthorizationGraph = null,
    string? GraphFormat = null);

public record SwitchProfileRequest(string ProfileId);

public record SwitchTenantRequest(
    string TenantId,
    bool EnableCrossTenantAccess = false);

public record TenantSwitchResponse(
    string PreviousTenantId,
    string CurrentTenantId,
    string CurrentTenantCode,
    string CurrentTenantName,
    bool CrossTenantAccessEnabled,
    string SessionTrackingId,
    // ADR-UMS-105 D1: sin portador nuevo no hay cambio. Nulo solo si no se pudo construir el grafo
    // del administrador, y en ese caso la auditoria lo registra como intento fallido.
    string? Token = null);

// ─── Error Codes ───────────────────────────────────────────────────────────────

public static class ErrorCodes
{
    public const string ValidationError = "AUTH_001";
    public const string TenantNotFound = "AUTH_002";
    public const string TenantInactive = "AUTH_003";
    public const string UserNotFound = "AUTH_004";
    public const string UserNotActive = "AUTH_005";
    public const string InvalidCredentials = "AUTH_006";
    public const string SessionExpired = "AUTH_007";
    public const string AccessDenied = "AUTH_008";
    public const string AdminLacksPermission = "AUTH_009";
    public const string TargetUserOutsideScope = "AUTH_010";
    public const string MfaEnrollmentRequired = "AUTH_011";
    // NOTA: AUTH_012 está reservado para «no IDP adapter registered» (UmsErrorCodes.NoIdpAdapterRegistered).
    // ADR-UMS-095: cuenta bloqueada temporalmente por intentos fallidos (HTTP 423 Locked) usa AUTH_017.
    public const string AccountLocked = "AUTH_017";
    // G-188: único desenlace de error del canje de restablecimiento. Deliberadamente indistinguible
    // entre token inexistente, vencido, ya usado y cuenta inhabilitada.
    public const string InvalidResetToken = "AUTH_019";
}

public static class UserAccountErrorCodes
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security", "S2068:Hard-coded credentials are security-sensitive",
        Justification = "Falso positivo: es un CÓDIGO DE ERROR ('USER_015'), no una credencial. El analizador " +
                        "casa por el término 'Password' en el nombre del símbolo.")]
    public const string FederatedUserPasswordReset = "USER_015";
}

public static class ConfigurationErrorCodes
{
    public const string ValidityPeriodExceedsMaximum = "CONFIG_003";
}
