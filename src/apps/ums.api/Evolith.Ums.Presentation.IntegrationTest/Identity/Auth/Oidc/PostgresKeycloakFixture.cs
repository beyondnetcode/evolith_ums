using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity.Auth.Oidc;

/// <summary>
/// G-109 (residual de FR-042, ADR-UMS-097): fixture <b>compuesto</b> que orquesta a la vez los DOS
/// contenedores reales que exige el E2E de resolución/fallback FR-042 respaldado por BD:
/// <list type="bullet">
///   <item><b>Postgres</b> — donde se siembran y de donde se leen las <c>IdpConfiguration</c> reales
///     (el camino de producción del store <c>IdpConfigurationOidcProviderConfigStore</c>).</item>
///   <item><b>Keycloak</b> — el IdP OIDC real contra el que se autentica (Authorization Code + PKCE).</item>
/// </list>
///
/// <para><b>Reto xUnit v3 resuelto:</b> una clase de test pertenece a UNA sola colección, así que no
/// puede combinar las colecciones <c>PostgreSql</c> y <c>Keycloak</c> por separado. Aquí se elige la
/// opción de <i>fixture que compone ambos</i>: reutiliza tal cual los fixtures existentes
/// (<see cref="PostgreSqlContainerFixture"/> y <see cref="KeycloakContainerFixture"/>) sin tocarlos, y
/// se expone bajo un único <c>ICollectionFixture</c> (ver <c>PostgresKeycloakCollectionDefinition</c>).</para>
///
/// <para><b>Skip sin Docker en AMBOS:</b> el fixture de Keycloak ya traga el fallo de arranque; el de
/// Postgres, en cambio, relanza (semántica de su colección original). Este compuesto captura ese
/// relanzamiento para conservar la semántica de <c>Assert.Skip</c> también cuando falta Docker.</para>
/// </summary>
public sealed class PostgresKeycloakFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _postgres = new();

    /// <summary>Fixture del Keycloak real (endpoints, issuer y constantes del realm importado).</summary>
    public KeycloakContainerFixture Keycloak { get; } = new();

    /// <summary>Cadena de conexión al Postgres del contenedor (vacía si no arrancó).</summary>
    public string ConnectionString => _postgres.ConnectionString;

    public bool PostgresAvailable { get; private set; }

    public bool KeycloakAvailable => Keycloak.IsAvailable;

    /// <summary>Ambos contenedores listos: precondición para no saltar los tests del E2E.</summary>
    public bool BothAvailable => PostgresAvailable && KeycloakAvailable;

    /// <summary>Diagnóstico agregado del fallo de arranque (sin fingir verde, SD-05).</summary>
    public string StartupError =>
        $"Postgres={(PostgresAvailable ? "OK" : _postgresError ?? "no disponible")}; " +
        $"Keycloak={(KeycloakAvailable ? "OK" : Keycloak.StartupError ?? "no disponible")}";

    private string? _postgresError;

    public async ValueTask InitializeAsync()
    {
        // Dos contenedores son LENTOS: se arrancan en PARALELO para acotar el tiempo de la colección.
        var keycloakTask = Keycloak.InitializeAsync().AsTask();
        var postgresTask = StartPostgresAsync();
        await Task.WhenAll(keycloakTask, postgresTask);
    }

    private async Task StartPostgresAsync()
    {
        try
        {
            await _postgres.InitializeAsync();
            PostgresAvailable = _postgres.IsAvailable;
        }
        catch (Exception ex)
        {
            // El fixture de Postgres relanza ante Docker ausente; lo capturamos para saltar (no tumbar).
            _postgresError = ex.Message;
            PostgresAvailable = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Keycloak.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
