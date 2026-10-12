namespace Evolith.Ums.Presentation.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration.AppConfiguration;

public interface IJwtTokenService
{
    string GenerateToken(TokenGenerationRequest request);

    /// <summary>
    /// Generates a JWT token that embeds the full AuthorizationGraph as claims.
    /// Used by /client/authenticate — the token IS the auth graph for the client system.
    /// </summary>
    /// <param name="graph">Grafo de autorización que el token transporta.</param>
    /// <param name="sessionId">
    /// Sesión a la que pertenece el token (G-247). La comparte con la cookie emitida en el mismo
    /// login, para que cerrar sesión cierre el dispositivo hable por donde hable.
    ///
    /// <para>G-295: OBLIGATORIO, sin valor por omisión. Lo era —`sessionId = null`— y de los cinco
    /// emisores de portador solo el login lo pasaba: los otros cuatro acuñaban una sesión inventada
    /// y el compilador callaba. La ausencia de sesión sigue siendo expresable —`null`, para un
    /// portador anterior a que el `sid` existiera—, pero ahora hay que escribirla.</para>
    /// </param>
    /// <param name="inquilinoDestino">
    /// Inquilino SOBRE EL QUE se actúa cuando un administrador interno cambia de contexto
    /// (ADR-UMS-105 D1/D2). Presente, el token viaja con ese `tenant_id` y conserva el de origen en
    /// `orig_tenant_id`; ausente, el token es el de siempre. Los scopes NO cambian: son los del
    /// grafo del administrador en su propio inquilino (D3).
    /// </param>
    string GenerateGraphToken(
        Evolith.Ums.Domain.Authorization.Graph.AuthorizationGraph graph,
        string? sessionId,
        Guid? inquilinoDestino = null);

    /// <summary>
    /// Generates a semantic-only JWT for external client systems.
    /// The token avoids GUIDs and uses business-facing codes/values instead.
    /// </summary>
    string GenerateSemanticGraphToken(Evolith.Ums.Domain.Authorization.Graph.AuthorizationGraph graph);

    string GenerateRefreshToken();
    DateTime GetTokenExpiration(string token);
}

public record TokenGenerationRequest(
    string UserId,
    string Email,
    string Username,
    Guid TenantId,
    string TenantCode,
    string? Role,
    string? RoleName,
    string? ProfileId,
    string[] Permissions,
    string Language = "en",
    bool IsInternalAdmin = false,
    /// <summary>
    /// Sesión a la que pertenece este token (G-247). Es la MISMA que lleva la cookie emitida en el
    /// mismo login: cerrar sesión cierra el dispositivo, y un dispositivo puede hablar por cookie o
    /// por portador. Si va vacío, el token no es revocable por sesión — se genera uno para no
    /// dejarlo sin identidad, pero entonces nadie más conoce ese identificador.
    /// </summary>
    string? SessionId = null);

public class JwtTokenService : IJwtTokenService
{
    /// <summary>
    /// El material de firma, único para los tres tokens que UMS emite.
    /// </summary>
    /// <remarks>
    /// Antes cada método construía su propia <c>SymmetricSecurityKey</c> a partir de
    /// <c>Jwt:Secret</c>. Tres construcciones idénticas son tres sitios donde el
    /// algoritmo puede divergir sin que nada falle, así que ahora hay uno solo y es
    /// RS256 (ADR-0157).
    /// </remarks>
    private readonly MaterialDeFirma _firma;

    private readonly string _issuer;
    private readonly string _audience;
    private readonly IConfigurationProvider _configProvider;

    public JwtTokenService(IConfiguration configuration, IConfigurationProvider configProvider, MaterialDeFirma firma)
    {
        _firma = firma ?? throw new ArgumentNullException(nameof(firma));
        _issuer = configuration["Jwt:Issuer"] ?? "ums-api";
        _audience = configuration["Jwt:Audience"] ?? "ums-web-app";
        _configProvider = configProvider;
    }

    public string GenerateToken(TokenGenerationRequest request)
    {
        var credentials = _firma.Credenciales;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId),
            new(JwtRegisteredClaimNames.Email, request.Email),
            new(JwtRegisteredClaimNames.Name, request.Username),
            new("tenant_id", request.TenantId.ToString()),
            new("tenant_code", request.TenantCode),
            new("session_tracking_id", Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // G-247: la sesión, compartida con la cookie del mismo login. `jti` no sirve para esto:
            // es distinto en cada token, así que revocarlo no alcanzaría al que emita el refresh.
            new("sid", string.IsNullOrWhiteSpace(request.SessionId)
                ? Guid.NewGuid().ToString("N")
                : request.SessionId),
        };

