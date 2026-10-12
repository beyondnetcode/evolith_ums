using Evolith.Ums.Domain.Identity.Repositories.TenantParameter;
using Evolith.Ums.Domain.Identity.Tenant.TenantParameter;
using TenantParameterEntity = Evolith.Ums.Domain.Identity.Tenant.TenantParameter.TenantParameter;

namespace Evolith.Ums.Application.Identity.Tenant.TenantParameter.Services;

public sealed class TenantParameterProvider : ITenantParameterProvider
{
    private readonly ITenantParameterRepository _repository;

    public TenantParameterProvider(ITenantParameterRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<TenantParameterDto>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var parameters = await _repository.GetByTenantIdAsync(tenantId, cancellationToken);
        return parameters.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<TenantParameterDto>> GetActiveByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var parameters = await _repository.GetActiveByTenantIdAsync(tenantId, cancellationToken);
        return parameters.Select(MapToDto).ToList();
    }

    public async Task<TenantParameterDto?> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default)
    {
        var parameter = await _repository.GetByCodeAsync(tenantId, code, cancellationToken);
        if (parameter is null) return null;

        ValidateTenantOwnership(tenantId, parameter);
        return MapToDto(parameter);
    }

    public async Task<string?> GetValueAsync(Guid tenantId, string code, CancellationToken cancellationToken = default)
    {
        var parameter = await _repository.GetByCodeAsync(tenantId, code, cancellationToken);
        if (parameter is null) return null;

        ValidateTenantOwnership(tenantId, parameter);
        return parameter.Value;
    }

    public async Task<bool> GetBoolValueAsync(Guid tenantId, string code, bool defaultValue = false, CancellationToken cancellationToken = default)
    {
        var parameter = await _repository.GetByCodeAsync(tenantId, code, cancellationToken);
        if (parameter is null) return defaultValue;

        ValidateTenantOwnership(tenantId, parameter);

        if (parameter.ValueType.Id != TenantParameterValueType.Boolean.Id)
            return defaultValue;

        return bool.TryParse(parameter.Value, out var result) ? result : defaultValue;
    }

    public async Task<int> GetIntValueAsync(Guid tenantId, string code, int defaultValue = 0, CancellationToken cancellationToken = default)
    {
        var parameter = await _repository.GetByCodeAsync(tenantId, code, cancellationToken);
        if (parameter is null) return defaultValue;

        ValidateTenantOwnership(tenantId, parameter);

        if (parameter.ValueType.Id != TenantParameterValueType.Integer.Id)
            return defaultValue;

        return int.TryParse(parameter.Value, out var result) ? result : defaultValue;
    }

    public async Task<string[]> GetStringListValueAsync(Guid tenantId, string code, string[] defaultValue = null!, CancellationToken cancellationToken = default)
    {
        var parameter = await _repository.GetByCodeAsync(tenantId, code, cancellationToken);
        if (parameter is null) return defaultValue;

        ValidateTenantOwnership(tenantId, parameter);

        if (parameter.ValueType.Id != TenantParameterValueType.StringList.Id)
            return defaultValue;

        if (string.IsNullOrWhiteSpace(parameter.Value))
            return defaultValue;

        return parameter.Value.Split(',').Select(s => s.Trim()).ToArray();
    }

    private static void ValidateTenantOwnership(Guid expectedTenantId, TenantParameterEntity parameter)
    {
        if (parameter.TenantId.GetValue() != expectedTenantId)
        {
            throw new TenantParameterIsolationException(
                $"Parameter '{parameter.Code.GetValue()}' does not belong to tenant '{expectedTenantId}'.");
        }
    }

    private static TenantParameterDto MapToDto(TenantParameterEntity parameter)
    {
        return new TenantParameterDto(
            parameter.GetId().GetValue(),
            parameter.TenantId.GetValue(),
            parameter.Code.GetValue(),
            parameter.Description.GetValue(),
            parameter.Value,
            parameter.ValueType.Name,
            parameter.Category.Name,
            parameter.IsActive,
            parameter.IsSensitive,
            parameter.DefaultValue,
            parameter.AllowedValues);
    }
}

public class TenantParameterIsolationException : Exception
{
    public TenantParameterIsolationException(string message) : base(message) { }
}