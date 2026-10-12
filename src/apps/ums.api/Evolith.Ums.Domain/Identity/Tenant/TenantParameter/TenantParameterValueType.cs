namespace Evolith.Ums.Domain.Identity.Tenant.TenantParameter;

public class TenantParameterValueType : DomainEnumeration
{
    public static readonly TenantParameterValueType String = new(1, "String");
    public static readonly TenantParameterValueType Integer = new(2, "Integer");
    public static readonly TenantParameterValueType Boolean = new(3, "Boolean");
    public static readonly TenantParameterValueType StringList = new(4, "StringList");
    public static readonly TenantParameterValueType Json = new(5, "Json");

    private TenantParameterValueType(int value, string name) : base(value, name) { }

    public static TenantParameterValueType FromString(string name)
    {
        return name.ToUpperInvariant() switch
        {
            "STRING" => String,
            "INTEGER" => Integer,
            "BOOLEAN" => Boolean,
            "STRINGLIST" => StringList,
            "JSON" => Json,
            _ => throw new ArgumentException($"Unknown value type: {name}", nameof(name))
        };
    }

    public static TenantParameterValueType? TryFromString(string name)
    {
        try
        {
            return FromString(name);
        }
        catch
        {
            return null;
        }
    }
}