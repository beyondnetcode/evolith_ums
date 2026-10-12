namespace Evolith.Ums.Domain.Test.Identity.Auth;

using System.Collections.Generic;
using Evolith.Ums.Domain.Identity.Auth;
using Xunit;

/// <summary>
/// FR-042 (ADR-UMS-097 §2.3, slice 2b): pruebas del clasificador de outcome IdP, que es la decisión de
/// seguridad que gobierna el fallback encadenado. Verifica que SOLO señales inequívocas de
/// indisponibilidad de infraestructura se clasifiquen como <see cref="IdpAuthOutcome.InfraUnavailable"/>
/// (autorizan avanzar), y que TODO lo demás sea <see cref="IdpAuthOutcome.CredentialTerminal"/>
/// (fail-closed, anti credential-spraying).
/// </summary>
public class IdpAuthOutcomeClassifierTests
{
    private static Result<ExternalIdentity> Success()
        => Result<ExternalIdentity>.Success(new ExternalIdentity(
            "user@ransa.pe", "sub-1", "User", new Dictionary<string, string>()));

    private static Result<ExternalIdentity> Failure(string error)
        => Result<ExternalIdentity>.Failure(error);

    // ── Éxito ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Classify_Success_ReturnsSuccess()
    {
        Assert.Equal(IdpAuthOutcome.Success, IdpAuthOutcomeClassifier.Classify(Success()));
    }

    // ── Indisponibilidad de infraestructura (ÚNICO caso que autoriza avanzar) ─────

    [Theory]
    [InlineData("AUTH_012: No IDP adapter registered for strategy 'KEYCLOAK'.")]
    [InlineData("AUTH_034: No se pudo obtener el JWKS del issuer. HTTP 503.")]
    // G-108: el token endpoint OIDC 5xx/timeout/transporte emite AUTH_035 (rama de INFRA, distinta del
    // AUTH_021 de credencial) → clasifica como InfraUnavailable → habilita el fallback encadenado.
    [InlineData("AUTH_035: El token endpoint del IdP no está disponible (5xx/timeout/transporte). HTTP 503.")]
    [InlineData("AUTH_035: El token endpoint del IdP no está disponible (5xx/timeout/transporte). No se pudo contactar el token endpoint (transporte/timeout).")]
    public void Classify_InfraSignals_ReturnsInfraUnavailable(string error)
    {
        Assert.Equal(IdpAuthOutcome.InfraUnavailable, IdpAuthOutcomeClassifier.Classify(Failure(error)));
    }

    // ── Fallos de credencial/token del IdP → TERMINAL (no avanza) ─────────────────

    [Theory]
    [InlineData("AUTH_006: Invalid username or password.")]
    [InlineData("AUTH_004: Authenticated IDP user has no UMS account.")]
    [InlineData("AUTH_020: No se encontró configuración OIDC válida para el proveedor.")]
    [InlineData("AUTH_026: La firma del 'id_token' es inválida.")]
    [InlineData("AUTH_027: El 'iss' del 'id_token' no coincide con el issuer configurado.")]
    [InlineData("AUTH_029: El 'id_token' está expirado ('exp').")]
    [InlineData("AUTH_031: El 'nonce' del 'id_token' no coincide con el emitido en la autorización.")]
    [InlineData("AUTH_033: El parámetro 'state' del callback no coincide con el emitido.")]
    public void Classify_CredentialAndTokenRejections_ReturnsCredentialTerminal(string error)
    {
        Assert.Equal(IdpAuthOutcome.CredentialTerminal, IdpAuthOutcomeClassifier.Classify(Failure(error)));
    }

    // ── GUARDA ANTI-REGRESIÓN DE SEGURIDAD (ADR-UMS-097 §2.3, G-108) ─────────────────────────────
    //    AUTH_021 es la rama de CREDENCIAL/4xx del intercambio de código (invalid_grant). DEBE seguir
    //    siendo TERMINAL: si se clasificara como infra, la cadena de fallback probaría la MISMA credencial
    //    contra cada IdP → credential spraying cross-IdP. Esta prueba NO debe "arreglarse" debilitándola
    //    (p. ej. metiendo AUTH_021 en InfraUnavailableCodes o clasificando por el texto «503»): la
    //    resiliencia se ganó separando ESTRUCTURALMENTE el 5xx en AUTH_035, no relajando el 4xx.

