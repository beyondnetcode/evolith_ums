namespace Evolith.Ums.Presentation.Bootstrapping.Bootstrappers;

using BeyondNetCode.Shell.Bootstrapper.Interface;
using Evolith.Ums.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class ConfigurationBootstrapper : IBootstrapper<IServiceCollection>
{
    public ConfigurationBootstrapper(IServiceCollection services)
    {
        Result = services;
    }

    public IServiceCollection? Result { get; private set; }

    public void Run()
    {
        ArgumentNullException.ThrowIfNull(Result);
        Result!.AddConfigurationProvider();
    }
}