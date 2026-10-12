using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.IdpConfiguration;
using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;
using IdpConfigurationAggregate = Evolith.Ums.Domain.Configuration.IdpConfiguration.IdpConfiguration;
using TenantAggregate = Evolith.Ums.Domain.Identity.Tenant.Tenant;

namespace Evolith.Ums.Application.Identity.Auth;

/// <summary>
/// Implementación del fallback encadenado de FR-042 (ADR-UMS-097 §2.3/§2.4, slice 2b).
///
/// <para><b>Disparador (irrenunciable):</b> el avance por <c>FallbackToId</c> ocurre EXCLUSIVAMENTE
/// ante <see cref="IdpAuthOutcome.InfraUnavailable"/> (indisponibilidad de infraestructura). Un
/// <see cref="IdpAuthOutcome.CredentialTerminal"/> DETIENE el recorrido y devuelve el error (nunca se
/// prueba la credencial contra el siguiente IdP: anti <i>credential spraying</i>). La clasificación es
/// fail-closed (<see cref="IdpAuthOutcomeClassifier"/>).</para>
///
/// <para><b>Recorrido:</b> comienza en la configuración ganadora del selector 2a
/// (<see cref="IdpConfigurationSelector"/>, por prioridad/suite/dominio) y sigue <c>FallbackToId</c>
/// con detección de ciclos (conjunto de visitados) y tope de saltos configurable. Si la cadena se
/// agota por indisponibilidad devuelve <see cref="ChainExhaustedError"/> (503, no 401).</para>
///
/// <para><b>Puente reglas ↔ dominio (ADR-UMS-097 §2.5):</b> cada configuración se resuelve al
/// <see cref="IdentityProvider"/> del inquilino cuya estrategia corresponde a su <c>ProviderType</c>
/// (<c>ProviderType.Id</c> ≡ <c>IdpStrategy.Id</c>, enumeraciones paralelas). A diferencia de la
/// selección de método (2a), aquí <b>no</b> se exige <c>IsActive</c>: el inquilino solo puede tener un
/// proveedor activo, pero la cadena la gobierna <c>IdpConfiguration.Status</c>; los proveedores de los
/// eslabones de respaldo están registrados aunque no sean el activo primario.</para>
///
/// <para><b>Auditoría (ADR-UMS-097 §2.4):</b> un evento append-only por proveedor intentado, acotado por
/// inquilino, con {config intentada, resultado, motivo}. Nunca se registra la credencial ni secretos
/// ([G-040]#5): solo se audita el código/motivo del resultado, que no contiene material sensible.</para>
/// </summary>
public sealed class IdpChainAuthenticator : IIdpChainAuthenticator
{
    /// <summary>
    /// Cadena de fallback agotada por indisponibilidad ⇒ 503 (ADR-UMS-097 §2.3), no 401. Código local
    /// al motor de auth (mismo patrón que <c>OidcAuthErrors</c> AUTH_020..034; no vive en el catálogo SDK).
    /// </summary>
    public const string ChainExhaustedError =
        "AUTH_018: El servicio de autenticación federada no está disponible temporalmente (cadena de fallback agotada).";

    /// <summary>
    /// Guarda defensiva: modo IdP sin ningún proveedor utilizable. No debería alcanzarse tras la
    /// resolución 2a en el flujo externo; se conserva por robustez. Se clasifica como infra a nivel HTTP
    /// (AUTH_012 ⇒ 503) porque significa que el adaptador/proveedor no está disponible, no que la credencial sea inválida.
    /// </summary>
    private const string NoUsableProviderError =
        "AUTH_012: No hay un proveedor de identidad utilizable para el inquilino.";

    /// <summary>
    /// Tope de saltos configurable (cascada Global&gt;Suite&gt;Tenant&gt;Module); default seguro = 5.
    /// No se siembra como parámetro obligatorio: ausente ⇒ default. La detección de ciclos ya acota el
    /// recorrido con independencia de este tope (es una segunda red de seguridad).
    /// </summary>
    public const string MaxHopsConfigCode = "AUTH_IDP_FALLBACK_MAX_HOPS";
    private const int DefaultMaxHops = 5;

    private const string IdpAttemptEventType = "Auth.Login.IdpAttempt";

    private readonly IIdpConfigurationRepository _idpConfigRepo;
    private readonly IIdpAuthStrategy _idpStrategy;
    private readonly IAuthAuditService _auditService;
    private readonly IConfigurationProvider _config;

