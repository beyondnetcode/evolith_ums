#pragma warning disable S1144
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Evolith.Ums.Globalization.Access;

public static class StringLocalizer
{
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _cache = new();
    private static readonly object _loadLock = new();

    public static string T(string key, string? language = null)
    {
        var lang = language ?? CultureContext.Current;
        var dict = GetDictionary(lang);
        return dict.TryGetValue(key, out var value) ? value : key;
    }

    public static LocalizedMessage Localize(string key, string? language = null)
    {
        var lang = language ?? CultureContext.Current;
        return new LocalizedMessage(key, T(key, lang), lang);
    }

    public static void ClearCache() => _cache.Clear();

    private static Dictionary<string, string> GetDictionary(string language)
    {
        return _cache.GetOrAdd(language, LoadResources);
    }

    private static Dictionary<string, string> LoadResources(string language)
    {
        var assembly = typeof(StringLocalizer).Assembly;
        var resourcePrefix = $"Evolith.Ums.Globalization.Resources.{language}";
        var merged = new Dictionary<string, string>();

        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase) && n.EndsWith(".json"));

        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (entries != null)
            {
                foreach (var kvp in entries)
                {
                    merged.TryAdd(kvp.Key, kvp.Value);
                }
            }
        }

        return merged;
    }
}

#pragma warning restore S1144
