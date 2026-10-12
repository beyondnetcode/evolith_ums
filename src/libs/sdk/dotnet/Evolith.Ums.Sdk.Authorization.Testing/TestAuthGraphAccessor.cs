using Evolith.Ums.Sdk.Contracts;

namespace Evolith.Ums.Sdk.Authorization.Testing;

/// <summary>
/// Trivial <see cref="IAuthGraphAccessor"/> backed by a single in-memory graph.
/// Useful for unit tests that wire the validator directly without DI.
/// </summary>
public sealed class TestAuthGraphAccessor : IAuthGraphAccessor
{
    public AuthorizationGraph? Current { get; set; }

    public TestAuthGraphAccessor() { }
    public TestAuthGraphAccessor(AuthorizationGraph? graph) { Current = graph; }
}
