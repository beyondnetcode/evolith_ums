using System.Threading;

namespace Evolith.Ums.Globalization.Access;

public static class CultureContext
{
    private static readonly AsyncLocal<string?> _currentCulture = new();

    public static string Current
    {
        get => _currentCulture.Value ?? "en";
        set => _currentCulture.Value = value;
    }

    public static IDisposable Set(string culture)
    {
        var previous = _currentCulture.Value;
        _currentCulture.Value = culture;
        return new CultureRestorer(previous);
    }

    private sealed class CultureRestorer(string? previous) : IDisposable
    {
        public void Dispose() => _currentCulture.Value = previous;
    }
}