    public IdpChainAuthenticator(
        IIdpConfigurationRepository idpConfigRepo,
        IIdpAuthStrategy idpStrategy,
        IAuthAuditService auditService,
        IConfigurationProvider config)
    {
        _idpConfigRepo = idpConfigRepo;
        _idpStrategy = idpStrategy;
        _auditService = auditService;
        _config = config;
    }

    public async Task<Result<IdpChainOutcome>> AuthenticateAsync(
        TenantAggregate tenant,
        string credential,
        Guid? systemSuiteId,
        string? emailDomain,
        string clientIp,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenant.Props.Id.GetValue();
        var tenantCode = tenant.Props.Code.GetValue();

        // FR-042 (ADR-UMS-097 §2.2): misma procedencia de suite que el resolver 2a — si el AccessScope no
        // fija suite, se usa el default del inquilino. Mantiene coherente el eslabón inicial de la cadena
        // 2b con el proveedor que eligió la resolución del método (2a).
        var effectiveSuiteId = systemSuiteId ?? tenant.DefaultSystemSuiteId?.GetValue();

        var configurations = await _idpConfigRepo.GetByTenantIdAsync(tenantId, cancellationToken);
        var selection = IdpConfigurationSelector.Select(configurations, effectiveSuiteId, emailDomain, providerType: null);

        // ── Legacy: ninguna IdpConfiguration gobierna la selección → intento único contra el proveedor
        //    activo del inquilino, conservando el comportamiento previo a 2b (sin cadena ni 503).
        if (selection is null)
        {
            var legacyProvider = tenant.GetActiveIdentityProvider();
            if (legacyProvider is null)
            {
                return Result<IdpChainOutcome>.Failure(NoUsableProviderError);
            }

            return await AttemptLegacyAsync(tenantId, tenantCode, credential, clientIp, legacyProvider, cancellationToken);
        }

        // ── Cadena gobernada por reglas: recorrido de FallbackToId con ciclos + tope de saltos.
        var byId = BuildIndex(configurations);
        var maxHops = Math.Max(1, _config.GetValueAs<int>(MaxHopsConfigCode, tenantId, DefaultMaxHops));
        var visited = new HashSet<Guid>();

        var current = selection.Value.Configuration;
        var hops = 0;

        while (current is not null && current.Status == IdpConfigStatus.Active)
        {
            var configId = current.Props.Id.GetValue();

            // Detección de ciclos: un Id ya visitado corta el recorrido (sin bucle infinito).
            if (!visited.Add(configId))
            {
                break;
            }

            // Tope de saltos: red de seguridad secundaria a la detección de ciclos.
            if (hops >= maxHops)
            {
                break;
            }

            hops++;

            var provider = BridgeToProvider(tenant, current);
            if (provider is null)
            {
                // El adaptador/proveedor de esta configuración no está registrado → indisponibilidad de
                // infraestructura de ESTE eslabón (§2.3 «adaptador no disponible/no registrado»). No se
                // prueba ninguna credencial, así que avanzar no abre spraying.
                await AuditAttemptAsync(tenantId, tenantCode, clientIp, DescribeConfig(current),
                    IdpAuthOutcome.InfraUnavailable, "Proveedor no registrado para la estrategia de la configuración.",
                    cancellationToken);
                current = NextInChain(current, byId);
                continue;
            }

            var attempt = await _idpStrategy.AuthenticateAsync(tenantId, credential, provider, cancellationToken);
            var outcome = IdpAuthOutcomeClassifier.Classify(attempt);

            await AuditAttemptAsync(tenantId, tenantCode, clientIp, DescribeConfig(current), outcome,
                outcome == IdpAuthOutcome.Success ? "Autenticación exitosa." : attempt.Error, cancellationToken);

            switch (outcome)
            {
                case IdpAuthOutcome.Success:
                    return Result<IdpChainOutcome>.Success(new IdpChainOutcome(attempt.Value, provider));

                case IdpAuthOutcome.CredentialTerminal:
                    // TERMINAL: el IdP rechazó (o el fallo es no clasificable como infra). NO se avanza al
                    // siguiente proveedor — encadenar aquí permitiría credential spraying cross-IdP (§2.3).
                    return Result<IdpChainOutcome>.Failure(attempt.Error);

                default:
                    // Indisponibilidad de infraestructura (IdpAuthOutcome.InfraUnavailable): avanzar por FallbackToId.
                    current = NextInChain(current, byId);
                    break;
            }
        }

        // Cadena agotada (todos los eslabones indisponibles, o ciclo, o tope) → 503, NUNCA 401.
        return Result<IdpChainOutcome>.Failure(ChainExhaustedError);
    }

