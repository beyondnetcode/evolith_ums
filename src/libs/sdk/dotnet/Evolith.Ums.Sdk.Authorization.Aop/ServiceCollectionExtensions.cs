using BeyondNetCode.Shell.Aop.Microsoft.Extensions.DependencyInjection.Aspects.Installer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Evolith.Ums.Sdk.Authorization.Aop;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Convenience: registers the validator + ASP.NET accessor + AOP aspect in one call.
    /// Equivalent to:
    /// <code>
    /// services.AddUmsSdkAuthorization();
    /// services.AddHttpContextAuthGraphAccessor();
    /// services.AddAop(b =&gt; b.AddAspect&lt;AuthorizationAspect&gt;());
    /// </code>
    /// </summary>
    public static IServiceCollection AddUmsSdkAuthorizationWithAop(this IServiceCollection services)
    {
        services.AddUmsSdkAuthorization();
        services.AddHttpContextAuthGraphAccessor();
        services.AddAop(builder => builder.AddAspect<AuthorizationAspect>());
        return services;
    }
}
