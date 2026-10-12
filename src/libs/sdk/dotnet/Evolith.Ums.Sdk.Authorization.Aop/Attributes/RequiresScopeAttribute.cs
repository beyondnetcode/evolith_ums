namespace Evolith.Ums.Sdk.Authorization.Aop;

/// <summary>
/// Declares that the method requires the given OAuth2-style scope (<c>"RESOURCE.ACTION"</c>)
/// to be present (and not denied) in the user's <c>AuthorizationGraph.scopes[]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiresScopeAttribute : RequiresAuthorizationAttribute
{
    public string Scope { get; }

    public RequiresScopeAttribute(string scope)
    {
        Scope = scope;
    }

    public override string Primitive => "RequiresScope";
    public override string Target => Scope;
}
