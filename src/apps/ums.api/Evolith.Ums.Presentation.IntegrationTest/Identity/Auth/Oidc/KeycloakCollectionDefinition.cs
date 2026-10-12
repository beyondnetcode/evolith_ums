namespace Evolith.Ums.Presentation.IntegrationTest.Identity.Auth.Oidc;

/// <summary>
/// G-109: colección que arranca el Keycloak del arnés (<see cref="KeycloakContainerFixture"/>)
/// una sola vez y lo comparte entre los tests del arnés OIDC real.
/// </summary>
[CollectionDefinition("Keycloak")]
public sealed class KeycloakCollectionDefinition : ICollectionFixture<KeycloakContainerFixture>
{
    // Clase marcador — xUnit cablea el fixture por el atributo de arriba.
}
