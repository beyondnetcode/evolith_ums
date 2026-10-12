using Evolith.Ums.Domain.Identity.Auth;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Port for building the complete AuthorizationGraph for an authenticated user.
///
/// The command handler that resolves credentials passes the already-fetched
/// UserAccount so the builder does not perform a redundant DB lookup.
/// </summary>
public interface IAuthorizationGraphBuilder
{
    /// <summary>
    /// Builds the full authorization graph for the given user and tenant.
    /// </summary>
    /// <param name="userAccount">The authenticated user — already fetched by the command handler.</param>
    /// <param name="tenantId">The tenant in which the user authenticated.</param>
    /// <param name="authMethod">The resolved auth method (Local or IDP).</param>
    /// <param name="systemCode">
    /// Código del sistema que pide el grafo, OPCIONAL (ADR-0156 §3.2). Ausente —portal
    /// multiproducto— el grafo se arma sobre todos los perfiles del usuario; presente, solo sobre
    /// los perfiles cuyo sistema coincide. El filtro se aplica SOBRE LOS PERFILES DEL USUARIO y
    /// tiene prohibido consultar el catálogo de sistemas por código: es lo que hace que un código
    /// inexistente y uno sin perfil sean indistinguibles por construcción, sin dos ramas que
    /// puedan divergir en un mensaje, un estado o un tiempo (ADR-0156 §6).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="projection">
    /// Bloques opcionales que el llamante pidió, o <c>null</c> —el valor por defecto— para el
    /// grafo completo. Llega hasta AQUÍ y no se aplica al final a propósito: filtrar solo al
    /// serializar ahorraría bytes pero no trabajo, y la única sección con consulta propia
    /// (<c>featureFlags</c>) seguiría pagándose aunque nadie la hubiera pedido.
    ///
    /// Va DESPUÉS de <paramref name="cancellationToken"/>, contra la convención habitual, y es una
    /// decisión consciente: los cuatro llamantes que no proyectan pasan el token de cancelación
    /// por POSICIÓN, y las expectativas de Moq de una decena de pruebas fijan esa misma lista de
    /// argumentos. Colocarlo antes convertiría un cambio aditivo en una migración de veinte
    /// ficheros sin que ninguno de ellos cambiara de comportamiento.
    /// </param>
    Task<Result<AuthorizationGraph>> BuildAsync(
        UserAccountAggregate userAccount,
        Guid                 tenantId,
        AuthMethod           authMethod,
        string?              systemCode = null,
        CancellationToken    cancellationToken = default,
        GraphProjection?     projection = null);

    /// <summary>
    /// Builds the authorization graph for a specific profile of the given user and tenant.
    /// Used by the admin preview flow to avoid resolving a different active profile.
    /// </summary>
    /// <param name="systemCode">
    /// Acota el bloque `profiles` del grafo al sistema indicado, igual que en
    /// <see cref="BuildAsync"/>. El PERFIL VIGENTE no lo elige este filtro —llega dado en
    /// <paramref name="profileId"/>—, pero la lista de perfiles disponibles sí debe respetarlo:
    /// devolverla completa entregaría a un satélite acotado a un sistema el inventario de los
    /// demás sistemas en los que ese usuario trabaja (ADR-0156 §2.5).
    /// </param>
    /// <param name="projection">Igual que en <see cref="BuildAsync"/>; <c>null</c> = grafo completo.</param>
    Task<Result<AuthorizationGraph>> BuildForProfileAsync(
        UserAccountAggregate userAccount,
        Guid                 tenantId,
        Guid                 profileId,
        AuthMethod           authMethod,
        string?              systemCode = null,
        CancellationToken    cancellationToken = default,
        GraphProjection?     projection = null);
}