    /// <summary>Intento único (sin cadena) para inquilinos legados sin <c>IdpConfiguration</c> gobernante.</summary>
    private async Task<Result<IdpChainOutcome>> AttemptLegacyAsync(
        Guid tenantId,
        string tenantCode,
        string credential,
        string clientIp,
        IdentityProvider provider,
        CancellationToken cancellationToken)
    {
        var attempt = await _idpStrategy.AuthenticateAsync(tenantId, credential, provider, cancellationToken);
        var outcome = IdpAuthOutcomeClassifier.Classify(attempt);

        await AuditAttemptAsync(tenantId, tenantCode, clientIp,
            $"legacy:{provider.Strategy.Name}", outcome,
            outcome == IdpAuthOutcome.Success ? "Autenticación exitosa." : attempt.Error, cancellationToken);

        // Sin cadena no hay fallback: cualquier fallo es terminal y devuelve el error tal cual (comportamiento
        // previo a 2b; no se convierte en 503, que es una semántica propia del recorrido de cadena).
        return outcome == IdpAuthOutcome.Success
            ? Result<IdpChainOutcome>.Success(new IdpChainOutcome(attempt.Value, provider))
            : Result<IdpChainOutcome>.Failure(attempt.Error);
    }

    /// <summary>
    /// Puente configuración → proveedor por estrategia (ADR-UMS-097 §2.5). No exige <c>IsActive</c>: ver
    /// nota de clase. Devuelve <c>null</c> si el inquilino no registró un proveedor de esa estrategia.
    /// </summary>
    private static IdentityProvider? BridgeToProvider(TenantAggregate tenant, IdpConfigurationAggregate config)
        => tenant.IdentityProviders.FirstOrDefault(ip => ip.Strategy.Id == config.ProviderType.Id);

    private static IdpConfigurationAggregate? NextInChain(
        IdpConfigurationAggregate current,
        IReadOnlyDictionary<Guid, IdpConfigurationAggregate> byId)
    {
        var fallbackToId = current.Props.FallbackToId;
        if (!fallbackToId.HasValue)
        {
            return null;
        }

        return byId.TryGetValue(fallbackToId.Value, out var next) ? next : null;
    }

    private static IReadOnlyDictionary<Guid, IdpConfigurationAggregate> BuildIndex(
        IReadOnlyList<IdpConfigurationAggregate> configurations)
    {
        var index = new Dictionary<Guid, IdpConfigurationAggregate>(configurations.Count);
        foreach (var configuration in configurations)
        {
            index[configuration.Props.Id.GetValue()] = configuration;
        }

        return index;
    }

    /// <summary>Etiqueta de traza del intento: tipo de proveedor + Id de la configuración (no secreto).</summary>
    private static string DescribeConfig(IdpConfigurationAggregate config)
        => $"{config.ProviderType.Name}#{config.Props.Id.GetValue()}";

    private async Task AuditAttemptAsync(
        Guid tenantId,
        string tenantCode,
        string clientIp,
        string idpLabel,
        IdpAuthOutcome outcome,
        string reason,
        CancellationToken cancellationToken)
    {
        // Motivo auditado sin material sensible: es el código/mensaje del resultado, nunca la credencial
        // ni el token ([G-040]#5). La credencial jamás entra en estos textos.
        var resultLabel = outcome switch
        {
            IdpAuthOutcome.Success => "success",
            IdpAuthOutcome.InfraUnavailable => "infra->advance",
            _ => "credential->terminal",
        };

        await _auditService.RecordAuthEventAsync(new AuthAuditEvent(
            UserId: Guid.Empty,
            TenantId: tenantId,
            TenantCode: tenantCode,
            AuthMethod: "IDP",
            EventType: IdpAttemptEventType,
            Succeeded: outcome == IdpAuthOutcome.Success,
            ClientIp: clientIp,
            FailureReason: $"{resultLabel}: {reason}",
            IdpProvider: idpLabel), cancellationToken);
    }
}
