namespace Evolith.Ums.Application.Identity.Auth;

using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration.AppConfiguration;

/// <summary>
/// Implementación fail-closed de <see cref="IRefreshTokenPolicyProvider"/>: lee los
/// códigos de <see cref="AppConfigurationCodes"/> por la cascada jerárquica del
/// <see cref="IConfigurationProvider"/>. Cualquier lectura ausente cae al default
/// seguro (deshabilitado). No emite ni persiste nada — solo resuelve la política.
/// </summary>
public sealed class RefreshTokenPolicyProvider : IRefreshTokenPolicyProvider
{
    private readonly IConfigurationProvider _config;

    public RefreshTokenPolicyProvider(IConfigurationProvider config)
    {
        _config = config;
    }

    public RefreshTokenPolicy Resolve(Guid? tenantId, Guid? suiteId = null, Guid? moduleId = null)
    {
        var enabled = _config.GetValueAs(
            AppConfigurationCodes.AuthRefreshTokenEnabled, tenantId, suiteId, moduleId,
            AppConfigurationDefaults.AuthRefreshTokenEnabled);

        if (!enabled)
        {
            return RefreshTokenPolicy.Disabled;
        }

        var lifetimeMinutes = _config.GetValueAs(
            AppConfigurationCodes.AuthRefreshTokenLifetimeMinutes, tenantId, suiteId, moduleId,
            AppConfigurationDefaults.AuthRefreshTokenLifetimeMinutes);

        // Vida no positiva con la capacidad activada: configuración inválida ⇒ fail-closed.
        if (lifetimeMinutes <= 0)
        {
            return RefreshTokenPolicy.Disabled;
        }

        var rotate = _config.GetValueAs(
            AppConfigurationCodes.AuthRefreshTokenRotate, tenantId, suiteId, moduleId,
            AppConfigurationDefaults.AuthRefreshTokenRotate);

        var detectReuse = _config.GetValueAs(
            AppConfigurationCodes.AuthRefreshTokenDetectReuse, tenantId, suiteId, moduleId,
            AppConfigurationDefaults.AuthRefreshTokenDetectReuse);

        var maxRenewals = _config.GetValueAs(
            AppConfigurationCodes.AuthRefreshTokenMaxRenewals, tenantId, suiteId, moduleId,
            AppConfigurationDefaults.AuthRefreshTokenMaxRenewals);

        return new RefreshTokenPolicy(
            Enabled: true,
            LifetimeMinutes: lifetimeMinutes,
            Rotate: rotate,
            DetectReuse: detectReuse,
            MaxRenewals: maxRenewals < 0 ? 0 : maxRenewals);
    }
}
