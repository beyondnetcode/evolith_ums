namespace Evolith.Ums.Infrastructure.Persistence.Seeders;

using System.Reflection;
using BeyondNetCode.Shell.Ddd;
using Microsoft.Extensions.DependencyInjection;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.AppConfiguration;
using Evolith.Ums.Domain.Configuration.FeatureFlag;
using Evolith.Ums.Domain.Configuration.IdpConfiguration;
using Evolith.Ums.Domain.Identity.Repositories.TenantParameter;
using Evolith.Ums.Domain.Identity.Tenant.TenantParameter;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;
using FeatureFlagAggregate = Evolith.Ums.Domain.Configuration.FeatureFlag.FeatureFlag;
using IdpConfigurationAggregate = Evolith.Ums.Domain.Configuration.IdpConfiguration.IdpConfiguration;
using SystemSuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;
using TenantParameterAggregate = Evolith.Ums.Domain.Identity.Tenant.TenantParameter.TenantParameter;

public static class ConfigurationDevDataSeeder
{
    private const string TestIdpSystemSuiteId = "11111111-1111-1111-1111-111111111111";
    private const string PaitaAgroexportFlagCode = "PAITA_AGROEXPORT";
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada en seeder de desarrollo/UAT. La siembra determinista con " +
                        "IDs bien conocidos exige fijar identidades y estados que el dominio no permite por vías " +
                        "públicas. No se ejecuta en producción (SeedDevData && !IsProduction).")]
    private static readonly BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Guid DemoSystemSuiteGuid = Guid.Parse(CoreDevDataSeeder.DemoSystemSuiteId);
    private static readonly Guid RansaTenantGuid = Guid.Parse(CoreDevDataSeeder.RansaTenantId);
    private static readonly Guid InternalAdminTenantGuid = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId);
    private static readonly Guid ApmTenantGuid = Guid.Parse("A3F5B9D2-7C3D-4C8E-A9B0-123456789ABC");
    private static readonly Guid NeptuniaTenantGuid = Guid.Parse("C9B736B4-6A84-48F8-B34D-176BC5A6D542");
    private static readonly Guid EvolithTenantGuid = Guid.Parse("5F4E3D2C-1B0A-9F8E-7D6C-543210987654");
    private static readonly Guid PaitaTenantGuid = Guid.Parse("9E8D7C6B-5A4F-3E2D-1C0B-9876543210FE");
    private static readonly Guid IntradevcoTenantGuid = Guid.Parse("F3E2D1C0-B9A8-7F6E-5D4C-321098765432");

    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var appConfigRepository = serviceProvider.GetService<IAppConfigurationRepository>();
        var featureFlagRepository = serviceProvider.GetService<IFeatureFlagRepository>();
        var idpConfigRepository = serviceProvider.GetService<IIdpConfigurationRepository>();
        var tenantParameterRepository = serviceProvider.GetService<ITenantParameterRepository>();
        var suiteRepository = serviceProvider.GetService<ISystemSuiteRepository>();

        var actor = ActorId.Create(CoreDevDataSeeder.SystemActorId);

        await SeedAppConfigurationsAsync(appConfigRepository, BuildSeedAppConfigurations(actor).Concat(BuildTenantSpecificConfigurations(actor)).ToList(), cancellationToken);
        await SeedFeatureFlagsAsync(featureFlagRepository, suiteRepository, actor, cancellationToken);
        await SeedIdpConfigsAsync(idpConfigRepository, suiteRepository, actor, cancellationToken);
        await SeedTenantParametersAsync(tenantParameterRepository, actor, cancellationToken);
    }

    internal static async Task SeedAppConfigurationsAsync(
        IAppConfigurationRepository? repository,
        IReadOnlyList<AppConfigurationAggregate> desiredConfigs,
        CancellationToken cancellationToken)
    {
        if (repository is null)
        {
            return;
        }

        var needsSave = false;

        // NOTA: `GetByScopeAndCodeAsync` solo ve las configuraciones VIVAS desde que la ranura se
        // libera al borrar. Consecuencia deliberada: si en un entorno de desarrollo alguien elimina
        // una configuración sembrada, el siguiente arranque la vuelve a crear —con id nuevo, junto a
        // la lápida—. Es el contrato de un sembrado: garantizar la línea base del entorno. No afecta
        // a producción, donde este sembrador no corre (SeedDevData && !IsProduction).
        foreach (var config in desiredConfigs)
        {
            var existing = await repository.GetByScopeAndCodeAsync(
                config.Props.TenantId?.GetValue(),
                config.Props.SystemSuiteId?.GetValue(),
                config.Props.ModuleId?.GetValue(),
                config.Code.GetValue(),
                cancellationToken);

            if (existing is not null)
            {
                continue;
            }

            await repository.AddAsync(config, cancellationToken);
            needsSave = true;
        }

        if (needsSave)
        {
            await repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        }
    }

    // AppConfiguration.Create(TenantId?, SystemSuiteId?, IdValueObject? moduleId, Code, ConfigurationValue, Description, bool isInheritable, bool isEncrypted, ActorId)
    /// <summary>
    /// Configuraciones GLOBALES de la plataforma (ámbito sin inquilino). Son datos de REFERENCIA,
    /// no de demostración: sin ellas UMS no puede ni autenticar. Verificado el 2026-08-15 sobre una
    /// base recién migrada — el login falla con
    /// <c>REQUIRED_PARAMETER_NOT_CONFIGURED: 'AUTH_USE_EXTERNAL_IDP'</c>, que el manejador colapsa a
    /// un 401 genérico.
    ///
    /// <para><c>internal</c> y no <c>private</c> porque <see cref="PlatformBootstrapper"/> también
    /// las necesita: el arranque en frío las siembra en TODO entorno, producción incluida, mientras
    /// que el resto de este sembrador sigue restringido a <c>SeedDevData &amp;&amp; !IsProduction</c>.</para>
    /// </summary>
    internal static IReadOnlyList<AppConfigurationAggregate> BuildSeedAppConfigurations(ActorId actor)
    {
        var results = new List<AppConfigurationAggregate>();

        // SESSION_TIMEOUT_MINUTES must come first - ConfigurationRestEndpointTests checks items[0].code
        AddPublishedConfiguration(results,
            null,
            Code.Create(AppConfigurationCodes.SessionTimeoutMinutes),
            ConfigurationValue.Create(AppConfigurationDefaults.SessionTimeoutMinutes.ToString()),
            Description.Create("Idle session timeout in minutes"),
            actor);

        AddPublishedConfiguration(results,
            null,
            Code.Create(AppConfigurationCodes.MaxLoginAttempts),
            ConfigurationValue.Create(AppConfigurationDefaults.MaxLoginAttempts.ToString()),
            Description.Create("Maximum login attempts before lockout"),
            actor);

        // ADR-UMS-095: duración del bloqueo temporal por intentos fallidos (nivel Global).
        AddPublishedConfiguration(results,
            null,
            Code.Create(AppConfigurationCodes.AccountLockoutDurationMinutes),
            ConfigurationValue.Create(AppConfigurationDefaults.AccountLockoutDurationMinutes.ToString()),
            Description.Create("Temporary account lockout duration in minutes after reaching the max login attempts"),
            actor);

        AddPublishedConfiguration(results,
            null,
            Code.Create(AppConfigurationCodes.AccessTokenDurationMs),
            ConfigurationValue.Create(AppConfigurationDefaults.AccessTokenDurationMs.ToString()),
            Description.Create("Access token lifetime in milliseconds (default: 1 hour)"),
            actor);

        AddPublishedConfiguration(results,
            null,
            Code.Create(AppConfigurationCodes.RefreshTokenDurationMs),
            ConfigurationValue.Create(AppConfigurationDefaults.RefreshTokenDurationMs.ToString()),
            Description.Create("Refresh token lifetime in milliseconds (default: 7 days)"),
            actor);

        AddPublishedConfiguration(results,
            null,
            Code.Create(AppConfigurationCodes.MinPasswordLength),
            ConfigurationValue.Create(AppConfigurationDefaults.MinPasswordLength.ToString()),
            Description.Create("Minimum required password length"),
            actor);

        AddPublishedConfiguration(results,
            null,
            Code.Create(AppConfigurationCodes.MaxValidityPeriodDays),
            ConfigurationValue.Create(AppConfigurationDefaults.MaxValidityPeriodDays.ToString()),
            Description.Create("Maximum user account validity period in days"),
            actor);

        AddPublishedConfiguration(results,
            null,
            Code.Create(AppConfigurationCodes.FrontendConfigTransport),
            ConfigurationValue.Create(AppConfigurationDefaults.FrontendConfigTransport),
            Description.Create("Transport mode for frontend config queries (REST only)"),
            actor);

        AddPublishedConfiguration(results,
            null,
            Code.Create(AppConfigurationCodes.AuthUseExternalIdp),
            ConfigurationValue.Create(AppConfigurationDefaults.AuthUseExternalIdp.ToString().ToLowerInvariant()),
            Description.Create("Whether the tenant uses external Identity Providers instead of local password credentials"),
            actor);

        AddPublishedConfiguration(results,
            null,
            Code.Create(AppConfigurationCodes.MfaAllowedMethods),
            ConfigurationValue.Create(AppConfigurationDefaults.MfaAllowedMethods),
            Description.Create("Comma-separated list of allowed MFA methods; tenants can override this value to restrict their security factors"),
            actor);

        AddPublishedConfiguration(results,
            null,
            Code.Create(AppConfigurationCodes.UiLanguageDefault),
            ConfigurationValue.Create(AppConfigurationDefaults.UiLanguageDefault),
            Description.Create("Default UI language for the platform"),
            actor);

        AddPublishedConfiguration(results,
            null,
            Code.Create(AppConfigurationCodes.UiTimezoneDefault),
            ConfigurationValue.Create(AppConfigurationDefaults.UiTimezoneDefault),
            Description.Create("Default UI timezone for the platform"),
            actor);

        return results;
    }

    private static IReadOnlyList<AppConfigurationAggregate> BuildTenantSpecificConfigurations(ActorId actor)
    {
        var results = new List<AppConfigurationAggregate>();

        foreach (var profile in GetTenantSeedProfiles())
        {
            var tenantId = TenantId.Load(profile.TenantId);

            AddPublishedConfiguration(results,
                tenantId,
                Code.Create(AppConfigurationCodes.SessionTimeoutMinutes),
                ConfigurationValue.Create(profile.SessionTimeoutMinutes),
                Description.Create($"Session timeout for {profile.Name}"),
                actor);

            AddPublishedConfiguration(results,
                tenantId,
                Code.Create(AppConfigurationCodes.MaxLoginAttempts),
                ConfigurationValue.Create(profile.MaxLoginAttempts),
                Description.Create($"Login attempt limit for {profile.Name}"),
                actor);

            // ADR-UMS-095: duración del bloqueo temporal por intentos fallidos (nivel Tenant).
            AddPublishedConfiguration(results,
                tenantId,
                Code.Create(AppConfigurationCodes.AccountLockoutDurationMinutes),
                ConfigurationValue.Create(profile.AccountLockoutDurationMinutes),
                Description.Create($"Temporary account lockout duration in minutes for {profile.Name}"),
                actor);

            AddPublishedConfiguration(results,
                tenantId,
                Code.Create(AppConfigurationCodes.MinPasswordLength),
                ConfigurationValue.Create(profile.MinPasswordLength),
                Description.Create($"Minimum password length for {profile.Name}"),
                actor);

            AddPublishedConfiguration(results,
                tenantId,
                Code.Create(AppConfigurationCodes.MaxValidityPeriodDays),
                ConfigurationValue.Create(profile.MaxValidityPeriodDays),
                Description.Create($"Maximum user validity period for {profile.Name}"),
                actor);

            AddPublishedConfiguration(results,
                tenantId,
                Code.Create(AppConfigurationCodes.MfaRequiredForAdmin),
                ConfigurationValue.Create(profile.MfaRequiredForAdmin),
                Description.Create($"Whether verified MFA is required for {profile.Name} logins"),
                actor);

            AddPublishedConfiguration(results,
                tenantId,
                Code.Create(AppConfigurationCodes.MfaAllowedMethods),
                ConfigurationValue.Create(profile.MfaAllowedMethods),
                Description.Create($"Allowed MFA methods for {profile.Name}"),
                actor);

            AddPublishedConfiguration(results,
                tenantId,
                Code.Create(AppConfigurationCodes.UiCustomBrandingEnabled),
                ConfigurationValue.Create(profile.CustomBrandingEnabled),
                Description.Create($"Whether custom branding is enabled for {profile.Name}"),
                actor);

            AddPublishedConfiguration(results,
                tenantId,
                Code.Create(AppConfigurationCodes.AuthUseExternalIdp),
                ConfigurationValue.Create(profile.AuthUseExternalIdp),
                Description.Create($"Whether {profile.Name} uses an external Identity Provider"),
                actor);

            AddPublishedConfiguration(results,
                tenantId,
                Code.Create(AppConfigurationCodes.UiLanguageDefault),
                ConfigurationValue.Create(profile.DefaultLanguage),
                Description.Create($"Default UI language for {profile.Name}"),
                actor);

            AddPublishedConfiguration(results,
                tenantId,
                Code.Create(AppConfigurationCodes.UiTimezoneDefault),
                ConfigurationValue.Create(profile.DefaultTimezone),
                Description.Create($"Default UI timezone for {profile.Name}"),
                actor);
        }

        return results;
    }

    private static void AddPublishedConfiguration(
        ICollection<AppConfigurationAggregate> results,
        TenantId? tenantId,
        Code code,
        ConfigurationValue value,
        Description description,
        ActorId actor)
    {
        var configResult = AppConfigurationAggregate.Create(
            tenantId,
            null,
            null,
            code,
            value,
            description,
            true,
            false,
            actor);

        if (configResult.IsFailure)
        {
            return;
        }

        var config = configResult.Value;
        var publishResult = config.Publish(actor);
        if (publishResult.IsSuccess)
        {
            config.DomainEvents.MarkChangesAsCommitted();
        }

        results.Add(config);
    }

    private static async Task SeedFeatureFlagsAsync(
        IFeatureFlagRepository? repository,
        ISystemSuiteRepository? suiteRepository,
        ActorId actor,
        CancellationToken cancellationToken)
    {
        if (repository is null || suiteRepository is null)
        {
            return;
        }

        var suites = await suiteRepository.GetAllAsync(null, cancellationToken);
        if (suites.Count == 0)
        {
            return;
        }

        var existingFlags = await repository.GetAllAsync(null, cancellationToken);
        var existingByKey = existingFlags
            .GroupBy(flag => (flag.SystemSuiteId, flag.FlagCode))
            .ToDictionary(group => group.Key, group => group.First());

        var needsSave = false;

        foreach (var suite in suites)
        {
            var suiteId = suite.GetId().GetValue();
            var tenantId = suite.TenantId.GetValue();

            foreach (var definition in GetFeatureFlagDefinitions(suite.Code.GetValue()))
            {
                if (existingByKey.TryGetValue((suiteId, definition.Code), out var existing))
                {
                    if (existing.Props.TenantId?.GetValue() != tenantId)
                    {
                        SetFeatureFlagTenantId(existing, tenantId);
                        await repository.UpdateAsync(existing, cancellationToken);
                        needsSave = true;
                    }
                    continue;
                }

                var flagResult = FeatureFlagAggregate.Create(
                    IdValueObject.Load(suiteId),
                    IdValueObject.Load(tenantId),
                    definition.Code,
                    definition.Type,
                    definition.Targets,
                    null,
                    null,
                    definition.RolloutPercentage,
                    actor);

                if (flagResult.IsFailure)
                {
                    continue;
                }

                ApplyFeatureFlagDemoRules(flagResult.Value, definition, actor);
                await repository.AddAsync(flagResult.Value, cancellationToken);
                existingByKey[(suiteId, definition.Code)] = flagResult.Value;
                needsSave = true;
            }
        }

        // FS-25 §4.5 / criterio 12: bandera de agroexportación acotada al tenant EVOLITH (sucursal Paita).
        needsSave = await SeedEvolithAgroexportFlagAsync(repository, suites, existingByKey, actor, cancellationToken) || needsSave;

        if (needsSave)
        {
            await repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        }
    }

    private static async Task<bool> SeedEvolithAgroexportFlagAsync(
        IFeatureFlagRepository repository,
        IReadOnlyList<SystemSuiteAggregate> suites,
        IDictionary<(Guid SystemSuiteId, string FlagCode), FeatureFlagAggregate> existingByKey,
        ActorId actor,
        CancellationToken cancellationToken)
    {
        var anchorSuiteId = ResolveEvolithAnchorSuiteId(suites);
        if (anchorSuiteId is null || existingByKey.ContainsKey((anchorSuiteId.Value, PaitaAgroexportFlagCode)))
        {
            return false;
        }

        var paitaFlag = BuildEvolithAgroexportFlag(anchorSuiteId.Value, actor);
        if (paitaFlag is null)
        {
            return false;
        }

        await repository.AddAsync(paitaFlag, cancellationToken);
        existingByKey[(anchorSuiteId.Value, PaitaAgroexportFlagCode)] = paitaFlag;
        return true;
    }

    private static Guid? ResolveEvolithAnchorSuiteId(IReadOnlyList<SystemSuiteAggregate> suites)
    {
        var evolithSuites = suites.Where(suite => suite.TenantId.GetValue() == EvolithTenantGuid).ToList();
        var anchor = evolithSuites.FirstOrDefault(suite => suite.Code.GetValue() == "WMS")
            ?? evolithSuites.FirstOrDefault();
        return anchor?.GetId().GetValue();
    }

    private static FeatureFlagAggregate? BuildEvolithAgroexportFlag(Guid anchorSuiteId, ActorId actor)
    {
        var flagResult = FeatureFlagAggregate.Create(
            IdValueObject.Load(anchorSuiteId),
            IdValueObject.Load(EvolithTenantGuid),
            PaitaAgroexportFlagCode,
            FlagType.Boolean,
            "*",
            null,
            null,
            null,
            actor);

        if (flagResult.IsFailure)
        {
            return null;
        }

        var flag = flagResult.Value;
        flag.AddCriteria("TenantId", "Equals", CoreDevDataSeeder.EvolithTenantId, actor);
        flag.DomainEvents.MarkChangesAsCommitted();
        flag.Activate(actor);
        flag.DomainEvents.MarkChangesAsCommitted();
        return flag;
    }

    private static void ApplyFeatureFlagDemoRules(
        FeatureFlagAggregate flag,
        FeatureFlagSeedDefinition definition,
        ActorId actor)
    {
        if (definition.Code is "DARK_MODE" or "BULK_PICKING")
        {
            flag.Activate(actor);
            flag.DomainEvents.MarkChangesAsCommitted();
        }

        if (definition.Targets.Contains("role:", StringComparison.OrdinalIgnoreCase))
        {
            var roleCode = definition.Targets.Split(':')[1].Split(',')[0];
            flag.AddCriteria("RoleCode", "Equals", roleCode, actor);
            flag.DomainEvents.MarkChangesAsCommitted();
        }

        if (definition.Code == "ENABLE_MFA")
        {
            flag.AddCriteria("TenantId", "Equals", CoreDevDataSeeder.RansaTenantId, actor);
            flag.DomainEvents.MarkChangesAsCommitted();
            flag.Activate(actor);
            flag.DomainEvents.MarkChangesAsCommitted();
        }

        if (definition.Code == "NEW_AUDIT_DASHBOARD")
        {
            flag.AddCriteria("DateRange", "Between",
                "{\"from\":\"2026-01-01T00:00:00Z\",\"to\":\"2026-12-31T23:59:59Z\"}", actor);
            flag.DomainEvents.MarkChangesAsCommitted();
            flag.Activate(actor);
            flag.DomainEvents.MarkChangesAsCommitted();
        }

        if (definition.Code == "PREMIUM_REPORTS")
        {
            flag.AddCriteria("TenantId", "Equals", CoreDevDataSeeder.RansaTenantId, actor);
            flag.AddCriteria("RoleCode", "In", "[\"ADMIN\",\"SUPERVISOR\"]", actor);
            flag.DomainEvents.MarkChangesAsCommitted();
            flag.Activate(actor);
            flag.DomainEvents.MarkChangesAsCommitted();
        }
    }

    private static IReadOnlyList<FeatureFlagSeedDefinition> GetFeatureFlagDefinitions(string suiteCode)
    {
        return suiteCode switch
        {
            "UMS" => new[]
            {
                new FeatureFlagSeedDefinition("ENABLE_MFA", FlagType.Boolean, "*", "Multi-factor authentication for logistics users", null),
                new FeatureFlagSeedDefinition("DARK_MODE", FlagType.Boolean, "*", "Dark mode UI toggle", null),
                new FeatureFlagSeedDefinition("ADVANCED_REPORTING", FlagType.Boolean, "role:ADMIN,role:SUPERVISOR", "Advanced analytics dashboard", null),
                new FeatureFlagSeedDefinition("ALLOW_PASSWORD_RESET_BY_ADMIN", FlagType.Boolean, "*", "Allow admin to reset user passwords", null),
                new FeatureFlagSeedDefinition("ALLOW_VALIDITY_PERIOD_MODIFICATION", FlagType.Boolean, "*", "Allow admin to modify user validity periods", null),
                new FeatureFlagSeedDefinition("NEW_AUDIT_DASHBOARD", FlagType.Boolean, "*", "New audit trail visualization (2026 rollout)", null),
                new FeatureFlagSeedDefinition("PREMIUM_REPORTS", FlagType.Boolean, "*", "Premium analytics reports — Ransa admins only", null),
            },
            "WMS" => new[]
            {
                new FeatureFlagSeedDefinition("BULK_PICKING", FlagType.Boolean, "*", "Bulk picking workflow", null),
                new FeatureFlagSeedDefinition("VOICE_PICKING", FlagType.Boolean, "role:OPERATOR", "Voice-directed picking", null),
                new FeatureFlagSeedDefinition("AUTO_REORDER", FlagType.Percentage, "*", "Automatic reorder point calculation", 50),
            },
            _ => Array.Empty<FeatureFlagSeedDefinition>()
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada en seeder de desarrollo/UAT. Fija el TenantId del feature " +
                        "flag semilla sobre props no públicos; no se ejecuta en producción (SeedDevData && !IsProduction).")]
    private static void SetFeatureFlagTenantId(FeatureFlagAggregate featureFlag, Guid tenantId)
    {
        var propsField = typeof(FeatureFlagAggregate).GetField("_props", PrivateInstanceFlags);
        var props = propsField?.GetValue(featureFlag);
        var tenantIdProperty = props?.GetType().GetProperty("TenantId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        tenantIdProperty?.SetValue(props, IdValueObject.Load(tenantId));
    }

    private static async Task SeedIdpConfigsAsync(
        IIdpConfigurationRepository? repository,
        ISystemSuiteRepository? suiteRepository,
        ActorId actor,
        CancellationToken cancellationToken)
    {
        if (repository is null)
        {
            return;
        }

        var needsSave = false;

        foreach (var profile in GetTenantSeedProfiles().Where(profile => profile.SeedExternalIdp))
        {
            var tenantId = profile.TenantId;
            var existing = await repository.GetByTenantIdAsync(tenantId, cancellationToken);
            var desiredDefinitions = await BuildIdpSeedDefinitions(profile, suiteRepository, actor, cancellationToken);

            foreach (var definition in desiredDefinitions)
            {
                var found = existing.FirstOrDefault(item =>
                    item.SystemSuiteId.GetValue() == definition.SystemSuiteId &&
                    item.ProviderType == definition.ProviderType &&
                    item.Props.DomainHints.SequenceEqual(definition.DomainHints));

                if (found is not null)
                {
                    continue;
                }

                await repository.AddAsync(definition.Configuration, cancellationToken);
                needsSave = true;
            }
        }

        if (needsSave)
        {
            await repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<IdpSeedDefinition>> BuildIdpSeedDefinitions(
        TenantSeedProfile profile,
        ISystemSuiteRepository? suiteRepository,
        ActorId actor,
        CancellationToken cancellationToken)
    {
        var results = new List<IdpSeedDefinition>();
        var suiteId = await ResolvePrimarySuiteIdAsync(profile.TenantId, suiteRepository, cancellationToken);

        if (profile.TenantId == RansaTenantGuid)
        {
            AddIdpSeedDefinition(
                results,
                profile.TenantId,
                suiteId,
                ProviderType.AzureAd,
                new[] { "ransa.pe", "ransa.com.pe" },
                "{\"clientId\":\"1234\",\"tenantId\":\"abcd-azure-tenant\"}",
                "keyvault://ums/ransa-entra-secret",
                1,
                null,
                actor);

            AddIdpSeedDefinition(
                results,
                profile.TenantId,
                Guid.Parse(TestIdpSystemSuiteId),
                ProviderType.AzureAd,
                new[] { "beyondnet.com" },
                "{\"authority\":\"https://login.microsoftonline.com/common\",\"clientId\":\"test-client-id\"}",
                "keyvault://ums/beyondnet-entra-secret",
                1,
                null,
                actor);

            return results;
        }

        AddIdpSeedDefinition(
            results,
            profile.TenantId,
            suiteId,
            ProviderType.AzureAd,
            profile.ExternalIdpDomainHints,
            profile.ExternalIdpPayload,
            profile.ExternalIdpSecretRef,
            profile.ExternalIdpResolutionPriority,
            null,
            actor);

        return results;
    }

    private static async Task<Guid> ResolvePrimarySuiteIdAsync(
        Guid tenantId,
        ISystemSuiteRepository? suiteRepository,
        CancellationToken cancellationToken)
    {
        if (suiteRepository is null)
        {
            return DemoSystemSuiteGuid;
        }

        var suites = await suiteRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        return suites.FirstOrDefault()?.GetId().GetValue() ?? DemoSystemSuiteGuid;
    }

    private static void AddIdpSeedDefinition(
        ICollection<IdpSeedDefinition> results,
        Guid tenantId,
        Guid systemSuiteId,
        ProviderType providerType,
        string[] domainHints,
        string configPayload,
        string secretRef,
        int resolutionPriority,
        Guid? fallbackToId,
        ActorId actor)
    {
        var idpResult = IdpConfigurationAggregate.Create(
            TenantId.Load(tenantId),
            SystemSuiteId.Load(systemSuiteId),
            providerType,
            domainHints,
            configPayload,
            secretRef,
            resolutionPriority,
            fallbackToId,
            actor);

        if (idpResult.IsFailure)
        {
            return;
        }

        idpResult.Value.Activate(actor);
        idpResult.Value.DomainEvents.MarkChangesAsCommitted();
        results.Add(new IdpSeedDefinition(idpResult.Value, systemSuiteId, providerType, domainHints));
    }

    private static async Task SeedTenantParametersAsync(
        ITenantParameterRepository? repository,
        ActorId actor,
        CancellationToken cancellationToken)
    {
        if (repository is null)
        {
            return;
        }

        var needsSave = false;
        var desired = BuildSeedTenantParameters(actor);

        foreach (var tenantGroup in desired.GroupBy(parameter => parameter.TenantId.GetValue()))
        {
            var existing = await repository.GetByTenantIdAsync(tenantGroup.Key, cancellationToken);
            var existingCodes = existing.Select(parameter => parameter.Code.GetValue()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var parameter in tenantGroup.Where(parameter => !existingCodes.Contains(parameter.Code.GetValue())))
            {
                await repository.AddAsync(parameter, cancellationToken);
                needsSave = true;
            }
        }

        if (needsSave)
        {
            await repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        }
    }

    private static IReadOnlyList<TenantParameterAggregate> BuildSeedTenantParameters(ActorId actor)
    {
        var results = new List<TenantParameterAggregate>();

        foreach (var profile in GetTenantSeedProfiles())
        {
            var tenantId = TenantId.Load(profile.TenantId);

            foreach (var definition in GetTenantParameterDefinitions())
            {
                var parameterResult = TenantParameterAggregate.Create(
                    tenantId,
                    definition.Code,
                    definition.Description,
                    definition.Value,
                    definition.ValueType,
                    definition.Category,
                    definition.IsSensitive,
                    definition.DefaultValue,
                    definition.AllowedValues,
                    actor);

                if (parameterResult.IsSuccess)
                {
                    results.Add(parameterResult.Value);
                }
            }
        }

        return results;
    }

    private static IReadOnlyList<TenantParameterSeedDefinition> GetTenantParameterDefinitions()
    {
        return
        [
            new TenantParameterSeedDefinition(
                TenantParameterCodes.ExportProfilePermissionGraphAllowedFormats,
                "Allowed formats for permission graph exports",
                "JSON,XML,YAML,CSV",
                TenantParameterValueType.StringList,
                TenantParameterCategory.Export,
                false,
                "JSON,XML,YAML,CSV",
                "JSON,XML,YAML,CSV"),
            new TenantParameterSeedDefinition(
                TenantParameterCodes.ExportProfilePermissionGraphDefaultFormat,
                "Default format for permission graph exports",
                "JSON",
                TenantParameterValueType.String,
                TenantParameterCategory.Export,
                false,
                "JSON",
                "JSON,XML,YAML,CSV"),
            new TenantParameterSeedDefinition(
                TenantParameterCodes.ExportProfilePermissionGraphIncludeTechnicalMetadata,
                "Include technical metadata in permission graph exports",
                "true",
                TenantParameterValueType.Boolean,
                TenantParameterCategory.Export,
                false,
                "true",
                null),
            new TenantParameterSeedDefinition(
                TenantParameterCodes.ExportProfilePermissionGraphMaskGuids,
                "Mask GUIDs in permission graph exports",
                "false",
                TenantParameterValueType.Boolean,
                TenantParameterCategory.Export,
                false,
                "false",
                null),
            new TenantParameterSeedDefinition(
                TenantParameterCodes.ExportProfilePermissionGraphIncludeFeatureFlags,
                "Include feature flags in permission graph exports",
                "true",
                TenantParameterValueType.Boolean,
                TenantParameterCategory.Export,
                false,
                "true",
                null),
            new TenantParameterSeedDefinition(
                TenantParameterCodes.ExportProfilePermissionGraphIncludeEffectivePermissionsSummary,
                "Include effective permissions summary in permission graph exports",
                "true",
                TenantParameterValueType.Boolean,
                TenantParameterCategory.Export,
                false,
                "true",
                null),
            new TenantParameterSeedDefinition(
                TenantParameterCodes.ExportProfilePermissionGraphMaxItems,
                "Maximum permission graph export items",
                "10000",
                TenantParameterValueType.Integer,
                TenantParameterCategory.Export,
                false,
                "10000",
                null),
            new TenantParameterSeedDefinition(
                TenantParameterCodes.AuthGraphDefaultFormat,
                "Default serialization format for the authorization graph",
                "JSON",
                TenantParameterValueType.String,
                TenantParameterCategory.Security,
                false,
                "JSON",
                "JSON,XML,YAML,CSV"),
            new TenantParameterSeedDefinition(
                TenantParameterCodes.AuthGraphAllowedFormats,
                "Allowed formats for the authorization graph",
                "JSON,XML,YAML,CSV",
                TenantParameterValueType.StringList,
                TenantParameterCategory.Security,
                false,
                "JSON,XML,YAML,CSV",
                "JSON,XML,YAML,CSV"),
            new TenantParameterSeedDefinition(
                TenantParameterCodes.AuthGraphIncludeTechnicalMetadata,
                "Include technical metadata in the authorization graph",
                "false",
                TenantParameterValueType.Boolean,
                TenantParameterCategory.Security,
                false,
                "false",
                null),
        ];
    }

    private static IReadOnlyList<TenantSeedProfile> GetTenantSeedProfiles()
    {
        return
        [
            new TenantSeedProfile(
                InternalAdminTenantGuid,
                "INTERNAL_ADMIN",
                AppConfigurationDefaults.SessionTimeoutMinutes.ToString(),
                AppConfigurationDefaults.MaxLoginAttempts.ToString(),
                AppConfigurationDefaults.MinPasswordLength.ToString(),
                AppConfigurationDefaults.MaxValidityPeriodDays.ToString(),
                AppConfigurationDefaults.MfaRequiredForAdmin.ToString().ToLowerInvariant(),
                AppConfigurationDefaults.MfaAllowedMethods,
                AppConfigurationDefaults.UiCustomBrandingEnabled.ToString().ToLowerInvariant(),
                AppConfigurationDefaults.AuthUseExternalIdp.ToString().ToLowerInvariant(),
                AppConfigurationDefaults.UiLanguageDefault,
                AppConfigurationDefaults.UiTimezoneDefault,
                false,
                [],
                string.Empty,
                string.Empty,
                1),
            new TenantSeedProfile(
                RansaTenantGuid,
                "RANSA_PERU",
                "45",
                AppConfigurationDefaults.MaxLoginAttempts.ToString(),
                AppConfigurationDefaults.MinPasswordLength.ToString(),
                AppConfigurationDefaults.MaxValidityPeriodDays.ToString(),
                AppConfigurationDefaults.MfaRequiredForAdmin.ToString().ToLowerInvariant(),
                "Totp,WebAuthn,EmailOtp",
                "true",
                "true",
                AppConfigurationDefaults.UiLanguageDefault,
                AppConfigurationDefaults.UiTimezoneDefault,
                true,
                ["ransa.pe", "ransa.com.pe"],
                "{\"authority\":\"https://login.microsoftonline.com/common\",\"clientId\":\"1234\",\"tenantId\":\"abcd-azure-tenant\"}",
                "keyvault://ums/ransa-entra-secret",
                1),
            new TenantSeedProfile(
                ApmTenantGuid,
                "APM_CALLAO",
                "20",
                "3",
                AppConfigurationDefaults.MinPasswordLength.ToString(),
                AppConfigurationDefaults.MaxValidityPeriodDays.ToString(),
                AppConfigurationDefaults.MfaRequiredForAdmin.ToString().ToLowerInvariant(),
                "Totp,SmsOtp",
                AppConfigurationDefaults.UiCustomBrandingEnabled.ToString().ToLowerInvariant(),
                AppConfigurationDefaults.AuthUseExternalIdp.ToString().ToLowerInvariant(),
                AppConfigurationDefaults.UiLanguageDefault,
                AppConfigurationDefaults.UiTimezoneDefault,
                false,
                [],
                string.Empty,
                string.Empty,
                1),
            new TenantSeedProfile(
                NeptuniaTenantGuid,
                "NEPTUNIA",
                "60",
                AppConfigurationDefaults.MaxLoginAttempts.ToString(),
                "14",
                AppConfigurationDefaults.MaxValidityPeriodDays.ToString(),
                AppConfigurationDefaults.MfaRequiredForAdmin.ToString().ToLowerInvariant(),
                "Totp,WebAuthn",
                AppConfigurationDefaults.UiCustomBrandingEnabled.ToString().ToLowerInvariant(),
                "true",
                AppConfigurationDefaults.UiLanguageDefault,
                AppConfigurationDefaults.UiTimezoneDefault,
                true,
                ["neptunia.com.pe", "neptunia.pe"],
                "{\"authority\":\"https://login.microsoftonline.com/neptunia\",\"clientId\":\"neptunia-client\"}",
                "keyvault://ums/neptunia-entra-secret",
                1),
            new TenantSeedProfile(
                EvolithTenantGuid,
                "EVOLITH",
                AppConfigurationDefaults.SessionTimeoutMinutes.ToString(),
                AppConfigurationDefaults.MaxLoginAttempts.ToString(),
                AppConfigurationDefaults.MinPasswordLength.ToString(),
                AppConfigurationDefaults.MaxValidityPeriodDays.ToString(),
                AppConfigurationDefaults.MfaRequiredForAdmin.ToString().ToLowerInvariant(),
                "Totp,EmailOtp",
                AppConfigurationDefaults.UiCustomBrandingEnabled.ToString().ToLowerInvariant(),
                AppConfigurationDefaults.AuthUseExternalIdp.ToString().ToLowerInvariant(),
                AppConfigurationDefaults.UiLanguageDefault,
                AppConfigurationDefaults.UiTimezoneDefault,
                false,
                [],
                string.Empty,
                string.Empty,
                1),
            new TenantSeedProfile(
                PaitaTenantGuid,
                "PAITA_PORT",
                "15",
                "3",
                AppConfigurationDefaults.MinPasswordLength.ToString(),
                AppConfigurationDefaults.MaxValidityPeriodDays.ToString(),
                AppConfigurationDefaults.MfaRequiredForAdmin.ToString().ToLowerInvariant(),
                "Totp,SmsOtp,EmailOtp",
                AppConfigurationDefaults.UiCustomBrandingEnabled.ToString().ToLowerInvariant(),
                "true",
                AppConfigurationDefaults.UiLanguageDefault,
                AppConfigurationDefaults.UiTimezoneDefault,
                true,
                ["paita.gob.pe", "paita-port.pe"],
                "{\"authority\":\"https://login.microsoftonline.com/paita\",\"clientId\":\"paita-client\"}",
                "keyvault://ums/paita-entra-secret",
                1),
            new TenantSeedProfile(
                IntradevcoTenantGuid,
                "INTRADEVCO",
                "40",
                AppConfigurationDefaults.MaxLoginAttempts.ToString(),
                "10",
                AppConfigurationDefaults.MaxValidityPeriodDays.ToString(),
                AppConfigurationDefaults.MfaRequiredForAdmin.ToString().ToLowerInvariant(),
                "Totp,WebAuthn,EmailOtp",
                AppConfigurationDefaults.UiCustomBrandingEnabled.ToString().ToLowerInvariant(),
                "true",
                AppConfigurationDefaults.UiLanguageDefault,
                AppConfigurationDefaults.UiTimezoneDefault,
                true,
                ["intradevco.com", "intradevco.pe"],
                "{\"authority\":\"https://login.microsoftonline.com/intradevco\",\"clientId\":\"intradevco-client\"}",
                "keyvault://ums/intradevco-entra-secret",
                1),
        ];
    }

    private sealed record TenantSeedProfile(
        Guid TenantId,
        string Name,
        string SessionTimeoutMinutes,
        string MaxLoginAttempts,
        string MinPasswordLength,
        string MaxValidityPeriodDays,
        string MfaRequiredForAdmin,
        string MfaAllowedMethods,
        string CustomBrandingEnabled,
        string AuthUseExternalIdp,
        string DefaultLanguage,
        string DefaultTimezone,
        bool SeedExternalIdp,
        string[] ExternalIdpDomainHints,
        string ExternalIdpPayload,
        string ExternalIdpSecretRef,
        int ExternalIdpResolutionPriority,
        // ADR-UMS-095: duración del bloqueo temporal por intentos fallidos. Default seguro (15 min),
        // alineado con AppConfigurationDefaults.AccountLockoutDurationMinutes; parametrizable por perfil.
        string AccountLockoutDurationMinutes = "15");

    private sealed record FeatureFlagSeedDefinition(
        string Code,
        FlagType Type,
        string Targets,
        string Description,
        int? RolloutPercentage);

    private sealed record TenantParameterSeedDefinition(
        string Code,
        string Description,
        string Value,
        TenantParameterValueType ValueType,
        TenantParameterCategory Category,
        bool IsSensitive,
        string? DefaultValue,
        string? AllowedValues);

    private sealed record IdpSeedDefinition(
        IdpConfigurationAggregate Configuration,
        Guid SystemSuiteId,
        ProviderType ProviderType,
        string[] DomainHints);

}
