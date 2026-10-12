using System.Text.Json.Serialization;

namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Authorization state for a domain resource (Aggregate, Entity, or DomainMethod)
/// registered in the SystemSuite. Each resource lists every system action with its
/// resolved effect, giving clients fine-grained access control at the domain model level.
/// </summary>
public sealed record GraphDomainPermission(
    [property: JsonIgnore] Guid ResourceId,
    string                            ResourceType,   // "Aggregate" | "Entity" | "DomainMethod"
    string                            ResourceCode,
    [property: JsonPropertyName("value")] string ResourceName,
    [property: JsonIgnore] Guid? ModuleId,
    [property: JsonIgnore] Guid? ParentResourceId,
    IReadOnlyList<GraphDomainAction>  Actions);

public sealed record GraphDomainAction(
    [property: JsonIgnore] Guid ActionId,
    string           ActionCode,
    [property: JsonPropertyName("value")] string ActionName,
    AccessEffect Effect,
    PermissionSource Source);
