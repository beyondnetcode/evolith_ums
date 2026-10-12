using System.Text.Json;
using Evolith.Ums.Sdk.Contracts;

namespace Evolith.Ums.Sdk.Tests;

/// <summary>
/// Loads golden fixtures from <c>src/libs/sdk/contracts/fixtures/</c> (linked into the test bin/Fixtures dir).
/// </summary>
internal static class FixtureLoader
{
    private static readonly string FixtureDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = true
    };

    public static AuthorizationGraph Load(string name)
    {
        var path = Path.Combine(FixtureDir, $"{name}.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AuthorizationGraph>(json, Options)
            ?? throw new InvalidOperationException($"Fixture '{name}' deserialized to null.");
    }

    public static string LoadRaw(string name)
    {
        var path = Path.Combine(FixtureDir, $"{name}.json");
        return File.ReadAllText(path);
    }
}
