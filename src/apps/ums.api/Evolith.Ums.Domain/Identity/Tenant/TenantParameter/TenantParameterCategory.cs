namespace Evolith.Ums.Domain.Identity.Tenant.TenantParameter;

public class TenantParameterCategory : DomainEnumeration
{
    public static readonly TenantParameterCategory Export = new(1, "Export");
    public static readonly TenantParameterCategory Security = new(2, "Security");
    public static readonly TenantParameterCategory Ui = new(3, "Ui");
    public static readonly TenantParameterCategory FeatureFlags = new(4, "FeatureFlags");
    public static readonly TenantParameterCategory Session = new(5, "Session");
    public static readonly TenantParameterCategory Localization = new(6, "Localization");
    public static readonly TenantParameterCategory Observability = new(7, "Observability");
    public static readonly TenantParameterCategory Permissions = new(8, "Permissions");

    private TenantParameterCategory(int value, string name) : base(value, name) { }

    public static TenantParameterCategory FromString(string name)
    {
        return name.ToUpperInvariant() switch
        {
            "EXPORT" => Export,
            "SECURITY" => Security,
            "UI" => Ui,
            "FEATUREFLAGS" => FeatureFlags,
            "SESSION" => Session,
            "LOCALIZATION" => Localization,
            "OBSERVABILITY" => Observability,
            "PERMISSIONS" => Permissions,
            _ => throw new ArgumentException($"Unknown category: {name}", nameof(name))
        };
    }

    public static TenantParameterCategory? TryFromString(string name)
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