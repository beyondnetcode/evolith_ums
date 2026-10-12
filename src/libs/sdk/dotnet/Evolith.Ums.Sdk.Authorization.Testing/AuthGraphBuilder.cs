using Evolith.Ums.Sdk.Contracts;

namespace Evolith.Ums.Sdk.Authorization.Testing;

/// <summary>
/// Fluent builder for fabricating valid <see cref="AuthorizationGraph"/> instances in tests.
/// Designed to make consumer-code unit tests trivial: declare exactly the permissions you need,
/// no JSON, no UMS, no HTTP.
/// </summary>
public sealed class AuthGraphBuilder
{
    private string _schemaVersion = SchemaVersion.Current;
    private string _tenantCode = "TEST_TENANT";
    private string _tenantName = "Test Tenant";
    private string _userEmail = "test.user@example.com";
    private string _systemSuiteCode = "TEST_SUITE";
    private string _roleCode = "TEST_ROLE";
    private string _profileScope = "OrgWide";
    private bool _branchScoped;
    private string _branchCode = "TEST_BRANCH";
    private string _authMethod = "Local";
    private IdpProviderRef? _provider;
    private TimeSpan _validity = TimeSpan.FromHours(1);
    private readonly List<string> _scopes = new();
    private readonly List<(string Resource, string Action, AccessEffect Effect, PermissionSource Source)> _domainPerms = new();
    private readonly List<(string OptionCode, string ActionCode, AccessEffect Effect, PermissionSource Source)> _menuOptions = new();
    private readonly List<(string FlagCode, bool Enabled, string? MatchedCriteriaType)> _flags = new();

    public static AuthGraphBuilder ForTenant(string tenantCode)
    {
        var b = new AuthGraphBuilder { _tenantCode = tenantCode };
        return b;
    }

    public AuthGraphBuilder WithTenantName(string name) { _tenantName = name; return this; }
    public AuthGraphBuilder WithUser(string email) { _userEmail = email; return this; }
    public AuthGraphBuilder WithSystemSuite(string code) { _systemSuiteCode = code; return this; }
    public AuthGraphBuilder WithRole(string code) { _roleCode = code; return this; }
    public AuthGraphBuilder WithBranchScopedProfile(string branchCode)
    {
        _profileScope = "BranchScoped";
        _branchScoped = true;
        _branchCode = branchCode;
        return this;
    }
    public AuthGraphBuilder WithSchemaVersion(string version) { _schemaVersion = version; return this; }
    public AuthGraphBuilder WithValidity(TimeSpan duration) { _validity = duration; return this; }
    public AuthGraphBuilder WithIdpAuth(string providerName, string providerCode, string strategy)
    {
        _authMethod = "IDP";
        _provider = new IdpProviderRef(providerName, providerCode, strategy);
        return this;
    }
    public AuthGraphBuilder WithScope(string scope) { _scopes.Add(scope); return this; }
    public AuthGraphBuilder WithDeny(string scope)
    {
        var (r, a) = ParseScope(scope);
        if (r is null || a is null)
            throw new ArgumentException($"Invalid scope format '{scope}'. Expected 'RESOURCE.ACTION'.", nameof(scope));
        _domainPerms.Add((r, a, AccessEffect.Deny, PermissionSource.Override));
        return this;
    }
    public AuthGraphBuilder WithDomainPermission(string resourceCode, string actionCode,
        AccessEffect effect = AccessEffect.Allow, PermissionSource source = PermissionSource.Template)
    {
        _domainPerms.Add((resourceCode, actionCode, effect, source));
        if (effect == AccessEffect.Allow) _scopes.Add($"{resourceCode}.{actionCode}");
        return this;
    }
    public AuthGraphBuilder WithMenuOption(string optionCode, string actionCode,
        AccessEffect effect = AccessEffect.Allow, PermissionSource source = PermissionSource.Template)
    {
        _menuOptions.Add((optionCode, actionCode, effect, source));
        if (effect == AccessEffect.Allow) _scopes.Add($"{optionCode}.{actionCode}");
        return this;
    }
    public AuthGraphBuilder WithFeatureFlag(string flagCode, bool enabled = true, string? matchedCriteriaType = null)
    {
        _flags.Add((flagCode, enabled, enabled ? (matchedCriteriaType ?? "TenantId") : null));
        return this;
    }

