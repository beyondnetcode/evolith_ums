namespace Evolith.Ums.Infrastructure.Persistence.Seeders;

using Evolith.Ums.Domain.Configuration.AppConfiguration;
using Evolith.Ums.Domain.Identity.Tenant.TenantParameter;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class ParameterCatalogSeeder
{
    private static readonly Guid[] TenantIds =
    [
        Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId),
        Guid.Parse(CoreDevDataSeeder.RansaTenantId),
        Guid.Parse("A3F5B9D2-7C3D-4C8E-A9B0-123456789ABC"),
        Guid.Parse("C9B736B4-6A84-48F8-B34D-176BC5A6D542"),
        Guid.Parse("5F4E3D2C-1B0A-9F8E-7D6C-543210987654"),
        Guid.Parse("9E8D7C6B-5A4F-3E2D-1C0B-9876543210FE"),
        Guid.Parse("F3E2D1C0-B9A8-7F6E-5D4C-321098765432"),
    ];

    // FS-25 §4.5 — parámetros de operación impo/expo acotados al tenant EVOLITH.
    private static readonly Guid EvolithTenantId = Guid.Parse(CoreDevDataSeeder.EvolithTenantId);
    private const string IncotermCode = "INCOTERM";
    private const string MonedaCode = "MONEDA";
    private const string TipoDuaCode = "TIPO_DUA";

    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var dbContext = serviceProvider.GetRequiredService<UmsPlatformDbContext>();
        await SeedDefinitionsAsync(dbContext, cancellationToken);
        await SeedGlobalValuesAsync(dbContext, cancellationToken);
        await SeedTenantValuesAsync(dbContext, cancellationToken);
        await SeedEvolithOperationValuesAsync(dbContext, cancellationToken);
    }

    private static async Task SeedDefinitionsAsync(UmsPlatformDbContext dbContext, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters, y ahora por una razón distinta a la original: el índice único de `Code`
        // pasó a ser parcial, así que reinsertar el código de una definición eliminada YA NO chocaría
        // contra el índice. Se mantiene mirando también las lápidas para no RESUCITAR lo que alguien
        // retiró a propósito: el sembrado garantiza que el catálogo base existe, no que reaparezca
        // cada arranque lo que se decidió quitar. Volver a declararla es un acto deliberado del alta.
        var existingCodes = (await dbContext.ParameterDefinitions
                .IgnoreQueryFilters()
                .Select(definition => definition.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var definitions = BuildDefinitions()
            .Where(definition => !existingCodes.Contains(definition.Code))
            .ToList();

        if (definitions.Count == 0)
        {
            return;
        }

        await dbContext.ParameterDefinitions.AddRangeAsync(definitions, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static async Task SeedGlobalValuesAsync(UmsPlatformDbContext dbContext, CancellationToken cancellationToken = default)
    {
        // Cuenta también los valores eliminados lógicamente, a propósito: liberar la ranura permite
        // volver a fijar el valor global, pero eso es un acto del operador, no del sembrado.
        var existingDefinitionIds = await dbContext.ParameterGlobalValues
            .Select(value => value.ParameterDefinitionId)
            .ToHashSetAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var systemActorId = "SYSTEM";
        var globalValues = new List<ParameterGlobalValueRecord>();

        AddGlobalValue(globalValues, existingDefinitionIds, "11111111-1111-1111-1111-111111111103", AppConfigurationDefaults.AccessTokenDurationMs.ToString(), systemActorId, now);
        AddGlobalValue(globalValues, existingDefinitionIds, "11111111-1111-1111-1111-111111111104", AppConfigurationDefaults.RefreshTokenDurationMs.ToString(), systemActorId, now);
        AddGlobalValue(globalValues, existingDefinitionIds, "11111111-1111-1111-1111-111111111108", AppConfigurationDefaults.FrontendConfigTransport, systemActorId, now);

        if (globalValues.Count == 0)
        {
            return;
        }

        await dbContext.ParameterGlobalValues.AddRangeAsync(globalValues, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static async Task SeedTenantValuesAsync(UmsPlatformDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var definitionsByCode = (await dbContext.ParameterDefinitions.ToListAsync(cancellationToken))
            .ToDictionary(definition => definition.Code, StringComparer.OrdinalIgnoreCase);

        var existingKeys = await dbContext.ParameterTenantValues
            .Select(value => new { value.TenantId, value.ParameterDefinitionId })
            .ToListAsync(cancellationToken);

        var existingKeySet = existingKeys
            .Select(value => (value.TenantId, value.ParameterDefinitionId))
            .ToHashSet();

        var now = DateTime.UtcNow;
        var systemActorId = "SYSTEM";
        var tenantValues = new List<ParameterTenantValueRecord>();

        foreach (var tenantId in TenantIds)
        {
            foreach (var seedValue in BuildTenantValues())
            {
                if (!definitionsByCode.TryGetValue(seedValue.Code, out var definition))
                {
                    continue;
                }

                var key = (tenantId, definition.Id);
                if (existingKeySet.Contains(key))
                {
                    continue;
                }

                tenantValues.Add(new ParameterTenantValueRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ParameterDefinitionId = definition.Id,
                    OverrideValue = seedValue.Value,
                    StatusId = 2,
                    Version = "1.0.0",
                    CreatedBy = systemActorId,
                    CreatedAtUtc = now,
                    AuditTimeSpan = now.ToString("O")
                });
            }
        }

        if (tenantValues.Count == 0)
        {
            return;
        }

        await dbContext.ParameterTenantValues.AddRangeAsync(tenantValues, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // FS-25 §4.5 / criterio 11: valores de operación impo/expo acotados al tenant EVOLITH.
    private static async Task SeedEvolithOperationValuesAsync(UmsPlatformDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var definitionsByCode = (await dbContext.ParameterDefinitions.ToListAsync(cancellationToken))
            .ToDictionary(definition => definition.Code, StringComparer.OrdinalIgnoreCase);

        var existingDefinitionIds = (await dbContext.ParameterTenantValues
                .Where(value => value.TenantId == EvolithTenantId)
                .Select(value => value.ParameterDefinitionId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var now = DateTime.UtcNow;
        var systemActorId = "SYSTEM";
        var tenantValues = new List<ParameterTenantValueRecord>();

        foreach (var seedValue in BuildEvolithOperationValues())
        {
            if (!definitionsByCode.TryGetValue(seedValue.Code, out var definition))
            {
                continue;
            }

            if (existingDefinitionIds.Contains(definition.Id))
            {
                continue;
            }

            tenantValues.Add(new ParameterTenantValueRecord
            {
                Id = Guid.NewGuid(),
                TenantId = EvolithTenantId,
                ParameterDefinitionId = definition.Id,
                OverrideValue = seedValue.Value,
                StatusId = 2,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            });
        }

        if (tenantValues.Count == 0)
        {
            return;
        }

        await dbContext.ParameterTenantValues.AddRangeAsync(tenantValues, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<(string Code, string Value)> BuildEvolithOperationValues()
    {
        return
        [
            (IncotermCode, "FOB,CIF,EXW,FCA,CFR,CPT"),
            (MonedaCode, "USD,PEN"),
            (TipoDuaCode, "Importación Definitiva,Exportación Definitiva,Admisión Temporal"),
        ];
    }

    private static IReadOnlyList<ParameterDefinitionRecord> BuildDefinitions()
    {
        var now = DateTime.UtcNow;
        var systemActorId = "SYSTEM";

        return
        [
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111101"),
                Code = AppConfigurationCodes.SessionTimeoutMinutes,
                Name = "Session Timeout",
                Description = "Idle session timeout in minutes",
                DataTypeId = 2,
                DefaultValue = AppConfigurationDefaults.SessionTimeoutMinutes.ToString(),
                ScopeId = 3,
                IsActive = true,
                IsMandatory = true,
                DisplayOrder = 1,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111102"),
                Code = AppConfigurationCodes.MaxLoginAttempts,
                Name = "Max Login Attempts",
                Description = "Maximum login attempts before lockout",
                DataTypeId = 2,
                DefaultValue = AppConfigurationDefaults.MaxLoginAttempts.ToString(),
                ScopeId = 3,
                IsActive = true,
                IsMandatory = true,
                DisplayOrder = 2,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                // ADR-UMS-095: duración del bloqueo temporal por intentos fallidos (junto a MAX_LOGIN_ATTEMPTS).
                Id = Guid.Parse("11111111-1111-1111-1111-111111111117"),
                Code = AppConfigurationCodes.AccountLockoutDurationMinutes,
                Name = "Account Lockout Duration Minutes",
                Description = "Temporary account lockout duration in minutes after reaching the max login attempts",
                DataTypeId = 2,
                DefaultValue = AppConfigurationDefaults.AccountLockoutDurationMinutes.ToString(),
                ScopeId = 3,
                IsActive = true,
                IsMandatory = false,
                DisplayOrder = 15,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111103"),
                Code = AppConfigurationCodes.AccessTokenDurationMs,
                Name = "Access Token Duration",
                Description = "Access token lifetime in milliseconds",
                DataTypeId = 2,
                DefaultValue = AppConfigurationDefaults.AccessTokenDurationMs.ToString(),
                ScopeId = 1,
                IsActive = true,
                IsMandatory = true,
                DisplayOrder = 3,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111104"),
                Code = AppConfigurationCodes.RefreshTokenDurationMs,
                Name = "Refresh Token Duration",
                Description = "Refresh token lifetime in milliseconds (7 days)",
                DataTypeId = 2,
                DefaultValue = AppConfigurationDefaults.RefreshTokenDurationMs.ToString(),
                ScopeId = 1,
                IsActive = true,
                IsMandatory = true,
                DisplayOrder = 4,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111105"),
                Code = AppConfigurationCodes.MinPasswordLength,
                Name = "Min Password Length",
                Description = "Minimum required password length",
                DataTypeId = 2,
                DefaultValue = AppConfigurationDefaults.MinPasswordLength.ToString(),
                ScopeId = 3,
                IsActive = true,
                IsMandatory = true,
                DisplayOrder = 5,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111106"),
                Code = AppConfigurationCodes.MfaRequiredForAdmin,
                Name = "MFA Required for Admin",
                Description = "Tenant-scoped toggle that requires verified MFA at login when enabled",
                DataTypeId = 3,
                DefaultValue = AppConfigurationDefaults.MfaRequiredForAdmin.ToString().ToLowerInvariant(),
                ScopeId = 3,
                IsActive = true,
                IsMandatory = false,
                DisplayOrder = 6,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111116"),
                Code = AppConfigurationCodes.MfaAllowedMethods,
                Name = "Allowed MFA Methods",
                Description = "Comma-separated list of allowed MFA methods for the tenant",
                DataTypeId = 1,
                DefaultValue = AppConfigurationDefaults.MfaAllowedMethods,
                ScopeId = 3,
                IsActive = true,
                IsMandatory = false,
                DisplayOrder = 7,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111107"),
                Code = AppConfigurationCodes.UiCustomBrandingEnabled,
                Name = "Custom Branding Enabled",
                Description = "Enable custom tenant branding",
                DataTypeId = 3,
                DefaultValue = AppConfigurationDefaults.UiCustomBrandingEnabled.ToString().ToLowerInvariant(),
                ScopeId = 3,
                IsActive = true,
                IsMandatory = false,
                DisplayOrder = 8,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111108"),
                Code = AppConfigurationCodes.FrontendConfigTransport,
                Name = "Frontend Config Transport",
                Description = "Transport mode for frontend config queries (REST only)",
                DataTypeId = 1,
                DefaultValue = AppConfigurationDefaults.FrontendConfigTransport,
                ScopeId = 1,
                IsActive = true,
                IsMandatory = false,
                DisplayOrder = 9,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111109"),
                Code = AppConfigurationCodes.MaxValidityPeriodDays,
                Name = "Max Validity Period Days",
                Description = "Maximum user account validity period in days",
                DataTypeId = 2,
                DefaultValue = AppConfigurationDefaults.MaxValidityPeriodDays.ToString(),
                ScopeId = 3,
                IsActive = true,
                IsMandatory = false,
                DisplayOrder = 10,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111110"),
                Code = AppConfigurationCodes.AuthUseExternalIdp,
                Name = "Use External IDP",
                Description = "Whether the tenant uses external Identity Providers instead of local password credentials",
                DataTypeId = 3,
                DefaultValue = AppConfigurationDefaults.AuthUseExternalIdp.ToString().ToLowerInvariant(),
                ScopeId = 3,
                IsActive = true,
                IsMandatory = false,
                DisplayOrder = 11,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111120"),
                Code = IncotermCode,
                Name = "Incoterm",
                Description = "Términos de comercio internacional (Incoterms) aplicables a las operaciones de impo/expo",
                DataTypeId = 1,
                DefaultValue = "FOB,CIF,EXW,FCA,CFR,CPT",
                ScopeId = 2,
                IsActive = true,
                IsMandatory = false,
                DisplayOrder = 12,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111121"),
                Code = MonedaCode,
                Name = "Moneda",
                Description = "Monedas admitidas en las operaciones de impo/expo",
                DataTypeId = 1,
                DefaultValue = "USD,PEN",
                ScopeId = 2,
                IsActive = true,
                IsMandatory = false,
                DisplayOrder = 13,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            },
            new ParameterDefinitionRecord
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111122"),
                Code = TipoDuaCode,
                Name = "Tipo de DUA",
                Description = "Tipos de declaración aduanera (DUA) para las operaciones de impo/expo",
                DataTypeId = 1,
                DefaultValue = "Importación Definitiva,Exportación Definitiva,Admisión Temporal",
                ScopeId = 2,
                IsActive = true,
                IsMandatory = false,
                DisplayOrder = 14,
                Version = "1.0.0",
                CreatedBy = systemActorId,
                CreatedAtUtc = now,
                AuditTimeSpan = now.ToString("O")
            }
        ];
    }

    private static IReadOnlyList<(string Code, string Value)> BuildTenantValues()
    {
        return
        [
            (TenantParameterCodes.ExportProfilePermissionGraphAllowedFormats, string.Join(",", TenantParameterDefaults.ExportProfilePermissionGraphAllowedFormats)),
            (TenantParameterCodes.ExportProfilePermissionGraphDefaultFormat, TenantParameterDefaults.ExportProfilePermissionGraphDefaultFormat),
            (TenantParameterCodes.ExportProfilePermissionGraphIncludeTechnicalMetadata, TenantParameterDefaults.ExportProfilePermissionGraphIncludeTechnicalMetadata.ToString().ToLowerInvariant()),
            (TenantParameterCodes.ExportProfilePermissionGraphMaskGuids, TenantParameterDefaults.ExportProfilePermissionGraphMaskGuids.ToString().ToLowerInvariant()),
            (TenantParameterCodes.ExportProfilePermissionGraphIncludeFeatureFlags, TenantParameterDefaults.ExportProfilePermissionGraphIncludeFeatureFlags.ToString().ToLowerInvariant()),
            (TenantParameterCodes.ExportProfilePermissionGraphIncludeEffectivePermissionsSummary, TenantParameterDefaults.ExportProfilePermissionGraphIncludeEffectivePermissionsSummary.ToString().ToLowerInvariant()),
            (TenantParameterCodes.ExportProfilePermissionGraphMaxItems, TenantParameterDefaults.ExportProfilePermissionGraphMaxItems.ToString()),
            (TenantParameterCodes.AuthGraphDefaultFormat, TenantParameterDefaults.AuthGraphDefaultFormat),
            (TenantParameterCodes.AuthGraphAllowedFormats, string.Join(",", TenantParameterDefaults.AuthGraphAllowedFormats)),
            (TenantParameterCodes.AuthGraphIncludeTechnicalMetadata, TenantParameterDefaults.AuthGraphIncludeTechnicalMetadata.ToString().ToLowerInvariant()),
        ];
    }

    private static void AddGlobalValue(
        ICollection<ParameterGlobalValueRecord> values,
        ISet<Guid> existingDefinitionIds,
        string definitionId,
        string effectiveValue,
        string createdBy,
        DateTime now)
    {
        var parsedDefinitionId = Guid.Parse(definitionId);
        if (existingDefinitionIds.Contains(parsedDefinitionId))
        {
            return;
        }

        values.Add(new ParameterGlobalValueRecord
        {
            Id = Guid.NewGuid(),
            ParameterDefinitionId = parsedDefinitionId,
            EffectiveValue = effectiveValue,
            StatusId = 2,
            Version = "1.0.0",
            CreatedBy = createdBy,
            CreatedAtUtc = now,
            AuditTimeSpan = now.ToString("O")
        });
    }
}