        if (!string.IsNullOrEmpty(request.Role))
        {
            claims.Add(new Claim(ClaimTypes.Role, request.Role));
        }

        if (!string.IsNullOrEmpty(request.RoleName))
        {
            claims.Add(new Claim("role_name", request.RoleName));
        }

        if (!string.IsNullOrEmpty(request.ProfileId))
        {
            claims.Add(new Claim("profile_id", request.ProfileId));
        }

        foreach (var permission in request.Permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        claims.Add(new Claim("language", request.Language));
        claims.Add(new Claim("is_internal_admin", request.IsInternalAdmin ? "true" : "false"));

        var accessTokenDurationMs = _configProvider.GetValueAs<int>(
            AppConfigurationCodes.AccessTokenDurationMs,
            request.TenantId,
            AppConfigurationDefaults.AccessTokenDurationMs);

        var expires = DateTime.UtcNow.AddMilliseconds(accessTokenDurationMs);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateGraphToken(
        Evolith.Ums.Domain.Authorization.Graph.AuthorizationGraph graph,
        string? sessionId,
        Guid? inquilinoDestino = null)
    {
        var credentials = _firma.Credenciales;

        var ctx    = graph.Context;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   ctx.User.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, ctx.User.Email),
            new(JwtRegisteredClaimNames.Name,  ctx.User.Username),
            // ADR-UMS-105 D1/D2: al cambiar de inquilino, el portador declara el DESTINO y conserva
            // el origen. Un endpoint que solo mutara el contexto de la peticion prometia un cambio
            // que la siguiente llamada no recordaba.
            new("tenant_id",                   (inquilinoDestino ?? ctx.Tenant.Id).ToString()),
            new("tenant_code",                 ctx.Tenant.Code),
            // El privilegio de operador (internal-admin) se deriva de IsManagementOwner
            // (igual que en AuthEndpoints). Debe viajar en el token Bearer de grafo — no solo
            // en el principal de cookie — para que switch-tenant y la gestión on-behalf del
            // operador sean alcanzables vía API (evolith_arch#18). No aplica al token semántico
            // de clientes externos (GenerateSemanticGraphToken), que nunca es internal-admin.
            new("is_internal_admin",           ctx.Tenant.IsManagementOwner ? "true" : "false"),
            new("auth_method",                 graph.Authentication.Method),
            new("graph_generated_at",          graph.GeneratedAt.ToString("O")),
            new("graph_valid_until",           graph.ValidUntil.ToString("O")),
            new("session_tracking_id",         Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            // G-247: la misma sesión que la cookie del login. `jti` cambia en cada token, así que
            // revocarlo no alcanzaría al que emita el refresh.
            new("sid", string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId),
        };

        // G-043: en el grafo lobby (onboarding pendiente) SystemSuite/Role/Profile son null; sus claims
        // se omiten (el token lobby no otorga rol/suite/perfil, coherente con "sin perfil aún").
        if (inquilinoDestino is not null && inquilinoDestino != ctx.Tenant.Id)
        {
            claims.Add(new Claim("orig_tenant_id", ctx.Tenant.Id.ToString()));
        }

        if (ctx.SystemSuite is not null)
            claims.Add(new Claim("sys_suite", ctx.SystemSuite.Code));

        // G-297: el sistema que el cliente PIDIÓ, que no es el mismo hecho que `sys_suite` —el que
        // se resolvió—. Sin distinguirlos no hay forma de renovar bien: propagar el resuelto acota
        // una sesión que nadie acotó, y no propagar nada devuelve al sistema del desempate.
        //
        // Solo aparece cuando hubo petición explícita, así que su AUSENCIA también dice algo: «esta
        // sesión es multiproducto». Lo lee el refresco por cookie/portador para reconstruir el grafo
        // como lo pidió el ingreso; el canje del token opaco no puede leerlo —es anónimo— y lo
        // recupera de la familia de refresco.
        if (ctx.RequestedSystem is not null)
            claims.Add(new Claim("req_sys", ctx.RequestedSystem.Code));

        if (ctx.Role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, ctx.Role.Code));
            claims.Add(new Claim("role_name", ctx.Role.Name));
        }
        if (ctx.Profile is not null)
            claims.Add(new Claim("profile_id", ctx.Profile.Id.ToString()));

        if (ctx.Branch is not null)
            claims.Add(new Claim("branch_id", ctx.Branch.Id.ToString()));

        if (graph.Authentication.Provider is not null)
            claims.Add(new Claim("idp_provider", graph.Authentication.Provider.Name));

        // G-172: aquí se emitía un claim `perm` por CADA par opción-acción y un `domain_perm` por
        // CADA par recurso-acción, SIN filtrar el efecto: sobre datos reales, 84 + 286 claims que
        // hinchaban el token a 16-24 KB para UN SOLO sistema, enviados en la cabecera
        // `Authorization` de cada petición. El grafo ya viaja en el cuerpo y el cliente lo cachea
        // toda la sesión: el token no necesita repetirlo.
        //
        // Los `scope` SÍ se quedan: la autorización del servidor los lee (`UserContext.HasPermission`,
        // que alimenta al aspecto AOP). Quitarlos denegaría todo, fail-closed. Moverlos a una
        // resolución por sesión es una mejora aparte, no un efecto colateral de este cambio.

        foreach (var scope in graph.Scopes)
            claims.Add(new Claim("scope", scope));

        foreach (var flag in graph.FeatureFlags.Where(f => f.IsEnabled))
            claims.Add(new Claim("feature", flag.FlagCode));

        var token = new JwtSecurityToken(
            issuer:            _issuer,
            audience:          _audience,
            claims:            claims,
            expires:           graph.ValidUntil,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateSemanticGraphToken(Evolith.Ums.Domain.Authorization.Graph.AuthorizationGraph graph)
    {
        var credentials = _firma.Credenciales;

        var ctx = graph.Context;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, ctx.User.Username),
            new(JwtRegisteredClaimNames.Email, ctx.User.Email),
            new(JwtRegisteredClaimNames.Name, ctx.User.DisplayName),
            new("tenant_code", ctx.Tenant.Code),
            new("tenant_name", ctx.Tenant.Name),
            new("auth_method", graph.Authentication.Method),
            new("graph_generated_at", graph.GeneratedAt.ToString("O")),
            new("graph_valid_until", graph.ValidUntil.ToString("O")),
            new("session_tracking_id", GenerateOpaqueId()),
            new(JwtRegisteredClaimNames.Jti, GenerateOpaqueId()),
        };

        // G-043: grafo lobby → SystemSuite/Role/Profile null; se omiten sus claims.
        if (ctx.SystemSuite is not null)
        {
            claims.Add(new Claim("sys_suite", ctx.SystemSuite.Code));
            claims.Add(new Claim("sys_suite_name", ctx.SystemSuite.Name));
        }
        if (ctx.Role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, ctx.Role.Code));
            claims.Add(new Claim("role_name", ctx.Role.Name));
        }
        if (ctx.Profile is not null)
        {
            claims.Add(new Claim("profile_scope", ctx.Profile.Scope));

            // El PERFIL VIGENTE viaja en el token desde ADR-0156 §8. Sin él, el cambio de perfil
            // duraba lo que la respuesta: `GET /client/graph` reconstruía el grafo por desempate y
            // devolvía otra vez el perfil que el usuario acababa de abandonar. Medido: `pmo.sdlc@`
            // cambiaba a PMO —75 permisos— y al revalidar volvía a DIRECTORIO —7—, en el mismo
            // proceso. Una conmutación que no sobrevive a la siguiente petición no es una
            // conmutación.
            //
            // Este token evita identificadores internos a propósito, y por eso el `sub` es el
            // nombre de usuario. El del perfil es la excepción justificada: desde el contrato
            // 2.4.0 el propio grafo publica `profiles[].id` a ese mismo cliente, así que el claim
            // no le revela nada que no tenga ya. No se usa un selector semántico rol+sistema
            // porque no desambigua: dos perfiles pueden compartirlos y diferir solo en sucursal.
            claims.Add(new Claim("profile_id", ctx.Profile.Id.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(ctx.Branch?.Code))
            claims.Add(new Claim("branch_code", ctx.Branch.Code));

        if (graph.Authentication.Provider is not null)
        {
            claims.Add(new Claim("idp_provider", graph.Authentication.Provider.Code));
            claims.Add(new Claim("idp_strategy", graph.Authentication.Provider.Strategy));
        }

        // G-172: el token semántico tampoco replica la matriz de permisos. Los `scope` bastan
        // para autorizar y son la mitad del volumen; el grafo completo viaja en el cuerpo.
        foreach (var scope in graph.Scopes)
            claims.Add(new Claim("scope", scope));

        foreach (var flag in graph.FeatureFlags.Where(f => f.IsEnabled))
            claims.Add(new Claim("feature", flag.FlagCode));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: graph.ValidUntil,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public DateTime GetTokenExpiration(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        return jwtToken.ValidTo;
    }

    private static string GenerateOpaqueId()
    {
        var bytes = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncoder.Encode(bytes);
    }
}
