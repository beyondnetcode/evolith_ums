using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Evolith.Ums.Sdk.Authorization.AspNetCore;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Plugs <see cref="UmsAuthGraphMiddleware"/> into the pipeline with default options.
    /// </summary>
    public static IApplicationBuilder UseUmsAuthGraph(this IApplicationBuilder app) =>
        app.UseMiddleware<UmsAuthGraphMiddleware>();

    /// <summary>
    /// Plugs <see cref="UmsAuthGraphMiddleware"/> with an inline configuration callback.
    /// </summary>
    public static IApplicationBuilder UseUmsAuthGraph(this IApplicationBuilder app, Action<UmsAuthGraphMiddlewareOptions> configure)
    {
        var monitor = app.ApplicationServices.GetService<IOptionsMonitor<UmsAuthGraphMiddlewareOptions>>();
        if (monitor is not null) configure(monitor.CurrentValue);
        return app.UseMiddleware<UmsAuthGraphMiddleware>();
    }

    /// <summary>Registers the middleware options for DI.</summary>
    public static IServiceCollection AddUmsAuthGraphMiddleware(this IServiceCollection services)
    {
        services.AddOptions<UmsAuthGraphMiddlewareOptions>();
        return services;
    }
}