    [Fact]
    public void Classify_TokenExchange4xxInvalidGrant_StaysTerminal_AntiSprayingGuard()
    {
        // Un 4xx invalid_grant (rechazo de credencial por el token endpoint) → AUTH_021 → TERMINAL.
        Assert.Equal(IdpAuthOutcome.CredentialTerminal,
            IdpAuthOutcomeClassifier.Classify(Failure("AUTH_021: Falló el intercambio del código por tokens contra el IdP. HTTP 400.")));
    }

    [Fact]
    public void Classify_TokenExchange4xx_IsNotInfra_EvenIfMessageMentions503_AntiSprayingGuard()
    {
        // Blindaje contra clasificación por texto: aunque el mensaje incluyera «503», el CÓDIGO AUTH_021
        // (credencial/4xx) manda ⇒ TERMINAL. La separación es por código/status estructural, nunca por texto.
        Assert.Equal(IdpAuthOutcome.CredentialTerminal,
            IdpAuthOutcomeClassifier.Classify(Failure("AUTH_021: Falló el intercambio del código por tokens contra el IdP. HTTP 503.")));
        Assert.False(IdpAuthOutcomeClassifier.IsInfraUnavailable(
            "AUTH_021: Falló el intercambio del código por tokens contra el IdP. HTTP 503."));
    }

    // ── AUTH_013 está reutilizado por flujos no-IdP → ambiguo → TERMINAL (fail-closed) ────

    [Fact]
    public void Classify_Auth013_IsTreatedAsTerminal_FailClosed()
    {
        Assert.Equal(IdpAuthOutcome.CredentialTerminal,
            IdpAuthOutcomeClassifier.Classify(Failure("AUTH_013: Stub IDP only accepts credentials starting with 'MOCK-'.")));
    }

    // ── Códigos desconocidos / error sin código → TERMINAL (fail-closed) ─────────

    [Theory]
    [InlineData("AUTH_999: algo no clasificable")]
    [InlineData("mensaje sin código de error")]
    public void Classify_UnknownOrCodeless_ReturnsCredentialTerminal(string error)
    {
        Assert.Equal(IdpAuthOutcome.CredentialTerminal, IdpAuthOutcomeClassifier.Classify(Failure(error)));
    }

    [Fact]
    public void Classify_NullResult_ReturnsCredentialTerminal()
    {
        Assert.Equal(IdpAuthOutcome.CredentialTerminal, IdpAuthOutcomeClassifier.Classify(null!));
    }

    // El helper IsInfraUnavailable es defensivo ante error vacío/nulo (fail-closed).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("mensaje sin código")]
    [InlineData("AUTH_999: desconocido")]
    public void IsInfraUnavailable_EmptyNullOrUnknown_ReturnsFalse(string? error)
    {
        Assert.False(IdpAuthOutcomeClassifier.IsInfraUnavailable(error));
    }

    // ── La lista blanca de infra es minimalista y explícita ──────────────────────

    [Fact]
    public void InfraAllowlist_IsExactlyAdapterJwksAndTokenEndpoint()
    {
        // La lista blanca crece SOLO con señales inequívocas de infra. G-108 añade AUTH_035 (token endpoint
        // OIDC 5xx/timeout/transporte). AUTH_021 (4xx credencial) NUNCA entra aquí.
        Assert.Equal(new HashSet<string> { "AUTH_012", "AUTH_034", "AUTH_035" },
            new HashSet<string>(IdpAuthOutcomeClassifier.InfraUnavailableCodes));
        Assert.DoesNotContain("AUTH_021", IdpAuthOutcomeClassifier.InfraUnavailableCodes);
    }
}
