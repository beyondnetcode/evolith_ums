using BeyondNetCode.Shell.Ddd;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Audit.AuditRecord;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Profile;
using Evolith.Ums.Domain.Authorization.SystemSuite;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;
using ApprovalRequestAggregate = Evolith.Ums.Domain.Approvals.ApprovalRequest.ApprovalRequest;
using AuditRecordAggregate = Evolith.Ums.Domain.Audit.AuditRecord.AuditRecord;
using FeatureFlagAggregate = Evolith.Ums.Domain.Configuration.FeatureFlag.FeatureFlag;
using RolePromotionRequestAggregate = Evolith.Ums.Domain.IGA.RolePromotionRequest.RolePromotionRequest;
using Evolith.Ums.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Evolith.Ums.ContractTest.Provider;

internal sealed class PactProviderStateMiddleware(RequestDelegate next)
{
    private const string Path = "/_pact/provider-states";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method)
            || !context.Request.Path.Equals(Path, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        ProviderStateRequest? request;
        try
        {
            request = await context.Request.ReadFromJsonAsync<ProviderStateRequest>(context.RequestAborted);
        }
        catch (JsonException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (request is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (string.IsNullOrWhiteSpace(request.State))
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return;
        }

        await EnsureProviderStateAsync(request, context.RequestServices);
        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    private static async Task EnsureProviderStateAsync(ProviderStateRequest request, IServiceProvider services)
    {
        var tenants = services.GetRequiredService<ITenantRepository>();
        var userAccounts = services.GetRequiredService<IUserAccountRepository>();
        var permissionTemplates = services.GetRequiredService<IPermissionTemplateRepository>();
        var profiles = services.GetRequiredService<IProfileRepository>();
        var systemSuites = services.GetRequiredService<ISystemSuiteRepository>();
        var approvalRequests = services.GetRequiredService<IApprovalRequestRepository>();
        var auditRecords = services.GetRequiredService<IAuditRecordRepository>();
        var featureFlags = services.GetRequiredService<IFeatureFlagRepository>();
        var appConfigurations = services.GetRequiredService<IAppConfigurationRepository>();
        var rolePromotionRequests = services.GetRequiredService<IRolePromotionRequestRepository>();

        await (request.State switch
        {
            var s when s.StartsWith("a tenant with id ") => EnsureTenantExistsAsync(s, tenants),
            var s when s.StartsWith("no tenant with id ") => Task.CompletedTask,
            "at least one tenant exists" => EnsureDefaultTenantAsync(tenants),

            var s when s.StartsWith("a user account with id ") && s.EndsWith(" is active") => EnsureUserAccountExistsAsync(s, userAccounts, tenants, true),
            var s when s.StartsWith("a user account with id ") && s.EndsWith(" is inactive") => EnsureUserAccountExistsAsync(s, userAccounts, tenants, false),
            var s when s.StartsWith("a user account with id ") => EnsureUserAccountExistsAsync(s, userAccounts, tenants, true),
            var s when s.StartsWith("no user account with id ") => Task.CompletedTask,
            "at least one user account exists" => EnsureDefaultUserAccountAsync(userAccounts, tenants),
            "a user account exists with valid credentials" => EnsureDefaultUserAccountWithPasswordAsync(userAccounts, tenants),
            "a user account does not exist or credentials do not match" => Task.CompletedTask,

            var s when s.StartsWith("a permission template with id ") => EnsurePermissionTemplateExistsAsync(s, permissionTemplates, tenants, systemSuites),
            "at least one permission template exists" => EnsureDefaultPermissionTemplateAsync(permissionTemplates, tenants, systemSuites),

            var s when s.StartsWith("a profile with id ") => EnsureProfileExistsAsync(s, profiles, tenants, userAccounts),
            "at least one profile exists" => EnsureDefaultProfileAsync(profiles, tenants, userAccounts),

            var s when s.StartsWith("a system suite with id ") => EnsureSystemSuiteExistsAsync(s, systemSuites, tenants),
            "at least one system suite exists" => EnsureDefaultSystemSuiteAsync(systemSuites, tenants),

            // G-082: Approvals — ApprovalRequest
            var s when s.StartsWith("an approval request with id ") => EnsureApprovalRequestExistsAsync(s, approvalRequests),
            var s when s.StartsWith("no approval request with id ") => Task.CompletedTask,
            "at least one approval request exists" => SeedApprovalRequestAsync(Guid.Empty, approvalRequests),

            // G-082: Audit — AuditRecord
            var s when s.StartsWith("an audit record with id ") => EnsureAuditRecordExistsAsync(s, auditRecords),
            var s when s.StartsWith("no audit record with id ") => Task.CompletedTask,
            "at least one audit record exists" => SeedAuditRecordAsync(Guid.Empty, auditRecords),

            // G-082: Configuration — FeatureFlag
            var s when s.StartsWith("a feature flag with id ") => EnsureFeatureFlagExistsAsync(s, featureFlags),
            var s when s.StartsWith("no feature flag with id ") => Task.CompletedTask,
            "at least one feature flag exists" => SeedFeatureFlagAsync(Guid.Empty, featureFlags),

            // G-082: Configuration — AppConfiguration
            var s when s.StartsWith("no app configuration with id ") => Task.CompletedTask,
            "at least one app configuration exists" => SeedAppConfigurationAsync(appConfigurations),

            // G-082: IGA — RolePromotionRequest
            var s when s.StartsWith("a role promotion request with id ") => EnsureRolePromotionRequestExistsAsync(s, rolePromotionRequests),
            var s when s.StartsWith("no role promotion request with id ") => Task.CompletedTask,
            "at least one role promotion request exists" => SeedRolePromotionRequestAsync(Guid.Empty, rolePromotionRequests),

            _ => Task.CompletedTask,
        });
    }

    private static readonly Guid DefaultTenantGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DefaultUserGuid = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly ActorId SeedActor = ActorId.Create("00000000-0000-0000-0000-000000000111");

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada en fixture de estado de proveedor Pact. Fija IDs bien " +
                        "conocidos sobre props no públicos para reproducir estados de proveedor en pruebas de " +
                        "contrato; no forma parte del runtime de producción.")]
    private static void SetEntityId<T>(T entity, Guid id) where T : class
    {
        var type = entity.GetType();
        var propsProperty = type.BaseType?.GetProperty("Props", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (propsProperty != null)
        {
            var props = propsProperty.GetValue(entity);
            if (props != null)
            {
                var idProperty = props.GetType().GetProperty("Id");
                if (idProperty != null)
                {
                    var idType = idProperty.PropertyType;
                    var loadMethod = idType.GetMethod("Load", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, new[] { typeof(Guid) });
                    if (loadMethod != null)
                    {
                        var idVal = loadMethod.Invoke(null, new object[] { id });
                        idProperty.SetValue(props, idVal);
                    }
                }
            }
        }
    }

    private static async Task EnsureDefaultTenantAsync(ITenantRepository tenants)
    {
        if (await tenants.GetByIdAsync(DefaultTenantGuid) is not null) return;
        await SeedTenantAsync(DefaultTenantGuid, "ACME", "Acme Corp", tenants);
    }

    private static async Task EnsureTenantExistsAsync(string state, ITenantRepository tenants)
    {
        var id = ExtractGuid(state);
        if (id == Guid.Empty) return;
        if (await tenants.GetByIdAsync(id) is not null) return;
        await SeedTenantAsync(id, "ACME", "Acme Corp", tenants);
    }

    private static async Task SeedTenantAsync(Guid id, string code, string name, ITenantRepository tenants)
    {
        var result = Tenant.Create(
            Code.Create(code),
            Name.Create(name),
            OrganizationType.INTERNAL,
            SeedActor,
            IdpStrategy.InternalBcrypt,
            companyReference: null,
            parentTenantId: null,
            tenantId: TenantId.Load(id));

        if (result.IsFailure) return;

        var tenant = result.Value;
        tenant.Activate(SeedActor);

        await tenants.AddAsync(tenant);
        await tenants.UnitOfWork.SaveEntitiesAsync();
    }

    private static async Task EnsureDefaultUserAccountWithPasswordAsync(
        IUserAccountRepository userAccounts, ITenantRepository tenants)
    {
        await EnsureDefaultUserAccountAsync(userAccounts, tenants);
        var user = await userAccounts.GetByIdAsync(DefaultUserGuid);
        if (user is not null)
        {
            // Bcrypt hash for "ValidPassword123!"
            user.AddPassword(PasswordHash.Create("$2a$11$00000000000000000000001Z9KzDqM8G.Y3kG.0Hw8z.gLdD/W5qG"), SeedActor);
            await userAccounts.UnitOfWork.SaveEntitiesAsync();
        }
    }

    private static async Task EnsureDefaultUserAccountAsync(
        IUserAccountRepository userAccounts, ITenantRepository tenants)
    {
        await EnsureDefaultTenantAsync(tenants);
        if (await userAccounts.GetByIdAsync(DefaultUserGuid) is not null) return;
        await SeedUserAccountAsync(DefaultUserGuid, DefaultTenantGuid, "user@example.com", userAccounts, true);
    }

    private static async Task EnsureUserAccountExistsAsync(
        string state, IUserAccountRepository userAccounts, ITenantRepository tenants, bool isActive)
    {
        var id = ExtractGuid(state);
        if (id == Guid.Empty) return;
        await EnsureDefaultTenantAsync(tenants);
        if (await userAccounts.GetByIdAsync(id) is not null) return;
        await SeedUserAccountAsync(id, DefaultTenantGuid, $"pact-test-{id}@contract.test", userAccounts, isActive);
    }

    private static async Task SeedUserAccountAsync(
        Guid id, Guid tenantId, string email, IUserAccountRepository userAccounts, bool isActive = true)
    {
        var result = UserAccount.Create(
            TenantId.Load(tenantId),
            Email.Create(email),
            UserCategory.Internal,
            identityReference: null,
            identityReferenceType: null,
            SeedActor,
            branchId: null,
            userAccountId: UserAccountId.Load(id));

        if (result.IsFailure) return;

        var account = result.Value;
        if (isActive)
        {
            account.Activate(SeedActor);
        }

        await userAccounts.AddAsync(account);
        await userAccounts.UnitOfWork.SaveEntitiesAsync();
    }

    private static async Task EnsureDefaultSystemSuiteAsync(ISystemSuiteRepository systemSuites, ITenantRepository tenants)
    {
        await EnsureDefaultTenantAsync(tenants);
        var id = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        if (await systemSuites.GetByIdAsync(id) is not null) return;
        await SeedSystemSuiteAsync(id, DefaultTenantGuid, systemSuites);
    }

    private static async Task EnsureSystemSuiteExistsAsync(string state, ISystemSuiteRepository systemSuites, ITenantRepository tenants)
    {
        await EnsureDefaultTenantAsync(tenants);
        var id = ExtractGuid(state);
        if (id == Guid.Empty) return;
        if (await systemSuites.GetByIdAsync(id) is not null) return;
        await SeedSystemSuiteAsync(id, DefaultTenantGuid, systemSuites);
    }

    private static async Task SeedSystemSuiteAsync(Guid id, Guid tenantId, ISystemSuiteRepository systemSuites)
    {
        var result = SystemSuite.Create(
            TenantId.Load(tenantId),
            Code.Create("UMS-CORE"),
            Name.Create("User Management System"),
            Description.Create("Core System"),
            SeedActor);
        if (result.IsFailure) return;

        var suite = result.Value;
        SetEntityId(suite, id);

        // Use reflection if Activate method is internal or missing
        var activateMethod = suite.GetType().GetMethod("Activate");
        activateMethod?.Invoke(suite, new object[] { SeedActor });

        await systemSuites.AddAsync(suite);
        await systemSuites.UnitOfWork.SaveEntitiesAsync();
    }

    private static async Task EnsureDefaultPermissionTemplateAsync(IPermissionTemplateRepository permissionTemplates, ITenantRepository tenants, ISystemSuiteRepository systemSuites)
    {
        await EnsureDefaultSystemSuiteAsync(systemSuites, tenants);
        var id = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        if (await permissionTemplates.GetByIdAsync(id) is not null) return;
        await SeedPermissionTemplateAsync(id, DefaultTenantGuid, Guid.NewGuid(), id, permissionTemplates);
    }

    private static async Task EnsurePermissionTemplateExistsAsync(string state, IPermissionTemplateRepository permissionTemplates, ITenantRepository tenants, ISystemSuiteRepository systemSuites)
    {
        await EnsureDefaultSystemSuiteAsync(systemSuites, tenants);
        var id = ExtractGuid(state);
        if (id == Guid.Empty) return;
        if (await permissionTemplates.GetByIdAsync(id) is not null) return;
        await SeedPermissionTemplateAsync(id, DefaultTenantGuid, Guid.NewGuid(), id, permissionTemplates);
    }

    private static async Task SeedPermissionTemplateAsync(Guid id, Guid tenantId, Guid roleId, Guid suiteId, IPermissionTemplateRepository permissionTemplates)
    {
        var result = PermissionTemplate.Create(
            TenantId.Load(tenantId),
            RoleId.Load(roleId),
            SystemSuiteId.Load(suiteId),
            SeedActor);
        if (result.IsFailure) return;

        var template = result.Value;
        SetEntityId(template, id);

        var publishMethod = template.GetType().GetMethod("Publish");
        publishMethod?.Invoke(template, new object[] { SeedActor });

        await permissionTemplates.AddAsync(template);
        await permissionTemplates.UnitOfWork.SaveEntitiesAsync();
    }

    private static async Task EnsureDefaultProfileAsync(IProfileRepository profiles, ITenantRepository tenants, IUserAccountRepository userAccounts)
    {
        await EnsureDefaultUserAccountAsync(userAccounts, tenants);
        var id = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        if (await profiles.GetByIdAsync(id) is not null) return;
        await SeedProfileAsync(id, DefaultTenantGuid, DefaultUserGuid, Guid.NewGuid(), profiles);
    }

    private static async Task EnsureProfileExistsAsync(string state, IProfileRepository profiles, ITenantRepository tenants, IUserAccountRepository userAccounts)
    {
        await EnsureDefaultUserAccountAsync(userAccounts, tenants);
        var id = ExtractGuid(state);
        if (id == Guid.Empty) return;
        if (await profiles.GetByIdAsync(id) is not null) return;
        await SeedProfileAsync(id, DefaultTenantGuid, DefaultUserGuid, Guid.NewGuid(), profiles);
    }

    private static async Task SeedProfileAsync(Guid id, Guid tenantId, Guid userId, Guid roleId, IProfileRepository profiles)
    {
        var result = Profile.Create(
            TenantId.Load(tenantId),
            UserId.Load(userId),
            RoleId.Load(roleId),
            null,
            SeedActor);
        if (result.IsFailure) return;

        var profile = result.Value;
        SetEntityId(profile, id);

        var activateMethod = profile.GetType().GetMethod("Activate");
        activateMethod?.Invoke(profile, new object[] { SeedActor });

        await profiles.AddAsync(profile);
        await profiles.UnitOfWork.SaveEntitiesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // G-082: sembradores de los cuatro contextos sin contrato previo.
    // Sólo interesa la FORMA HTTP; se construyen agregados válidos con
    // identificadores fijos cargados desde GUID (sin verificar existencia
    // de referencias, que no participan en la forma de la respuesta).
    // ─────────────────────────────────────────────────────────────
    private static readonly Guid SeedRefGuidA = Guid.Parse("aaaa1111-0000-0000-0000-000000000001");
    private static readonly Guid SeedRefGuidB = Guid.Parse("bbbb2222-0000-0000-0000-000000000002");

    // ── Approvals: ApprovalRequest ─────────────────────────────────
    private static async Task EnsureApprovalRequestExistsAsync(string state, IApprovalRequestRepository requests)
    {
        var id = ExtractGuid(state);
        if (id == Guid.Empty) return;
        if (await requests.GetByIdAsync(id) is not null) return;
        await SeedApprovalRequestAsync(id, requests);
    }

    private static async Task SeedApprovalRequestAsync(Guid id, IApprovalRequestRepository requests)
    {
        var result = ApprovalRequestAggregate.Create(
            ApprovalWorkflowId.Load(SeedRefGuidA),
            UserId.Load(SeedRefGuidB),
            null,
            SystemSuiteId.Load(SeedRefGuidA),
            null,
            RoleId.Load(SeedRefGuidB),
            "Contract test approval request.",
            SeedActor);

        if (result.IsFailure) return;

        var request = result.Value;
        if (id != Guid.Empty) SetEntityId(request, id);

        await requests.AddAsync(request);
        await requests.UnitOfWork.SaveEntitiesAsync();
    }

    // ── Audit: AuditRecord ─────────────────────────────────────────
    private static async Task EnsureAuditRecordExistsAsync(string state, IAuditRecordRepository auditRecords)
    {
        var id = ExtractGuid(state);
        if (id == Guid.Empty) return;
        if (await auditRecords.GetByIdAsync(id) is not null) return;
        await SeedAuditRecordAsync(id, auditRecords);
    }

    private static async Task SeedAuditRecordAsync(Guid id, IAuditRecordRepository auditRecords)
    {
        // El listado se acota por inquilino (RootTenantId == tenantId de la consulta) y por
        // ventana temporal reciente: se siembra con el inquilino por defecto y WhenOccurred = ahora.
        var result = AuditRecordAggregate.Record(
            DefaultUserGuid,
            SubjectType.System,
            "ContractTestEvent",
            "ContractTestEvent",
            AuditResult.Success,
            DefaultUserGuid,
            "ContractTest",
            DefaultTenantGuid,
            null);

        if (result.IsFailure) return;

        var record = result.Value;
        if (id != Guid.Empty) SetEntityId(record, id);

        await auditRecords.AppendAsync(record);
        await auditRecords.UnitOfWork.SaveEntitiesAsync(record);
    }

    // ── Configuration: FeatureFlag ─────────────────────────────────
    private static async Task EnsureFeatureFlagExistsAsync(string state, IFeatureFlagRepository featureFlags)
    {
        var id = ExtractGuid(state);
        if (id == Guid.Empty) return;
        if (await featureFlags.GetByIdAsync(id) is not null) return;
        await SeedFeatureFlagAsync(id, featureFlags);
    }

    private static async Task SeedFeatureFlagAsync(Guid id, IFeatureFlagRepository featureFlags)
    {
        var result = FeatureFlagAggregate.Create(
            IdValueObject.Load(SeedRefGuidA),
            null,
            "CONTRACT_TEST_FLAG",
            FlagType.Boolean,
            "all",
            null,
            null,
            null,
            SeedActor);

        if (result.IsFailure) return;

        var flag = result.Value;
        if (id != Guid.Empty) SetEntityId(flag, id);

        await featureFlags.AddAsync(flag);
        await featureFlags.UnitOfWork.SaveEntitiesAsync();
    }

    // ── Configuration: AppConfiguration ────────────────────────────
    private static async Task SeedAppConfigurationAsync(IAppConfigurationRepository appConfigurations)
    {
        var result = AppConfigurationAggregate.Create(
            null,
            null,
            null,
            Code.Create("CONTRACT_TEST_CFG"),
            ConfigurationValue.Create("contract-test-value"),
            Description.Create("Contract test configuration."),
            true,
            false,
            SeedActor,
            false);

        if (result.IsFailure) return;

        await appConfigurations.AddAsync(result.Value);
        await appConfigurations.UnitOfWork.SaveEntitiesAsync();
    }

    // ── IGA: RolePromotionRequest ──────────────────────────────────
    private static async Task EnsureRolePromotionRequestExistsAsync(string state, IRolePromotionRequestRepository requests)
    {
        var id = ExtractGuid(state);
        if (id == Guid.Empty) return;
        if (await requests.GetByIdAsync(id) is not null) return;
        await SeedRolePromotionRequestAsync(id, requests);
    }

    private static async Task SeedRolePromotionRequestAsync(Guid id, IRolePromotionRequestRepository requests)
    {
        var result = RolePromotionRequestAggregate.Create(
            TenantId.Load(DefaultTenantGuid),
            UserId.Load(SeedRefGuidA),
            UserId.Load(SeedRefGuidB),
            RoleId.Load(SeedRefGuidA),
            RoleId.Load(SeedRefGuidB),
            SeedActor);

        if (result.IsFailure) return;

        var request = result.Value;
        if (id != Guid.Empty) SetEntityId(request, id);

        await requests.AddAsync(request);
        await requests.UnitOfWork.SaveEntitiesAsync();
    }

    private static Guid ExtractGuid(string state)
    {
        foreach (var part in state.Split(' '))
        {
            if (Guid.TryParse(part, out var guid))
            {
                return guid;
            }
        }
        return Guid.Empty;
    }
}

/// <summary>Body shape posted by PactNet verifier to set up provider state.</summary>
internal sealed record ProviderStateRequest(string State, Dictionary<string, string>? Params = null);
