using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;
using TenantAggregate = Evolith.Ums.Domain.Identity.Tenant.Tenant;

namespace Evolith.Ums.Domain.Identity.Auth;

/// <summary>
/// Orquesta la autenticación federada del login recorriendo la cadena de fallback
/// <c>IdpConfiguration.FallbackToId</c> (FR-042 · ADR-UMS-097 §2.3/§2.4, slice 2b).
///
/// Partiendo de la configuración ganadora del selector (2a), intenta autenticar; si el intento
/// es <see cref="IdpAuthOutcome.InfraUnavailable"/> avanza al siguiente proveedor de la cadena;
/// si es <see cref="IdpAuthOutcome.CredentialTerminal"/> se detiene y devuelve el error (sin avanzar,
/// anti <i>credential spraying</i>); si es <see cref="IdpAuthOutcome.Success"/> devuelve la identidad
/// externa y el proveedor que efectivamente autenticó. Detecta ciclos y aplica un tope de saltos;
/// si la cadena se agota por indisponibilidad devuelve un fallo <c>AUTH_018</c> (503, no 401). Audita
/// un evento por proveedor intentado.
/// </summary>
public interface IIdpChainAuthenticator
{
    Task<Result<IdpChainOutcome>> AuthenticateAsync(
        TenantAggregate tenant,
        string credential,
        Guid? systemSuiteId,
        string? emailDomain,
        string clientIp,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado exitoso del recorrido de la cadena: la identidad externa validada y el
/// <see cref="IdentityProvider"/> que la autenticó (puede ser un proveedor de respaldo distinto
/// al primario cuando hubo fallback por indisponibilidad).
/// </summary>
public sealed record IdpChainOutcome(ExternalIdentity Identity, IdentityProvider Provider);
