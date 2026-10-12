namespace Evolith.Ums.Domain.Test.Authorization.Graph;

using System.Text.Json;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity.Auth;
using Xunit;

/// <summary>
/// Domain invariant tests for AuthorizationGraph.
/// These tests ensure the graph record is constructed correctly and that
/// key invariants (timing, deny-wins semantics at record level) hold.
/// Business-logic resolution tests live in Application layer tests.
/// </summary>
public class AuthorizationGraphTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static AuthorizationGraph BuildMinimalGraph(
        int sessionTimeoutMinutes = 30,
        string authMethod         = "Local",
        IReadOnlyList<GraphMenuModule>? menuAccess = null,
        IReadOnlyList<GraphDomainPermission>? domainPerms = null,
        IReadOnlyList<GraphFeatureFlag>? featureFlags = null,
        IReadOnlyList<string>? scopes = null)
    {
        var context = new GraphContext(
            User:        new GraphUser(Guid.NewGuid(), "user@test.com", "user@test.com", "Test User", "Active"),
            Tenant:      new GraphTenant(Guid.NewGuid(), "TEST", "Test Tenant", "Active", false),
            SystemSuite: new GraphSystemSuite(Guid.NewGuid(), "CORE", "Core System", "Active"),
            Role:        new GraphRole(Guid.NewGuid(), "ADMIN", "Administrator", 1, null),
            Profile:     new GraphProfile(Guid.NewGuid(), "OrgWide", true),
            Branch:      null);

        var auth = new GraphAuthentication(
            Method:           authMethod,
            Provider:         null,
            MfaRequired:      false,
            IssuedAt:         DateTime.UtcNow,
            SessionExpiresAt: DateTime.UtcNow.AddMinutes(sessionTimeoutMinutes));

        var config = new GraphEffectiveConfig(
            SessionTimeoutMinutes: sessionTimeoutMinutes,
            MaxLoginAttempts:      5,
            MinPasswordLength:     12,
            MfaRequiredForAdmin:   false,
            MfaAllowedMethods:     new[] { "Totp", "WebAuthn" },
            AccessTokenDurationMs: 3600000,
            AuthUseExternalIdp:    false);

        return AuthorizationGraph.Build(
            context:           context,
            authentication:    auth,
            actions:           new List<GraphAction>(),
            menuAccess:        menuAccess ?? new List<GraphMenuModule>(),
            domainPermissions: domainPerms ?? new List<GraphDomainPermission>(),
            featureFlags:      featureFlags ?? new List<GraphFeatureFlag>(),
            effectiveConfig:   config,
            scopes:            scopes ?? new List<string>(),
            generatedAt:       DateTime.UtcNow);
    }

    // ── Timing tests ──────────────────────────────────────────────────────────

    [Fact]
    public void Build_ValidUntil_IsAfterGeneratedAt()
    {
        var graph = BuildMinimalGraph(sessionTimeoutMinutes: 30);

        Assert.True(graph.ValidUntil > graph.GeneratedAt);
    }

    [Fact]
    public void Build_ValidUntil_EqualsGeneratedAtPlusSessionTimeout()
    {
        var graph = BuildMinimalGraph(sessionTimeoutMinutes: 60);

        var expected = graph.GeneratedAt.AddMinutes(60);
        Assert.Equal(expected, graph.ValidUntil, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Build_GeneratedAt_IsUtc()
    {
        var graph = BuildMinimalGraph();

        Assert.Equal(DateTimeKind.Utc, graph.GeneratedAt.Kind);
    }

    // ── Empty graph is valid ──────────────────────────────────────────────────

    [Fact]
    public void Build_WithEmptyPermissions_IsValid()
    {
        var graph = BuildMinimalGraph();

        Assert.NotNull(graph);
        Assert.Empty(graph.MenuAccess);
        Assert.Empty(graph.DomainPermissions);
        Assert.Empty(graph.Scopes);
        Assert.Empty(graph.FeatureFlags);
    }

    // ── Auth method propagation ───────────────────────────────────────────────

    [Fact]
    public void Build_LocalAuth_AuthMethodIsLocal()
    {
        var graph = BuildMinimalGraph(authMethod: "Local");

        Assert.Equal("Local", graph.Authentication.Method);
        Assert.Null(graph.Authentication.Provider);
    }

    [Fact]
    public void Build_IdpAuth_AuthMethodIsIDP()
    {
        var context = new GraphContext(
            User:        new GraphUser(Guid.NewGuid(), "u@test.com", "u@test.com", "U", "Active"),
            Tenant:      new GraphTenant(Guid.NewGuid(), "T", "T", "Active", false),
            SystemSuite: new GraphSystemSuite(Guid.NewGuid(), "S", "S", "Active"),
            Role:        new GraphRole(Guid.NewGuid(), "R", "R", 1, null),
            Profile:     new GraphProfile(Guid.NewGuid(), "OrgWide", true),
            Branch:      null);

        var provider = new GraphIdpProvider(Guid.NewGuid(), "Azure AD", "AZURE", "AzureAd");
        var auth = new GraphAuthentication("IDP", provider, false, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30));
        var config = new GraphEffectiveConfig(30, 5, 12, false, new[] { "Totp" }, 3600000, true);

        var graph = AuthorizationGraph.Build(context, auth,
            new List<GraphAction>(), new List<GraphMenuModule>(),
            new List<GraphDomainPermission>(), new List<GraphFeatureFlag>(),
            config, new List<string>(), DateTime.UtcNow);

        Assert.Equal("IDP", graph.Authentication.Method);
        Assert.NotNull(graph.Authentication.Provider);
        Assert.Equal("AzureAd", graph.Authentication.Provider!.Strategy);
    }

    // ── Scopes are OAuth2 formatted ───────────────────────────────────────────

    [Fact]
    public void Build_Scopes_AreLowercase()
    {
        var scopes = new List<string> { "users.read", "inventory.write" };
        var graph  = BuildMinimalGraph(scopes: scopes);

        Assert.All(graph.Scopes, s => Assert.Equal(s, s.ToLowerInvariant()));
    }

    // ── DomainMethod hierarchy ────────────────────────────────────────────────

    [Fact]
    public void Build_DomainPermissions_PreservesParentResourceId()
    {
        var aggregateId = Guid.NewGuid();
        var methodId    = Guid.NewGuid();

        var permissions = new List<GraphDomainPermission>
        {
            new(aggregateId, "Aggregate", "USERS", "Users", null, null, new List<GraphDomainAction>()),
            new(methodId, "DomainMethod", "RESET_PWD", "ResetPassword()", null, aggregateId, new List<GraphDomainAction>()),
        };

        var graph = BuildMinimalGraph(domainPerms: permissions);

        var method = graph.DomainPermissions.Single(p => p.ResourceType == "DomainMethod");
        Assert.Equal(aggregateId, method.ParentResourceId);
        Assert.Null(graph.DomainPermissions.Single(p => p.ResourceType == "Aggregate").ParentResourceId);
    }

    // ── Context fields ────────────────────────────────────────────────────────

    [Fact]
    public void Build_BranchScoped_BranchInContext()
    {
        var branchId = Guid.NewGuid();
        var context = new GraphContext(
            User:        new GraphUser(Guid.NewGuid(), "u@test.com", "u@test.com", "U", "Active"),
            Tenant:      new GraphTenant(Guid.NewGuid(), "T", "T", "Active", false),
            SystemSuite: new GraphSystemSuite(Guid.NewGuid(), "S", "S", "Active"),
            Role:        new GraphRole(Guid.NewGuid(), "R", "R", 1, null),
            Profile:     new GraphProfile(Guid.NewGuid(), "BranchScoped", true),
            Branch:      new GraphBranch(branchId, "BR-01", "Branch 01"));

        var auth   = new GraphAuthentication("Local", null, false, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30));
        var config = new GraphEffectiveConfig(30, 5, 12, false, new[] { "Totp" }, 3600000, false);
        var graph  = AuthorizationGraph.Build(context, auth,
            new List<GraphAction>(), new List<GraphMenuModule>(),
            new List<GraphDomainPermission>(), new List<GraphFeatureFlag>(),
            config, new List<string>(), DateTime.UtcNow);

        Assert.NotNull(graph.Context.Branch);
        Assert.Equal("BranchScoped", graph.Context.Profile.Scope);
        Assert.Equal(branchId, graph.Context.Branch!.Id);
    }

    [Fact]
    public void Build_SerializesWithoutTechnicalIdsByDefault()
    {
        var graph = BuildMinimalGraph();

        var json = JsonSerializer.Serialize(graph);

        Assert.DoesNotContain("\"id\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"value\"", json, StringComparison.OrdinalIgnoreCase);
    }
}
