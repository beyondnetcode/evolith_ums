using FluentAssertions;
using Evolith.Ums.Sdk.Authorization;
using Evolith.Ums.Sdk.Contracts;
using Xunit;

namespace Evolith.Ums.Sdk.Tests;

/// <summary>
/// Validator behavioral tests driven by the canonical golden fixtures from
/// <c>src/libs/sdk/contracts/fixtures/</c>. Every fixture maps to one or more assertions
/// that must hold across all three SDK runtimes — these tests document the .NET side of that contract.
/// </summary>
public sealed class AuthorizationValidatorTests
{
    private static readonly IAuthorizationValidator Validator = new AuthorizationValidator();

    [Fact]
    public void LocalAuthSuccess_GrantsViewScopes_DeniesNotGrantedScopes()
    {
        var graph = FixtureLoader.Load("local-auth-success");
        // Ensure graph is in the future for this test run
        graph = MakeValid(graph);

        Validator.RequireScope(graph, "STOCK_VIEW.VIEW").IsGranted.Should().BeTrue();
        Validator.RequireScope(graph, "PURCHASE_ORDER.VIEW").IsGranted.Should().BeTrue();

        var notGranted = Validator.RequireScope(graph, "PURCHASE_ORDER.APPROVE");
        notGranted.IsGranted.Should().BeFalse();
        notGranted.ErrorCode.Should().Be("AUTH_101");
    }

    [Fact]
    public void IdpAuthSuccess_HasBranchScopedProfile_AndIdpProvider()
    {
        var graph = FixtureLoader.Load("idp-auth-success");
        graph = MakeValid(graph);

        graph.Context.Branch.Should().NotBeNull();
        graph.Context.Profile!.Scope.Should().Be("BranchScoped");
        graph.Authentication.Method.Should().Be("IDP");
        graph.Authentication.Provider!.Value.Should().Be("AZURE_AD");

        Validator.RequireDomainAccess(graph, "BRANCH_INVENTORY", "APPROVE").IsGranted.Should().BeTrue();
        Validator.RequireScope(graph, "BRANCH_INVENTORY.VIEW").IsGranted.Should().BeTrue();
    }

    [Fact]
    public void DenyWins_OverridesAnyAllowOnTheSameScope()
    {
        var graph = FixtureLoader.Load("deny-wins");
        graph = MakeValid(graph);

        var decision = Validator.RequireDomainAccess(graph, "PURCHASE_ORDER", "DELETE");
        decision.IsGranted.Should().BeFalse();
        decision.ErrorCode.Should().Be("AUTH_106");
        decision.Status.Should().Be(AuthorizationDecisionStatus.Denied);

        var menuDecision = Validator.RequireMenuOption(graph, "STOCK_DELETE");
        menuDecision.IsGranted.Should().BeFalse();
        menuDecision.ErrorCode.Should().Be("AUTH_104");
    }

    [Fact]
    public void OverrideAllow_PromotesEffectToAllow()
    {
        var graph = FixtureLoader.Load("override-allow");
        graph = MakeValid(graph);

        var decision = Validator.RequireDomainAccess(graph, "PURCHASE_ORDER", "APPROVE");
        decision.IsGranted.Should().BeTrue();
    }

    [Fact]
    public void EmptyPermissions_DeniesAllProbesAsNotGranted()
    {
        var graph = FixtureLoader.Load("empty-permissions");
        graph = MakeValid(graph);

        Validator.RequireScope(graph, "ANY.SCOPE").ErrorCode.Should().Be("AUTH_101");
        Validator.RequireMenuOption(graph, "ANY_MENU").ErrorCode.Should().Be("AUTH_103");
        Validator.RequireDomainAccess(graph, "ANY", "ACTION").ErrorCode.Should().Be("AUTH_105");
        Validator.RequireFeatureFlag(graph, "ANY_FLAG").ErrorCode.Should().Be("AUTH_108");
    }

    [Fact]
    public void ExpiredGraph_FailsWithAuth201_RegardlessOfContents()
    {
        var graph = FixtureLoader.Load("expired-graph");

        // Do NOT MakeValid — we want this fixture's actual past validUntil.
        var decision = Validator.RequireScope(graph, "PURCHASE_ORDER.VIEW");
        decision.IsGranted.Should().BeFalse();
        decision.ErrorCode.Should().Be("AUTH_201");
        decision.Status.Should().Be(AuthorizationDecisionStatus.Expired);
    }

    [Fact]
    public void FeatureFlagMatched_IsEnabled_ReportsGranted()
    {
        var graph = FixtureLoader.Load("feature-flag-matched");
        graph = MakeValid(graph);

        Validator.RequireFeatureFlag(graph, "WMS_NEW_PICKING_UI").IsGranted.Should().BeTrue();
        Validator.RequireFeatureFlag(graph, "WMS_EXPRESS_CHECKOUT").IsGranted.Should().BeTrue();
    }

    [Fact]
    public void FeatureFlagMissedContext_DeniesWithAuth107()
    {
        var graph = FixtureLoader.Load("feature-flag-missed-context");
        graph = MakeValid(graph);

        var decision = Validator.RequireFeatureFlag(graph, "WMS_NEW_PICKING_UI");
        decision.IsGranted.Should().BeFalse();
        decision.ErrorCode.Should().Be("AUTH_107");
    }

    [Fact]
    public void MultiTenantRejection_AssertTenant_FailsWithAuth109()
    {
        var graph = FixtureLoader.Load("multi-tenant-rejection");
        graph = MakeValid(graph);

        var decision = Validator.AssertTenant(graph, "LOGISTICS_CORE");
        decision.IsGranted.Should().BeFalse();
        decision.ErrorCode.Should().Be("AUTH_109");
        decision.Status.Should().Be(AuthorizationDecisionStatus.TenantMismatch);

        Validator.AssertTenant(graph, "ACME_RETAIL").IsGranted.Should().BeTrue();
    }

    [Fact]
    public void SchemaUnsupportedMajor_RejectsWithAuth205()
    {
        var graph = FixtureLoader.Load("schema-unsupported-major") with
        {
            ValidUntil = DateTimeOffset.UtcNow.AddHours(1)
        };

        var decision = Validator.RequireScope(graph, "ANY.SCOPE");
        decision.IsGranted.Should().BeFalse();
        decision.ErrorCode.Should().Be("AUTH_205");
        decision.Status.Should().Be(AuthorizationDecisionStatus.SchemaUnsupported);
    }

    [Fact]
    public void MissingGraph_RejectsWithAuth202()
    {
        var decision = Validator.RequireScope(null, "ANY.SCOPE");
        decision.ErrorCode.Should().Be("AUTH_202");
        decision.Status.Should().Be(AuthorizationDecisionStatus.GraphMissing);
    }

    private static AuthorizationGraph MakeValid(AuthorizationGraph g) =>
        g with { ValidUntil = DateTimeOffset.UtcNow.AddHours(1) };
}
