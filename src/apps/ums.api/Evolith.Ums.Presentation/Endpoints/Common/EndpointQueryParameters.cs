namespace Evolith.Ums.Presentation.Endpoints.Common;

internal record EndpointPagedQueryParameters
{
    [FromQuery]
    public int Page { get; init; }

    [FromQuery]
    public int PageSize { get; init; }

    [FromQuery]
    public string? Search { get; init; }

    [FromQuery]
    public string? Criteria { get; init; }

    [FromQuery]
    public string? Status { get; init; }

    [FromQuery]
    public string? SortBy { get; init; }

    [FromQuery]
    public string? SortOrder { get; init; }
}

internal record TenantPagedQueryParameters : EndpointPagedQueryParameters
{
    [FromQuery]
    public Guid? TenantId { get; init; }
}

internal sealed record TenantUserPagedQueryParameters : TenantPagedQueryParameters
{
    [FromQuery]
    public Guid? UserId { get; init; }
}

internal sealed record PermissionTemplatePagedQueryParameters : TenantPagedQueryParameters
{
    [FromQuery]
    public Guid? SystemSuiteId { get; init; }

    [FromQuery]
    public Guid? RoleId { get; init; }
}

internal sealed record FeatureFlagPagedQueryParameters : EndpointPagedQueryParameters
{
    [FromQuery]
    public string? FlagType { get; init; }

    [FromQuery]
    public string? LinkedResourceType { get; init; }
}

internal sealed record IdpConfigurationPagedQueryParameters : TenantPagedQueryParameters
{
    [FromQuery]
    public Guid? SystemSuiteId { get; init; }

    [FromQuery]
    public string? ProviderType { get; init; }
}

internal sealed record AppConfigurationPagedQueryParameters : EndpointPagedQueryParameters
{
    [FromQuery]
    public string? Scope { get; init; }

    [FromQuery]
    public Guid? TenantId { get; init; }

    [FromQuery]
    public Guid? SystemSuiteId { get; init; }

    [FromQuery]
    public Guid? ModuleId { get; init; }
}

internal sealed record AuditRecordPagedQueryParameters
{
    [FromQuery]
    public int Page { get; init; }

    [FromQuery]
    public int PageSize { get; init; }

    [FromQuery]
    public string? EventType { get; init; }

    [FromQuery]
    public Guid? ActorId { get; init; }

    [FromQuery]
    public Guid? EntityId { get; init; }

    [FromQuery]
    public string? EntityType { get; init; }

    [FromQuery]
    public Guid? TenantId { get; init; }

    [FromQuery]
    public DateTime? From { get; init; }

    [FromQuery]
    public DateTime? To { get; init; }
}

internal sealed record AppConfigurationResolveQueryParameters
{
    [FromQuery]
    public string Code { get; init; } = string.Empty;

    [FromQuery]
    public Guid? TenantId { get; init; }

    [FromQuery]
    public Guid? SuiteId { get; init; }

    [FromQuery]
    public Guid? ModuleId { get; init; }
}

internal sealed record ProfileAuthGraphPreviewParameters
{
    [FromRoute]
    public Guid ProfileId { get; init; }

    [FromQuery]
    public string? Format { get; init; }
}
