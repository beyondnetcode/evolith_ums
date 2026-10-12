namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Estado de acceso del grafo (ADR-0156 §5.1). Enumeración CERRADA: el contrato publicado la
/// declara con `enum` en `auth-graph.schema.json`, así que añadir un valor es un cambio MAYOR de
/// esquema, no una ampliación silenciosa.
/// </summary>
public enum GraphAccessState
{
    /// <summary>
    /// Hay perfil vigente: el grafo lleva navegación, permisos y ámbitos. `onboardingPending`
    /// es `false`.
    /// </summary>
    Granted = 0,

    /// <summary>
    /// El usuario TIENE perfiles activos en el inquilino, pero NINGUNO en el sistema que se pidió
    /// —o el sistema pedido no existe, que es indistinguible por construcción: el filtro se aplica
    /// sobre los sistemas de los perfiles del usuario y NUNCA consulta el catálogo por código
    /// (ADR-0156 §6). `onboardingPending` es `false`: la cuenta está dada de alta y mostrarle un
    /// flujo de onboarding sería mentirle.
    /// </summary>
    NoProfileInSystem = 1,

    /// <summary>
    /// El usuario no tiene NINGÚN perfil activo en el inquilino (G-043, grafo lobby).
    /// `onboardingPending` es `true`.
    /// </summary>
    OnboardingPending = 2,
}
