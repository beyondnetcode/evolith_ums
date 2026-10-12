using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Auth;

namespace Evolith.Ums.Application.Authorization.Graph;

/// <summary>
/// Generates the same AuthorizationGraph produced by POST /api/v1/client/authenticate
/// without requiring external credentials. Credential validation is the only step
/// omitted — all graph rules, tenant parameters, scopes, format resolution, and
/// audit recording still execute through the same pipeline.
///
/// Flow (mirrors AuthenticateUserCommandHandler.BuildResultAsync):
///   1. Load Profile → User (target of the graph)
///   2. Load Tenant; resolve the tenant's configured auth method for context accuracy
///   3. IAuthorizationGraphBuilder.BuildAsync(user, tenantId, authMethod)
///   4. Serialize with default serializer + record the default format
///   5. Audit with EventType = "Graph.Preview.Internal"
///   6. Return graph + serialized + format so the Presentation layer can
///      override format via Shell.Factory (identical to ClientAuthEndpoints pattern)
/// </summary>
public sealed class PreviewProfileAuthGraphCommandHandler(
    IProfileRepository            profileRepo,
    IUserAccountRepository        userRepo,
    ITenantRepository             tenantRepo,
    IAuthorizationGraphBuilder    graphBuilder,
    IAuthGraphFormatProvider      formatProvider,
    IAuthMethodResolver           methodResolver,
    IAuthorizationGraphSerializer defaultSerializer,
    IAuthAuditService             auditService)
    : ICommandHandler<PreviewProfileAuthGraphCommand, PreviewProfileAuthGraphResult>
{
    [AuthorizationAspect(ResourceCode = "manage_profiles", ActionCode = "manage")]
    public async Task<Result<PreviewProfileAuthGraphResult>> Handle(
        PreviewProfileAuthGraphCommand command,
        CancellationToken              cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString();

        // ── 1. Profile ────────────────────────────────────────────────────────
        var profile = await profileRepo.GetByIdAsync(command.ProfileId, cancellationToken);
        if (profile is null)
            return Result<PreviewProfileAuthGraphResult>.Failure("Profile not found.");

        var tenantId = profile.Props.TenantId.GetValue();
        var userId   = profile.Props.UserId.GetValue();

        // ── 2. Tenant ─────────────────────────────────────────────────────────
        var tenant = await tenantRepo.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return Result<PreviewProfileAuthGraphResult>.Failure("Tenant not found.");

        // ── 3. User (the profile owner whose graph we are previewing) ──────────
        var user = await userRepo.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result<PreviewProfileAuthGraphResult>.Failure(
                "User account not found for this profile.");

        // ── 4. Resolve tenant's configured auth method (for graph context node)
        // Preview bypasses credential validation but keeps the tenant auth mode
        // so the graph's Authentication node reflects the real configuration.
        // Unlike public external auth, internal preview must still render even
        // when the tenant is configured for IDP but an active provider has not
        // been activated yet.
        var methodResult = await methodResolver.ResolveAsync(
            tenantId,
            AuthAccessScope.InternalPreview,
            cancellationToken: cancellationToken);

        if (methodResult.IsFailure)
        {
            return Result<PreviewProfileAuthGraphResult>.Failure(methodResult.Error);
        }

        var authMethod = methodResult.Value;

        // ── 5. Build graph for the exact requested profile ────────────────────
        // Sin acotar por sistema: la vista previa es una herramienta de administración y su
        // usuario quiere ver los perfiles del sujeto tal como son, no filtrados por el sistema
        // desde el que mira.
        var graphResult = await graphBuilder.BuildForProfileAsync(
            user, tenantId, command.ProfileId, authMethod, systemCode: null, cancellationToken);

        if (graphResult.IsFailure)
        {
            await RecordAuditAsync(
                command, tenantId, userId, tenant.Props.Code.GetValue(),
                authMethod.Type.ToString(), false, requestId,
                graphResult.Error, cancellationToken);

            return Result<PreviewProfileAuthGraphResult>.Failure(graphResult.Error);
        }

        var graph = graphResult.Value;

        // ── 6. Serialize with default serializer (Presentation layer can override)
        var defaultFormat = await formatProvider.GetDefaultFormatAsync(tenantId, cancellationToken);
        var serialized    = defaultSerializer.Serialize(graph);

        // ── 7. Audit — internal preview event ─────────────────────────────────
        await RecordAuditAsync(
            command, tenantId, userId, tenant.Props.Code.GetValue(),
            authMethod.Type.ToString(), true, requestId,
            null, cancellationToken);

        return Result<PreviewProfileAuthGraphResult>.Success(new PreviewProfileAuthGraphResult(
            Graph:          graph,
            SerializedGraph: serialized,
            GraphFormat:    defaultFormat,
            RequestId:      requestId,
            ProfileId:      command.ProfileId,
            UserId:         userId,
            TenantId:       tenantId,
            TenantCode:     tenant.Props.Code.GetValue(),
            AuthMethodUsed: authMethod.Type.ToString()));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S1172:Unused method parameters should be removed",
        Justification = "'profileUserId' y 'requestId' se conservan como contexto de auditoría reservado; " +
                        "aún no se proyectan en AuthAuditEvent (pendiente de enriquecer el evento, ver G-016).")]
    private Task RecordAuditAsync(
        PreviewProfileAuthGraphCommand command,
        Guid   tenantId,
        Guid   profileUserId,
        string tenantCode,
        string authMethod,
        bool   succeeded,
        string requestId,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        var evt = new AuthAuditEvent(
            UserId:        Guid.TryParse(command.AdminUserId, out var adminId) ? adminId : Guid.Empty,
            TenantId:      tenantId,
            TenantCode:    tenantCode,
            AuthMethod:    authMethod,
            EventType:     "Graph.Preview.Internal",
            Succeeded:     succeeded,
            ClientIp:      command.ClientIp,
            FailureReason: failureReason,
            IdpProvider:   null);

        return auditService.RecordAuthEventAsync(evt, cancellationToken);
    }
}
