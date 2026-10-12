using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity.Auth.Oidc;

/// <summary>
/// G-109 (deuda de ADR-UMS-094): arnés Keycloak REAL sobre Testcontainers. Levanta un
/// Keycloak contenerizado (<c>start-dev --import-realm</c>) con el realm <c>evolith</c>
/// importado —cliente <c>ums-client</c> (PKCE S256 + Direct Access Grant) y usuario de
/// prueba con contraseña— para ejercer el adaptador OIDC federado contra un IdP real,
/// <b>sin mocks del token endpoint ni del JWKS</b> (lo que el ADR-UMS-094 prometió y el
/// slice 1 no construyó: sus tests usan JWKS/token mockeados con issuer ficticio
/// <c>idp.test</c>).
///
/// Calca el patrón desechable/reproducible de <c>PostgreSqlContainerFixture</c>: si
/// Docker no está disponible o la imagen no se puede traer, <see cref="IsAvailable"/>
/// queda en <c>false</c> y los tests hacen <c>Assert.Skip(...)</c> en vez de fallar en
/// crudo. El contenedor se comparte una vez por la colección <c>[Collection("Keycloak")]</c>.
/// </summary>
public sealed class KeycloakContainerFixture : IAsyncLifetime
{
    // Imagen estable y pineada de Keycloak (26.x). start-dev habilita HTTP y no exige TLS,
    // apropiado para un IdP de pruebas efímero.
    private const string KeycloakImage = "quay.io/keycloak/keycloak:26.3.2";
    private const string RealmName = "evolith";

    private IContainer? _container;

    /// <summary>Cliente OIDC público registrado en el realm importado.</summary>
    public const string ClientId = "ums-client";

    /// <summary>Redirect URI válida registrada en el cliente para el flujo authorization_code.</summary>
    public const string RedirectUri = "https://ums.test/callback";

    /// <summary>Usuario de prueba (con contraseña) sembrado por el realm importado.</summary>
    public const string TestUsername = "ana.ransa";
    public const string TestPassword = "Password123!";
    public const string TestEmail = "ana.ransa@ransa.pe";

    /// <summary>URL base del Keycloak del contenedor (host + puerto mapeado). Vacía si no arrancó.</summary>
    public string BaseAddress { get; private set; } = string.Empty;

    /// <summary>Issuer real del realm: <c>{BaseAddress}/realms/evolith</c>.</summary>
    public string RealmIssuer { get; private set; } = string.Empty;

    /// <summary>
    /// Endpoints OIDC reales derivados del issuer del contenedor (auth/token/jwks), tal
    /// como los consume el adaptador. Mismo esquema que Keycloak publica en su discovery.
    /// </summary>
    public OidcEndpoints Endpoints { get; private set; } =
        new(string.Empty, string.Empty, string.Empty, string.Empty);

    /// <summary>
    /// <c>true</c> cuando Keycloak arrancó y el realm quedó servido. Los tests deben
    /// llamar <c>Assert.Skip(!IsAvailable, "Docker/Keycloak requerido")</c>.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Diagnóstico del fallo de arranque (si lo hubo), para reportar sin fingir verde.</summary>
    public string? StartupError { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            var realmPath = Path.Combine(
                AppContext.BaseDirectory, "Identity", "Auth", "Oidc", "evolith-realm.json");
            var realmBytes = await File.ReadAllBytesAsync(realmPath);

            _container = new ContainerBuilder()
                .WithImage(KeycloakImage)
                // Bootstrap admin (KC 26+). No lo usa el test, pero evita advertencias de arranque.
                .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
                .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", "admin")
                .WithEnvironment("KC_HTTP_ENABLED", "true")
                .WithEnvironment("KC_HOSTNAME_STRICT", "false")
                // Inyecta el realm importable en el directorio que --import-realm lee al arrancar.
                // El modo va como `uint` desde Testcontainers 4.5: la sobrecarga que recibia
                // `UnixFileModes` desaparecio. Se conserva el enum para que siga leyendose que
                // son permisos 0444 y no una constante magica; el valor numerico es el mismo.
                .WithResourceMapping(
                    realmBytes,
                    "/opt/keycloak/data/import/evolith-realm.json",
                    (uint)(UnixFileModes.UserRead | UnixFileModes.GroupRead | UnixFileModes.OtherRead))
                .WithCommand("start-dev", "--import-realm")
                .WithPortBinding(8080, assignRandomHostPort: true)
                // Listo = el realm responde su discovery OIDC (el import ya ocurrió).
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request => request
                        .ForPort(8080)
                        .ForPath($"/realms/{RealmName}/.well-known/openid-configuration")
                        .ForStatusCode(HttpStatusCode.OK)))
                .Build();

            await _container.StartAsync();

            BaseAddress = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(8080)}";
            RealmIssuer = $"{BaseAddress}/realms/{RealmName}";
            Endpoints = new OidcEndpoints(
                AuthorizationEndpoint: $"{RealmIssuer}/protocol/openid-connect/auth",
                TokenEndpoint: $"{RealmIssuer}/protocol/openid-connect/token",
                JwksUri: $"{RealmIssuer}/protocol/openid-connect/certs",
                Issuer: RealmIssuer);

            IsAvailable = true;
        }
        catch (Exception ex)
        {
            // No relanzamos: Docker/Keycloak no disponibles ⇒ los tests se saltan con mensaje claro
            // (a diferencia de Postgres, este arnés es aditivo y no debe tumbar la corrida entera).
            StartupError = ex.ToString();
            IsAvailable = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
