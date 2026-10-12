namespace Evolith.Ums.Application.Configuration.ParameterCatalog.DTOs;

public sealed record ParameterDefinitionDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int DataTypeId,
    string DataTypeName,
    string DefaultValue,
    int ScopeId,
    string ScopeName,
    bool IsActive,
    bool IsMandatory,
    int DisplayOrder,
    string Version);

public sealed record ParameterGlobalValueDto(
    Guid Id,
    Guid ParameterDefinitionId,
    string ParameterCode,
    string EffectiveValue,
    string Status,
    string Version);

public sealed record ParameterTenantValueDto(
    Guid Id,
    Guid TenantId,
    string TenantCode,
    Guid ParameterDefinitionId,
    string ParameterCode,
    string OverrideValue,
    string Status,
    string Version);

public sealed record ResolvedParameterDto(
    Guid DefinitionId,
    string Code,
    string Name,
    string Description,
    int DataTypeId,
    string DataTypeName,
    string EffectiveValue,
    string DefaultValue,
    int ScopeId,
    string ScopeName,
    bool HasOverride,
    string Status);