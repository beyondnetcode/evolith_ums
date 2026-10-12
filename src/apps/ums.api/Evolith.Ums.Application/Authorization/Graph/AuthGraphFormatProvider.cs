using Evolith.Ums.Application.Identity.Tenant.TenantParameter.Services;
using Evolith.Ums.Domain.Identity.Tenant.TenantParameter;

namespace Evolith.Ums.Application.Authorization.Graph;

public sealed class AuthGraphFormatProvider : IAuthGraphFormatProvider
{
    private readonly ITenantParameterProvider _parameters;

    public AuthGraphFormatProvider(ITenantParameterProvider parameters)
    {
        _parameters = parameters;
    }

    public async Task<string> GetDefaultFormatAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var raw = await _parameters.GetValueAsync(
            tenantId,
            TenantParameterCodes.AuthGraphDefaultFormat,
            cancellationToken) ?? TenantParameterDefaults.AuthGraphDefaultFormat;
        return raw.Trim().ToUpperInvariant();
    }

    public async Task<IReadOnlyList<string>> GetAllowedFormatsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var allowed = await _parameters.GetStringListValueAsync(
            tenantId,
            TenantParameterCodes.AuthGraphAllowedFormats,
            TenantParameterDefaults.AuthGraphAllowedFormats,
            cancellationToken);
        return allowed.Select(f => f.Trim().ToUpperInvariant()).ToList();
    }

    public async Task<string> ResolveFormatAsync(Guid tenantId, string? requestedFormat, CancellationToken cancellationToken = default)
    {
        var defaultFormat = await GetDefaultFormatAsync(tenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(requestedFormat)) return defaultFormat;

        var normalized = requestedFormat.Trim().ToUpperInvariant();
        var allowed    = await GetAllowedFormatsAsync(tenantId, cancellationToken);

        return allowed.Contains(normalized) ? normalized : defaultFormat;
    }
}
