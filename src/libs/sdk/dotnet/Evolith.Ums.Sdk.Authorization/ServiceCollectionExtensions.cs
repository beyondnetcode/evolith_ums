using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Evolith.Ums.Sdk.Authorization;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the pure <see cref="IAuthorizationValidator"/> as a singleton and binds default
    /// <see cref="AuthorizationOptions"/>. Does NOT register an accessor — call
    /// <see cref="AddHttpContextAuthGraphAccessor"/> for ASP.NET Core apps or register a custom
    /// <see cref="IAuthGraphAccessor"/> for workers/CLI.
    /// </summary>
    public static IServiceCollection AddUmsSdkAuthorization(this IServiceCollection services)
    {
        services.TryAddSingleton<IAuthorizationValidator, AuthorizationValidator>();
        services.AddOptions<AuthorizationOptions>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="HttpContextAuthGraphAccessor"/> as the scoped accessor.
    /// The consumer is responsible for calling <c>services.AddHttpContextAccessor()</c>
    /// (from <c>Microsoft.AspNetCore.Http</c>) — kept out of this package to avoid pulling
    /// the full ASP.NET Core hosting stack into non-web consumers.
    /// </summary>
    public static IServiceCollection AddHttpContextAuthGraphAccessor(this IServiceCollection services)
    {
        services.TryAddScoped<IAuthGraphAccessor, HttpContextAuthGraphAccessor>();
        return services;
    }
}
