namespace Evolith.Ums.Infrastructure.Configuration;

using Evolith.Ums.Application.Configuration.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class ConfigurationLoaderHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public ConfigurationLoaderHostedService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IConfigurationProvider>();
        await provider.LoadAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class ConfigurationLoaderExtensions
{
    public static IServiceCollection AddConfigurationProvider(this IServiceCollection services)
    {
        // El caché se inyecta dinámicamente en DependencyInjection.cs
        // (RedisConfigurationCache si hay cadena de conexión, sino InMemoryConfigurationCache).
        services.AddSingleton<IConfigurationProvider, ConfigurationProvider>();
        services.AddHostedService<ConfigurationLoaderHostedService>();
        return services;
    }
}
