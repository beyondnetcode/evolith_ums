namespace Evolith.Ums.Sdk.Authorization.Aop;

/// <summary>
/// Declares that the method requires the given menu option code to resolve to
/// <c>AccessEffect.Allow</c> in <c>AuthorizationGraph.menuAccess[].menus[].subMenus[].options[]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiresMenuOptionAttribute : RequiresAuthorizationAttribute
{
    public string OptionCode { get; }

    public RequiresMenuOptionAttribute(string optionCode)
    {
        OptionCode = optionCode;
    }

    public override string Primitive => "RequiresMenuOption";
    public override string Target => OptionCode;
}
