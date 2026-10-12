using Evolith.Ums.Sdk.Contracts;

namespace Evolith.Ums.Sdk.Authorization;

/// <summary>
/// Pure validator port: given an <see cref="AuthorizationGraph"/> and a probe, returns a decision.
/// No I/O, no logging, no DI dependencies. Used by the AOP aspect, NestJS guards, and any
/// imperative caller (controllers, Blazor components, workers).
/// </summary>
public interface IAuthorizationValidator
{
    AuthorizationDecision RequireScope(AuthorizationGraph? graph, string scope);
    AuthorizationDecision RequireMenuOption(AuthorizationGraph? graph, string optionCode);
    AuthorizationDecision RequireDomainAccess(AuthorizationGraph? graph, string resourceCode, string actionCode);
    AuthorizationDecision RequireFeatureFlag(AuthorizationGraph? graph, string flagCode);
    AuthorizationDecision AssertTenant(AuthorizationGraph? graph, string expectedTenantCode);
}
