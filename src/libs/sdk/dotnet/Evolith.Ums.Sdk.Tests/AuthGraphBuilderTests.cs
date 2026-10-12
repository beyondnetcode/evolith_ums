using FluentAssertions;
using Evolith.Ums.Sdk.Authorization;
using Evolith.Ums.Sdk.Authorization.Testing;
using Evolith.Ums.Sdk.Contracts;
using Xunit;

namespace Evolith.Ums.Sdk.Tests;

public sealed class AuthGraphBuilderTests
{
    private static readonly IAuthorizationValidator Validator = new AuthorizationValidator();

    [Fact]
    public void Build_ProducesValidGraph_WithProvidedScopes()
    {
        var graph = AuthGraphBuilder
            .ForTenant("LOGISTICS_CORE")
            .WithUser("ana.flores@example.com")
            .WithScope("PURCHASE_ORDER.VIEW")
            .WithScope("PURCHASE_ORDER.APPROVE")
            .Build();

        graph.SchemaVersion.Should().Be(SchemaVersion.Current);
        graph.Context.Tenant.Code.Should().Be("LOGISTICS_CORE");
        graph.Scopes.Should().Contain("PURCHASE_ORDER.VIEW").And.Contain("PURCHASE_ORDER.APPROVE");

        Validator.RequireScope(graph, "PURCHASE_ORDER.APPROVE").IsGranted.Should().BeTrue();
    }

    [Fact]
    public void WithDeny_AddsDenyOverride_ThatBeatsScopeAllowance()
    {
        var graph = AuthGraphBuilder
            .ForTenant("LOGISTICS_CORE")
            .WithScope("STOCK.DELETE")        // claim Allow via scope
            .WithDeny("STOCK.DELETE")          // but also Deny override in domainPermissions
            .Build();

        var decision = Validator.RequireScope(graph, "STOCK.DELETE");
        decision.IsGranted.Should().BeFalse();
        decision.ErrorCode.Should().Be("AUTH_102");   // explicit deny
    }

    [Fact]
    public void BuildExpired_ProducesGraph_WithPastValidUntil()
    {
        var graph = AuthGraphBuilder
            .ForTenant("LOGISTICS_CORE")
            .WithScope("ANY.VIEW")
            .BuildExpired();

        graph.ValidUntil.Should().BeBefore(DateTimeOffset.UtcNow);

        Validator.RequireScope(graph, "ANY.VIEW").ErrorCode.Should().Be("AUTH_201");
    }

    [Fact]
    public void WithFeatureFlag_EnabledTrue_IsGranted()
    {
        var graph = AuthGraphBuilder
            .ForTenant("LOGISTICS_CORE")
            .WithFeatureFlag("WMS_NEW_PICKING_UI", enabled: true, "BranchId")
            .Build();

        Validator.RequireFeatureFlag(graph, "WMS_NEW_PICKING_UI").IsGranted.Should().BeTrue();
    }

    [Fact]
    public void WithBranchScopedProfile_PopulatesBranch_AndProfileScope()
    {
        var graph = AuthGraphBuilder
            .ForTenant("LOGISTICS_CORE")
            .WithBranchScopedProfile("CALLAO_DC")
            .Build();

        graph.Context.Branch.Should().NotBeNull();
        graph.Context.Branch!.Code.Should().Be("CALLAO_DC");
        graph.Context.Profile.Scope.Should().Be("BranchScoped");
    }

    [Fact]
    public void TestAuthGraphAccessor_ReturnsConfiguredGraph()
    {
        var graph = AuthGraphBuilder.ForTenant("LOGISTICS_CORE").Build();
        var accessor = new TestAuthGraphAccessor(graph);
        accessor.Current.Should().BeSameAs(graph);
    }
}
