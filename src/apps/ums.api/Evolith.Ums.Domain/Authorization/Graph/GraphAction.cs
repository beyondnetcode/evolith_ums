using System.Text.Json.Serialization;

namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// A registered action in the SystemSuite — the full catalogue of actions
/// the system can perform, returned in the graph so clients know what actions exist.
/// </summary>
public sealed record GraphAction(
    [property: JsonIgnore] Guid Id,
    string Code,
    [property: JsonPropertyName("value")] string Name);
