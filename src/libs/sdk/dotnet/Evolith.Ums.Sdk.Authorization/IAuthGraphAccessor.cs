using Evolith.Ums.Sdk.Contracts;

namespace Evolith.Ums.Sdk.Authorization;

/// <summary>
/// Port for retrieving the AuthorizationGraph applicable to the current execution scope.
/// Implementations: <see cref="HttpContextAuthGraphAccessor"/> for ASP.NET Core, or any
/// custom adapter for workers, CLI, or IExecutionContextAccessor integration (ADR-0061).
/// </summary>
public interface IAuthGraphAccessor
{
    /// <summary>
    /// The graph for the current scope, or null when no authenticated session is bound.
    /// Callers must not cache this value across scope boundaries.
    /// </summary>
    AuthorizationGraph? Current { get; }
}
