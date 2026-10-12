namespace Evolith.Ums.Sdk.Authorization.Aop;

/// <summary>
/// Declares that the method requires the given feature flag code to be present and isEnabled = true
/// in <c>AuthorizationGraph.featureFlags[]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiresFeatureFlagAttribute : RequiresAuthorizationAttribute
{
    public string FlagCode { get; }

    public RequiresFeatureFlagAttribute(string flagCode)
    {
        FlagCode = flagCode;
    }

    public override string Primitive => "RequiresFeatureFlag";
    public override string Target => FlagCode;
}
