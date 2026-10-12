namespace Evolith.Ums.Presentation.IntegrationTest.Identity.Auth.Oidc;

/// <summary>
/// G-109: colección que arranca UNA sola vez el fixture compuesto Postgres+Keycloak
/// (<see cref="PostgresKeycloakFixture"/>) y lo comparte entre los tests del E2E de
/// resolución/fallback FR-042 respaldado por BD.
/// </summary>
[CollectionDefinition("PostgresKeycloak")]
public sealed class PostgresKeycloakCollectionDefinition : ICollectionFixture<PostgresKeycloakFixture>
{
    // Clase marcador — xUnit cablea el fixture por el atributo de arriba.
}
