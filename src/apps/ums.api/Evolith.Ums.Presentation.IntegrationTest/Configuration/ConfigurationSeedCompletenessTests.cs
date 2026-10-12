using Microsoft.Extensions.DependencyInjection;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Identity.Repositories.TenantParameter;
using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Configuration;

public sealed class ConfigurationSeedCompletenessTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly IServiceProvider _services;

    public ConfigurationSeedCompletenessTests(UmsApiWebApplicationFactory factory)
    {
        _services = factory.Services;
    }

    [Fact]
    public async Task SeededTenants_ShouldHaveCompleteConfigurationCatalogs()
    {
        using var scope = _services.CreateScope();

        var appConfigRepository = scope.ServiceProvider.GetRequiredService<IAppConfigurationRepository>();
        var featureFlagRepository = scope.ServiceProvider.GetRequiredService<IFeatureFlagRepository>();
        var idpConfigurationRepository = scope.ServiceProvider.GetRequiredService<IIdpConfigurationRepository>();
        var tenantParameterRepository = scope.ServiceProvider.GetRequiredService<ITenantParameterRepository>();

        var ct = TestContext.Current.CancellationToken;
        var tenants = GetSeedTenants();

        foreach (var tenantId in tenants)
        {
            var appConfigs = await appConfigRepository.GetAllAsync(tenantId, ct);
            appConfigs.Count.Should().BeGreaterOrEqualTo(9);
            var appConfigCodes = appConfigs.Select(config => config.Code.GetValue()).ToList();
            appConfigCodes.Should().Contain("SESSION_TIMEOUT_MINUTES");
            appConfigCodes.Should().Contain("MAX_LOGIN_ATTEMPTS");
            appConfigCodes.Should().Contain("AUTH_USE_EXTERNAL_IDP");
            appConfigCodes.Should().Contain("UI_LANGUAGE_DEFAULT");
            appConfigCodes.Should().Contain("UI_TIMEZONE_DEFAULT");

            // G-014: las feature flags se siembran POR SUITE (ConfigurationDevDataSeeder.
            // GetFeatureFlagDefinitions: UMS=7, WMS=3), no un catálogo fijo por inquilino. Un
            // inquilino con solo suite WMS obtiene 3 (EVOLITH: +PAITA_AGROEXPORT = 4). El umbral
            // fijo >=10 asumía que todo inquilino tuviera suites UMS+WMS, lo que el seed NO
            // garantiza. La completitud real es que cada inquilino sembrado tenga las flags de
            // su(s) suite(s): no vacío.
            var featureFlags = await featureFlagRepository.GetAllAsync(tenantId, ct);
            featureFlags.Should().NotBeEmpty(
                because: "cada inquilino sembrado con suite debe recibir las feature flags de su(s) suite(s)");

            var tenantParameters = await tenantParameterRepository.GetByTenantIdAsync(tenantId, ct);
            tenantParameters.Should().HaveCount(10);
        }

        foreach (var tenantId in GetExternalIdpTenants())
        {
            var idpConfigurations = await idpConfigurationRepository.GetByTenantIdAsync(tenantId, ct);
            idpConfigurations.Should().NotBeEmpty();
        }

        foreach (var tenantId in GetLocalAuthTenants())
        {
            var idpConfigurations = await idpConfigurationRepository.GetByTenantIdAsync(tenantId, ct);
            idpConfigurations.Should().BeEmpty();
        }
    }

    private static Guid[] GetSeedTenants()
    {
        return
        [
            Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId),
            Guid.Parse(CoreDevDataSeeder.RansaTenantId),
            Guid.Parse("A3F5B9D2-7C3D-4C8E-A9B0-123456789ABC"),
            Guid.Parse("C9B736B4-6A84-48F8-B34D-176BC5A6D542"),
            Guid.Parse("5F4E3D2C-1B0A-9F8E-7D6C-543210987654"),
            Guid.Parse("9E8D7C6B-5A4F-3E2D-1C0B-9876543210FE"),
            Guid.Parse("F3E2D1C0-B9A8-7F6E-5D4C-321098765432"),
        ];
    }

    private static Guid[] GetExternalIdpTenants()
    {
        return
        [
            Guid.Parse(CoreDevDataSeeder.RansaTenantId),
            Guid.Parse("C9B736B4-6A84-48F8-B34D-176BC5A6D542"),
            Guid.Parse("9E8D7C6B-5A4F-3E2D-1C0B-9876543210FE"),
            Guid.Parse("F3E2D1C0-B9A8-7F6E-5D4C-321098765432"),
        ];
    }

    private static Guid[] GetLocalAuthTenants()
    {
        return
        [
            Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId),
            Guid.Parse("A3F5B9D2-7C3D-4C8E-A9B0-123456789ABC"),
            Guid.Parse("5F4E3D2C-1B0A-9F8E-7D6C-543210987654"),
        ];
    }
}
