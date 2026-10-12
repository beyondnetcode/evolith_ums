using System.Reflection;

namespace Evolith.Ums.Domain.Enums;

public static class DomainEnumerationExtensions
{
    public static T? TryFromString<T>(string name) where T : DomainEnumeration
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var upperName = name.ToUpperInvariant();
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var value = field.GetValue(null) as DomainEnumeration;
            if (value != null && value.Name.ToUpperInvariant() == upperName)
            {
                return value as T;
            }
        }
        return null;
    }

    public static T FromString<T>(string name) where T : DomainEnumeration
    {
        return TryFromString<T>(name)
            ?? throw new ArgumentException($"Cannot convert '{name}' to {typeof(T).Name}", nameof(name));
    }

    public static int ToId<T>(this T enumeration) where T : DomainEnumeration
        => enumeration.Id;

    public static string ToName<T>(this T enumeration) where T : DomainEnumeration
        => enumeration.Name;
}