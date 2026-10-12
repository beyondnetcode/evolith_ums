using Evolith.Ums.Globalization;
using Evolith.Ums.Presentation.Bootstrapping;
using MediatR;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

public sealed class UmsApiServiceBootstrappersTests
{
    [Fact]
    public void AddUmsApiServiceBootstrappers_ShouldRegisterCoreServices()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();

        services.AddUmsApiServiceBootstrappers(configuration, new FakeHostEnvironment());

        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IMediator));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ILocalizationService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IRequestContext));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(HealthCheckService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ICorsService));
    }

    [Fact]
    public void AddUmsApiServiceBootstrappers_ShouldRegisterPlatformPolicies()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();

        services.AddUmsApiServiceBootstrappers(configuration, new FakeHostEnvironment());

        // G-248: ya NO se registra `RateLimiterOptions`. El limitador de ASP.NET era en proceso
        // —con N réplicas el cupo efectivo era N veces el declarado— y corría antes de autenticar,
        // así que repartía por IP y dos usuarios distintos compartían cupo. Lo sustituye
        // `LimiteDePeticionesMiddleware` sobre `ILimitadorDePeticiones`, que se registra en la capa
        // de infraestructura junto al resto del estado compartido. Se comprueba su AUSENCIA: si
        // alguien vuelve a añadir `AddRateLimiter`, habrá dos limitadores contando lo mismo.
        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(IConfigureOptions<RateLimiterOptions>));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IConfigureOptions<CorsOptions>));
    }

    [Fact]
    public void AddUmsApiServiceBootstrappers_FueraDeDesarrolloSinClaveDeFirma_FallaAlComponer()
    {
        // G-199: UMS firma y valida sus propios tokens en RS256 con `Jwt:PrivateKeyPem`. Fuera de
        // desarrollo no hay par efímero de respaldo, así que la composición falla en el arranque
        // en vez de arrancar «a medias» y descubrirlo en la primera petición.
        //
        // El entorno importa: en Development la clave se genera efímera y componer SÍ funciona.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedOrigins"] = "https://localhost:3000",
                ["Persistence:Provider"] = "InMemory",
            })
            .Build();

        var entorno = new FakeHostEnvironment { EnvironmentName = Environments.Production };

        var componer = () => services.AddUmsApiServiceBootstrappers(configuration, entorno);

        componer.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:PrivateKeyPem*");
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedOrigins"] = "https://localhost:3000",
                ["Persistence:Provider"] = "InMemory",
            })
            .Build();
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Evolith.Ums.Presentation.IntegrationTest";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
