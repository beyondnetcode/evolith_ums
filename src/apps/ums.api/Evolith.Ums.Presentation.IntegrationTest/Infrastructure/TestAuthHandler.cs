using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Presentation.Middleware;

namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// El administrador SEMBRADO de EVOLITH: una cuenta que EXISTE, con perfil y plantilla publicada.
    ///
    /// <para>El actor por omision era `…0111`, un GUID que no corresponde a ninguna cuenta. Daba
    /// igual mientras el atajo de Admin Root concedia cualquier permiso por la sola claim
    /// `is_internal_admin`; con el atajo apagado ese usuario no tiene grafo, no tiene un solo scope
    /// y la peticion muere en el guardia — 163 de 352 pruebas, por su andamiaje y no por lo que
    /// miden (G-280).</para>
    /// </summary>
    public const string AdministradorSembrado = "5f4e3d14-1b0a-9f8e-7d6c-543210987654";

    private readonly ITenantContext _tenantContext;
    private readonly IUserAccountRepository _userAccounts;
    private readonly IAuthorizationGraphBuilder _graphBuilder;

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        ITenantContext tenantContext,
        IUserAccountRepository userAccounts,
        IAuthorizationGraphBuilder graphBuilder)
        : base(options, logger, encoder, clock)
    {
        _tenantContext = tenantContext;
        _userAccounts = userAccounts;
        _graphBuilder = graphBuilder;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var tenantId = "3fa85f64-5717-4562-b3fc-2c963f66afa6"; // Default system tenant
        if (Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader))
        {
            tenantId = tenantHeader.ToString();
        }

        // Override opcional del actor autenticado (NameIdentifier). Aditivo y retrocompatible:
        // por defecto se mantiene el actor histórico "…0111"; sólo las pruebas que necesitan
        // varios actores distintos (p. ej. la segregación de funciones del happy-path IGA, G-052)
        // envían este encabezado para conmutar la identidad por petición.
        var actorId = AdministradorSembrado;
        if (Request.Headers.TryGetValue("X-Test-Actor-Id", out var actorHeader)
            && Guid.TryParse(actorHeader.ToString(), out _))
        {
            actorId = actorHeader.ToString();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, actorId),
            new("tenant_id", tenantId),
            new("org_id", tenantId),
            new("is_internal_admin", "true"),
            new(ClaimTypes.Role, "SuperAdmin")
        };

        // Los MISMOS scopes que tendria en produccion, construidos del catalogo real (G-280). Este
        // esquema se registra como el de autenticacion por omision, asi que su principal SUSTITUYE
        // al que resolvio el middleware de desarrollo: sin esto, la identidad de las pruebas llegaba
        // al guardia con identidad y ni un permiso, y solo pasaba por el atajo de Admin Root.
        if (Guid.TryParse(tenantId, out var idInquilino))
        {
            try { _tenantContext.Initialize(idInquilino, true); } catch { /* ya inicializado */ }
        }

        await ScopesDelGrafo.AnadirAsync(
            claims, actorId, tenantId, esAdminInterno: true,
            _tenantContext, _userAccounts, _graphBuilder, Context.RequestAborted);

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return AuthenticateResult.Success(ticket);
    }
}
