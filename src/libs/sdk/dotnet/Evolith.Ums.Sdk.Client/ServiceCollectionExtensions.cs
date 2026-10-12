using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Evolith.Ums.Sdk.Client;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a typed <see cref="IUmsAuthClient"/> backed by HttpClientFactory.
    /// Configure the base address through <paramref name="configure"/>.
    /// </summary>
    public static IServiceCollection AddUmsSdkClient(this IServiceCollection services, Action<UmsSdkClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        services.AddOptions<UmsSdkClientOptions>().Configure(configure);
        services.AddHttpClient<IUmsAuthClient, UmsAuthClient>();
        services.TryAddSingleton<IUmsAuthClient>(sp => sp.GetRequiredService<UmsAuthClient>());
        return services;
    }
}
