namespace Evolith.Ums.Domain.Configuration.Parameter.ValueObjects;

public sealed class ParameterScope : DomainEnumeration
{
    public static readonly ParameterScope GlobalOnly      = new(1, nameof(GlobalOnly));
    public static readonly ParameterScope TenantOnly      = new(2, nameof(TenantOnly));
    public static readonly ParameterScope GlobalAndTenant = new(3, nameof(GlobalAndTenant));
    /// <summary>Applies at System Suite level (ConfigurationScope.Suite = 4).</summary>
    public static readonly ParameterScope SuiteLevel      = new(4, nameof(SuiteLevel));
    /// <summary>Applies at Module level (ConfigurationScope.Module = 5).</summary>
    public static readonly ParameterScope ModuleLevel     = new(5, nameof(ModuleLevel));

    public bool SupportsGlobal => Id == 1 || Id == 3;
    public bool SupportsTenant => Id == 2 || Id == 3;
    public bool SupportsSuite  => Id == 4;
    public bool SupportsModule => Id == 5;

    private ParameterScope(int value, string name) : base(value, name) { }

    public static IEnumerable<ParameterScope> AllScopes => [GlobalOnly, TenantOnly, GlobalAndTenant, SuiteLevel, ModuleLevel];

    public static ParameterScope FromValue(int value) =>
        AllScopes.FirstOrDefault(s => s.Id == value)
        ?? throw new ArgumentOutOfRangeException(nameof(value), $"Invalid scope value: {value}");
}