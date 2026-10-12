namespace Evolith.Ums.Domain.Configuration.IdpConfiguration;

/// <summary>
/// Regla de selección de <see cref="IdpConfiguration"/> de FR-042 (ADR-UMS-097 §2.1), como
/// política de dominio pura y sin dependencias. Es la <b>fuente única</b> de la selección:
/// la consumen tanto la resolución del método de login (<c>AuthMethodResolverService</c>)
/// como el motor de consulta OIDC (<c>IdpConfigurationResolver</c>), para que ambos
/// subsistemas —que G-096 halló desconectados— apliquen exactamente las mismas reglas.
///
/// Selección:
/// <list type="number">
///   <item>Candidatos = configuraciones <see cref="IdpConfigStatus.Active"/>, filtradas por
///     <c>systemSuiteId</c> (si se fija) y por <c>providerType</c> (si se fija).</item>
///   <item>Si algún candidato casa con el dominio de email (<c>DomainHints</c>), el conjunto
///     se restringe a los que casan por dominio; si ninguno casa, se conserva el conjunto completo.</item>
///   <item>Del conjunto resultante gana la de menor <see cref="IdpConfiguration.ResolutionPriority"/>
///     (orden asc); ante empate, la de mayor <see cref="IdpConfiguration.Version"/>.</item>
/// </list>
///
/// El <b>fallback encadenado</b> por indisponibilidad (ADR-UMS-097 §2.3, slice 2b) NO se implementa aquí:
/// esta regla elige UN proveedor y no encadena. Un fallo posterior de credenciales es terminal.
/// </summary>
public static class IdpConfigurationSelector
{
    /// <summary>Configuración ganadora y si su elección vino guiada por coincidencia de dominio.</summary>
    public readonly record struct Selection(IdpConfiguration Configuration, bool DomainMatched);

    /// <summary>
    /// Aplica la regla FR-042 sobre las configuraciones del inquilino.
    /// Devuelve <c>null</c> si no hay ninguna configuración activa que satisfaga los filtros.
    /// </summary>
    /// <param name="configurations">Configuraciones del inquilino (cualquier estado).</param>
    /// <param name="systemSuiteId">Suite por la que filtrar; <c>null</c> = sin filtro de suite.</param>
    /// <param name="emailDomain">Dominio (o email completo) para el routing por dominio; <c>null</c> = sin filtro de dominio.</param>
    /// <param name="providerType">Nombre de <c>ProviderType</c> por el que filtrar; <c>null</c> = cualquier tipo.</param>
    public static Selection? Select(
        IReadOnlyList<IdpConfiguration> configurations,
        Guid? systemSuiteId,
        string? emailDomain,
        string? providerType)
    {
        var candidates = configurations
            .Where(configuration => configuration.Status == IdpConfigStatus.Active)
            .Where(configuration => !systemSuiteId.HasValue || configuration.SystemSuiteId.GetValue() == systemSuiteId.Value)
            .Where(configuration => string.IsNullOrWhiteSpace(providerType)
                || string.Equals(configuration.ProviderType.Name, providerType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var normalizedDomain = NormalizeDomain(emailDomain);
        var domainMatchedCandidates = string.IsNullOrWhiteSpace(normalizedDomain)
            ? new List<IdpConfiguration>()
            : candidates.Where(configuration => MatchesDomain(configuration, normalizedDomain)).ToList();

        var domainMatched = domainMatchedCandidates.Count > 0;
        var winner = (domainMatched ? domainMatchedCandidates : candidates)
            .OrderBy(configuration => configuration.ResolutionPriority)
            .ThenByDescending(configuration => configuration.Version)
            .First();

        return new Selection(winner, domainMatched);
    }

    private static bool MatchesDomain(IdpConfiguration configuration, string normalizedDomain)
        => configuration.Props.DomainHints.Any(hint => string.Equals(hint.Trim(), normalizedDomain, StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeDomain(string? emailDomain)
    {
        if (string.IsNullOrWhiteSpace(emailDomain))
        {
            return null;
        }

        var trimmed = emailDomain.Trim();
        var atIndex = trimmed.IndexOf('@');
        return atIndex >= 0 ? trimmed[(atIndex + 1)..].Trim().ToLowerInvariant() : trimmed.ToLowerInvariant();
    }
}
