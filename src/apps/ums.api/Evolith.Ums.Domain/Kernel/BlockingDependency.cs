namespace Evolith.Ums.Domain.Kernel;

/// <summary>
/// Describes a single dependency that blocks a state-change or deletion operation.
/// Serialized by the Presentation layer into the structured error response.
/// </summary>
public sealed record BlockingDependency(
    string EntityType,    // e.g. "UserAccount", "Profile", "Role"
    string Status,        // e.g. "Active", "Published"
    int    Count);        // number of blocking instances found

/// <summary>
/// Encodes a blocked-operation error with structured dependency metadata.
/// Format: "<ERROR_CODE>|<json-of-dependencies>"
/// Parsed by the Presentation layer to produce a rich API response.
/// </summary>
public static class BlockedOperationError
{
    public static string Encode(string errorCode, IReadOnlyList<BlockingDependency> deps)
    {
        var depsJson = System.Text.Json.JsonSerializer.Serialize(deps);
        return $"{errorCode}|{depsJson}";
    }

    public static bool TryDecode(string error, out string errorCode, out IReadOnlyList<BlockingDependency> deps)
    {
        var idx = error.IndexOf('|');
        if (idx < 0)
        {
            errorCode = error;
            deps = [];
            return false;
        }

        errorCode = error[..idx];
        try
        {
            deps = System.Text.Json.JsonSerializer.Deserialize<List<BlockingDependency>>(error[(idx + 1)..])
                   ?? [];
            return true;
        }
        catch
        {
            deps = [];
            return false;
        }
    }
}
