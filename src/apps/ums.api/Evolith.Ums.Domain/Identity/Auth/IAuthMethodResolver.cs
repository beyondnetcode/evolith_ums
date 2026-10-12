namespace Evolith.Ums.Domain.Identity.Auth;

/// <summary>
/// Resolves the authentication method for a tenant by reading
/// AUTH_USE_EXTERNAL_IDP from the in-memory configuration provider
/// (never hits the database per request).
///
/// Portal management access always resolves to local authentication.
/// Returns AuthMethod.Local() when AUTH_USE_EXTERNAL_IDP == false.
/// Returns AuthMethod.Idp(provider) when true and an active IDP exists.
/// Returns failure when IDP mode is configured but no active provider is found,
/// except for InternalPreview where IDP mode is still returned without a provider
/// so administrative previews can render the effective graph context.
///
/// FR-042 (ADR-UMS-097 §2.2): en modo IDP la selección del proveedor se rige por el motor de
/// reglas (prioridad/suite/dominio). El contexto de resolución se enhebra aquí:
/// <paramref name="systemSuiteId"/> (suite; <c>null</c> si el alcance no la fija) y
/// <paramref name="emailDomain"/> (dominio extraído del identificador de login; <c>null</c>
/// si no es un email → se omite el filtro por dominio). Es un cambio de firma interno,
/// no del contrato REST público de /auth/login.
/// </summary>
public interface IAuthMethodResolver
{
    Task<Result<AuthMethod>> ResolveAsync(
        Guid tenantId,
        AuthAccessScope scope,
        Guid? systemSuiteId = null,
        string? emailDomain = null,
        CancellationToken cancellationToken = default);
}
