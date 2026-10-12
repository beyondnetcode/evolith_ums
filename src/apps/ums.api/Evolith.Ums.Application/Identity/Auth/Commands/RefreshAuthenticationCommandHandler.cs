using Evolith.Ums.Application.Authorization.Graph;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Application.Identity.Auth;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity.Auth;

namespace Evolith.Ums.Application.Identity.Auth.Commands;

/// <summary>Códigos de error de la renovación (ADR-UMS-091).</summary>
public static class RefreshErrorCodes
{
    public const string Invalid       = "AUTH_REFRESH_001"; // no existe / hash desconocido
    public const string ReuseDetected = "AUTH_REFRESH_002"; // token ya rotado presentado ⇒ familia invalidada
    public const string Revoked       = "AUTH_REFRESH_003"; // token/familia ya revocados
    public const string Expired       = "AUTH_REFRESH_004"; // vencido
    public const string Disabled      = "AUTH_REFRESH_005"; // capacidad no activa (fail-closed)
    public const string MaxRenewals   = "AUTH_REFRESH_006"; // tope de renovaciones alcanzado
    public const string PrincipalGone = "AUTH_REFRESH_007"; // inquilino o usuario inactivo/inexistente
}

/// <summary>
/// Renovación de sesión por refresh token (ADR-UMS-091 / FR-015/016). Orquesta:
///   1. Buscar el token por hash (SHA-256); nunca se compara el plaintext.
///   2. Detección de reuso: presentar un token ya rotado/usado invalida la familia entera.
///   3. Fail-closed: si la política del inquilino está apagada, no se renueva.
///   4. Validar vigencia, tope de renovaciones, y que inquilino y usuario sigan activos
///      (un usuario bloqueado o suspendido no puede renovar → revocación efectiva).
///   5. <b>Regenerar el grafo COMPLETO</b> desde el estado actual (IAuthorizationGraphBuilder).
///   6. Rotar el token (nuevo Active en la familia; el anterior queda Rotated).
///   7. Auditar el evento.
/// </summary>
public sealed class RefreshAuthenticationCommandHandler
    : ICommandHandler<RefreshAuthenticationCommand, RefreshAuthenticationResult>
{
    private readonly IRefreshTokenStore            _store;
    private readonly IRefreshTokenPolicyProvider   _policyProvider;
    private readonly ITenantRepository             _tenantRepo;
    private readonly IUserAccountRepository        _userRepo;
    private readonly IAuthMethodResolver           _methodResolver;
    private readonly IAuthorizationGraphBuilder    _graphBuilder;
    private readonly IAuthGraphFormatProvider      _formatProvider;
    private readonly IAuthorizationGraphSerializer _serializer;
    private readonly IAuthAuditService             _auditService;

    public RefreshAuthenticationCommandHandler(
        IRefreshTokenStore            store,
        IRefreshTokenPolicyProvider   policyProvider,
        ITenantRepository             tenantRepo,
        IUserAccountRepository        userRepo,
        IAuthMethodResolver           methodResolver,
        IAuthorizationGraphBuilder    graphBuilder,
        IAuthGraphFormatProvider      formatProvider,
        IAuthorizationGraphSerializer serializer,
        IAuthAuditService             auditService)
    {
        _store          = store;
        _policyProvider = policyProvider;
        _tenantRepo     = tenantRepo;
        _userRepo       = userRepo;
        _methodResolver = methodResolver;
        _graphBuilder   = graphBuilder;
        _formatProvider = formatProvider;
        _serializer     = serializer;
        _auditService   = auditService;
    }

    public async Task<Result<RefreshAuthenticationResult>> Handle(
        RefreshAuthenticationCommand command,
        CancellationToken            cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Result<RefreshAuthenticationResult>.Failure($"{RefreshErrorCodes.Invalid}: Refresh token is required.");
        }

        var hash = RefreshTokenHasher.Hash(command.RefreshToken);
        var snapshot = await _store.FindByHashAsync(hash, cancellationToken);

        if (snapshot is null)
        {
            await AuditFailureAsync(Guid.Empty, Guid.Empty, command.ClientIp,
                RefreshErrorCodes.Invalid, "Unknown refresh token", cancellationToken);
            return Result<RefreshAuthenticationResult>.Failure($"{RefreshErrorCodes.Invalid}: Invalid refresh token.");
        }

        var now = DateTime.UtcNow;

        // ── Detección de reuso / estado no renovable ──────────────────────────────
        if (snapshot.Status == RefreshTokenStatuses.Revoked)
        {
            await AuditFailureAsync(snapshot.UserId, snapshot.TenantId, command.ClientIp,
                RefreshErrorCodes.Revoked, "Revoked refresh token presented", cancellationToken);
            return Result<RefreshAuthenticationResult>.Failure($"{RefreshErrorCodes.Revoked}: Refresh token has been revoked.");
        }

        if (snapshot.Status is not (RefreshTokenStatuses.Active))
        {
            // Rotated | Used ⇒ el token ya fue canjeado; presentarlo de nuevo es reuso.
            var policyForReuse = _policyProvider.Resolve(snapshot.TenantId);
            if (policyForReuse.DetectReuse)
            {
                await _store.RevokeFamilyAsync(snapshot.FamilyId, "reuse_detected", now, cancellationToken);
            }
            await AuditFailureAsync(snapshot.UserId, snapshot.TenantId, command.ClientIp,
                RefreshErrorCodes.ReuseDetected, "Reuse of a rotated refresh token", cancellationToken);
            return Result<RefreshAuthenticationResult>.Failure($"{RefreshErrorCodes.ReuseDetected}: Refresh token reuse detected; the session family was revoked.");
        }

        if (snapshot.ExpiresAtUtc <= now)
        {
            await AuditFailureAsync(snapshot.UserId, snapshot.TenantId, command.ClientIp,
                RefreshErrorCodes.Expired, "Expired refresh token", cancellationToken);
            return Result<RefreshAuthenticationResult>.Failure($"{RefreshErrorCodes.Expired}: Refresh token has expired.");
        }

        // ── Fail-closed: la capacidad debe seguir activa para el inquilino ────────
        var policy = _policyProvider.Resolve(snapshot.TenantId);
        if (!policy.Enabled)
        {
            await AuditFailureAsync(snapshot.UserId, snapshot.TenantId, command.ClientIp,
                RefreshErrorCodes.Disabled, "Refresh capability disabled for tenant", cancellationToken);
            return Result<RefreshAuthenticationResult>.Failure($"{RefreshErrorCodes.Disabled}: Refresh capability is not enabled for this tenant.");
        }

        // ── Tope de renovaciones (0 = sin tope) ───────────────────────────────────
        if (policy.MaxRenewals > 0 && snapshot.RenewalCount >= policy.MaxRenewals)
        {
            await _store.RevokeFamilyAsync(snapshot.FamilyId, "max_renewals", now, cancellationToken);
            await AuditFailureAsync(snapshot.UserId, snapshot.TenantId, command.ClientIp,
                RefreshErrorCodes.MaxRenewals, "Max renewals reached", cancellationToken);
            return Result<RefreshAuthenticationResult>.Failure($"{RefreshErrorCodes.MaxRenewals}: Maximum number of renewals reached; please sign in again.");
        }

        // ── El inquilino y el usuario deben seguir vigentes ───────────────────────
        var tenant = await _tenantRepo.GetByIdAsync(snapshot.TenantId, cancellationToken);
        if (tenant is null || tenant.Props.Status != Domain.Enums.TenantStatus.Active)
        {
            await _store.RevokeFamilyAsync(snapshot.FamilyId, "tenant_inactive", now, cancellationToken);
            await AuditFailureAsync(snapshot.UserId, snapshot.TenantId, command.ClientIp,
                RefreshErrorCodes.PrincipalGone, "Tenant inactive or missing", cancellationToken);
            return Result<RefreshAuthenticationResult>.Failure($"{RefreshErrorCodes.PrincipalGone}: The tenant is no longer active.");
        }

        var user = await _userRepo.GetByIdAsync(snapshot.UserId, cancellationToken);
        if (user is null
            || user.Props.TenantId.GetValue() != snapshot.TenantId
            || user.Props.Status != Domain.Enums.UserStatus.Active)
        {
            // Usuario bloqueado/suspendido/eliminado ⇒ revocación efectiva de la sesión.
            await _store.RevokeFamilyAsync(snapshot.FamilyId, "user_inactive", now, cancellationToken);
            await AuditFailureAsync(snapshot.UserId, snapshot.TenantId, command.ClientIp,
                RefreshErrorCodes.PrincipalGone, "User inactive or missing", cancellationToken);
            return Result<RefreshAuthenticationResult>.Failure($"{RefreshErrorCodes.PrincipalGone}: The user account is no longer active.");
        }

        // ── Regeneración COMPLETA del grafo (ADR-UMS-091, decisión 1) ────────────────
        var methodResult = await _methodResolver.ResolveAsync(
            snapshot.TenantId, AuthAccessScope.PortalManagement, cancellationToken: cancellationToken);
        if (methodResult.IsFailure)
        {
            await AuditFailureAsync(snapshot.UserId, snapshot.TenantId, command.ClientIp,
                RefreshErrorCodes.PrincipalGone, methodResult.Error, cancellationToken);
            return Result<RefreshAuthenticationResult>.Failure(methodResult.Error);
        }

        var authMethod = methodResult.Value;
        // G-297: acotado por el sistema que el ingreso pidió, que la FAMILIA recuerda. Este canje es
        // anónimo —el refresh token es la única credencial que llega—, así que no hay principal del
        // que leerlo: si no lo guardase la familia, no lo sabría nadie. Con `null`, un sujeto con
        // perfil en dos sistemas renovaba hacia el que ganase el desempate, no hacia el que pidió.
        //
        // Familias anteriores a este cambio traen `SystemCode` nulo y renuevan como antes.
        var graphResult = await _graphBuilder.BuildAsync(
            user, snapshot.TenantId, authMethod, snapshot.SystemCode, cancellationToken);
        if (graphResult.IsFailure)
        {
            await AuditFailureAsync(snapshot.UserId, snapshot.TenantId, command.ClientIp,
                RefreshErrorCodes.PrincipalGone, graphResult.Error, cancellationToken);
            return Result<RefreshAuthenticationResult>.Failure(graphResult.Error);
        }

        var graph = graphResult.Value;
        var format = await _formatProvider.GetDefaultFormatAsync(snapshot.TenantId, cancellationToken);
        var serialized = _serializer.Serialize(graph);

        // ── Rotación del refresh token ────────────────────────────────────────────
        string? newRefreshPlaintext = null;
        int refreshExpiresIn;
        if (policy.Rotate)
        {
            newRefreshPlaintext = RefreshTokenGenerator.Generate();
            var newTokenId  = Guid.NewGuid();
            var expiresAt   = now.AddMinutes(policy.LifetimeMinutes);
            await _store.RotateAsync(
                snapshot,
                newTokenId,
                RefreshTokenHasher.Hash(newRefreshPlaintext),
                now,
                expiresAt,
                cancellationToken);
            refreshExpiresIn = policy.LifetimeMinutes * 60;
        }
        else
        {
            // Sin rotación: el cliente conserva su token; solo se renueva el acceso.
            refreshExpiresIn = (int)Math.Max(0, (snapshot.ExpiresAtUtc - now).TotalSeconds);
        }

        await _auditService.RecordAuthEventAsync(new AuthAuditEvent(
            UserId:     snapshot.UserId,
            TenantId:   snapshot.TenantId,
            TenantCode: tenant.Props.Code.GetValue(),
            AuthMethod: authMethod.Type.ToString(),
            EventType:  "Auth.Refresh.Success",
            Succeeded:  true,
            ClientIp:   command.ClientIp), cancellationToken);

        return Result<RefreshAuthenticationResult>.Success(new RefreshAuthenticationResult(
            Graph:            graph,
            SerializedGraph:  serialized,
            GraphFormat:      format,
            ExpiresIn:        graph.EffectiveConfig.AccessTokenDurationMs / 1000,
            IssuedAt:         graph.GeneratedAt,
            NewRefreshToken:  newRefreshPlaintext,
            RefreshExpiresIn: refreshExpiresIn,
            // G-295: la sesión del ingreso, para que el portador renovado siga siendo el mismo
            // dispositivo a ojos del logout.
            SessionId:        snapshot.SessionId));
    }

    private async Task AuditFailureAsync(
        Guid userId, Guid tenantId, string clientIp,
        string code, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await _auditService.RecordAuthEventAsync(new AuthAuditEvent(
                UserId:        userId,
                TenantId:      tenantId,
                TenantCode:    string.Empty,
                AuthMethod:    "Refresh",
                EventType:     "Auth.Refresh.Failure",
                Succeeded:     false,
                ClientIp:      clientIp,
                FailureReason: $"{code}: {reason}"), cancellationToken);
        }
        catch
        {
            // La auditoría no debe enmascarar el resultado de la renovación.
        }
    }
}