    public AuthorizationGraph Build()
    {
        var now = DateTimeOffset.UtcNow;
        var validUntil = now.Add(_validity);

        var user = new UserSummary(
            _userEmail,
            _userEmail.Split('@')[0],
            _userEmail,
            "Active");

        var tenant = new TenantSummary(_tenantCode, _tenantName, "Active", false);
        var suite = new SystemSuiteSummary(_systemSuiteCode, _systemSuiteCode, "Active");
        var role = new RoleSummary(_roleCode, _roleCode, 1);
        var profile = new ProfileSummary(_profileScope, true);
        BranchSummary? branch = _branchScoped
            ? new BranchSummary(_branchCode, _branchCode)
            : null;

        var context = new PrincipalContext(user, tenant, suite, role, profile, branch);
        var authentication = new AuthenticationMetadata(_authMethod, _provider, false, now, validUntil);

        var actions = new List<ActionRef>();
        var menuAccess = BuildMenuAccess(actions);
        var domainPermissions = BuildDomainPermissions(actions);
        var featureFlags = _flags
            .Select(f => new FeatureFlagState(f.FlagCode, f.Enabled, f.MatchedCriteriaType))
            .ToList();

        var effectiveConfig = new EffectiveConfig(60, 5, 12, true, new[] { "Totp" }, 3600000, _authMethod == "IDP");

        return new AuthorizationGraph(
            _schemaVersion,
            false,
            context,
            authentication,
            actions,
            [],                       // profiles: el constructor de pruebas no modela multi-perfil
            menuAccess,
            domainPermissions,
            featureFlags,
            effectiveConfig,
            new Dictionary<string, IReadOnlyDictionary<string, string>>(),   // settings
            _scopes.Distinct(StringComparer.Ordinal).ToList(),
            now,
            validUntil);
    }

    public AuthorizationGraph BuildExpired()
    {
        var graph = Build();
        var past = DateTimeOffset.UtcNow.AddYears(-5);
        return graph with { GeneratedAt = past, ValidUntil = past.AddHours(1) };
    }

    private List<MenuModule> BuildMenuAccess(List<ActionRef> actions)
    {
        if (_menuOptions.Count == 0) return new();

        // Un nodo hoja por opción, agrupando sus acciones: la forma del contrato v2.0.0.
        var hojas = _menuOptions
            .GroupBy(m => m.OptionCode, StringComparer.Ordinal)
            .Select(grupo =>
            {
                var acciones = grupo.Select(m =>
                {
                    var existing = actions.FirstOrDefault(a => a.Code == m.ActionCode);
                    var actionRef = existing ?? new ActionRef(m.ActionCode, m.ActionCode);
                    if (existing is null) actions.Add(actionRef);
                    return new NodeAction(m.ActionCode, m.Effect, m.Source);
                }).ToList();

                return new NavigationNode(grupo.Key, grupo.Key, "Option", 1, null, $"/{grupo.Key.ToLowerInvariant()}", acciones, []);
            })
            .ToList();

        var submenu = new NavigationNode("TEST_SUB", "Test SubMenu", "SubMenu", 1, null, null, [], hojas);
        var menu = new NavigationNode("TEST_MENU", "Test Menu", "Menu", 1, null, null, [], [submenu]);
        return new() { new MenuModule("TEST_MODULE", "Test Module", 1, "Active", "layout-grid", [menu]) };
    }

    private List<DomainResourcePermissions> BuildDomainPermissions(List<ActionRef> actions)
    {
        if (_domainPerms.Count == 0) return new();

        return _domainPerms
            .GroupBy(d => d.Resource)
            .Select(grp =>
            {
                var resolutions = grp.Select(d =>
                {
                    var existing = actions.FirstOrDefault(a => a.Code == d.Action);
                    var actionRef = existing ?? new ActionRef(d.Action, d.Action);
                    if (existing is null) actions.Add(actionRef);
                    return new DomainActionResolution(d.Action, d.Action, d.Effect, d.Source);
                }).ToList();
                return new DomainResourcePermissions("Aggregate", grp.Key, grp.Key, resolutions);
            })
            .ToList();
    }

    private static (string? Resource, string? Action) ParseScope(string scope)
    {
        var idx = scope.IndexOf('.');
        if (idx <= 0 || idx == scope.Length - 1) return (null, null);
        return (scope[..idx], scope[(idx + 1)..]);
    }
}
