using Evolith.Ums.Domain.Authorization.Graph;

namespace Evolith.Ums.Application.Authorization.Graph;

/// <summary>
/// Internal preview command: generates the same auth graph that a client system
/// receives from POST /api/v1/client/authenticate, without requiring external
/// credentials. Skips credential validation only; all graph rules, tenant
/// parameters, scopes, and format resolution still apply.
///
/// Must only be invoked from the UMS admin UI under an authenticated session.
/// </summary>
public sealed record PreviewProfileAuthGraphCommand(
    Guid   ProfileId,
    string  AdminUserId,  // UMS admin user requesting the preview (for audit)
    string  ClientIp) : ICommand<PreviewProfileAuthGraphResult>;

/// <summary>Result returned to the Presentation layer for format override and HTTP response.</summary>
public sealed record PreviewProfileAuthGraphResult(
    AuthorizationGraph Graph,            // full graph object for Presentation-layer re-serialization
    string             SerializedGraph,  // pre-serialized in tenant default format
    string             GraphFormat,      // default format used: "JSON"|"XML"|"YAML"|"CSV"
    string             RequestId,        // correlates with the audit record
    Guid               ProfileId,
    Guid               UserId,
    Guid               TenantId,
    string             TenantCode,
    string             AuthMethodUsed);  // "Local" | "IDP"
