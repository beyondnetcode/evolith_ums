using BeyondNetCode.Shell.Ddd;

namespace Evolith.Ums.Infrastructure.Persistence.Reflection;

internal static class DomainEnumerationMapper
{
    public static TEnum FromValue<TEnum>(int value) where TEnum : DomainEnumeration
    {
        return DomainEnumeration.FromValue<TEnum>(value)
            ?? throw new InvalidOperationException($"Enumeration {typeof(TEnum).Name} does not define value {value}.");
    }

    public static TEnum FromName<TEnum>(string name) where TEnum : DomainEnumeration
    {
        return DomainEnumeration.FromDisplayName<TEnum>(name)
            ?? throw new InvalidOperationException($"Enumeration {typeof(TEnum).Name} does not define name {name}.");
    }
}
