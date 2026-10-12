namespace Evolith.Ums.Sdk.Authorization.Aop;

/// <summary>
/// Declares that the method requires Allow on the given (resourceCode, actionCode) pair in
/// <c>AuthorizationGraph.domainPermissions[]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiresDomainAccessAttribute : RequiresAuthorizationAttribute
{
    public string ResourceCode { get; }
    public string ActionCode { get; }

    public RequiresDomainAccessAttribute(string resourceCode, string actionCode)
    {
        ResourceCode = resourceCode;
        ActionCode = actionCode;
    }

    public override string Primitive => "RequiresDomainAccess";
    public override string Target => $"{ResourceCode}.{ActionCode}";
}
