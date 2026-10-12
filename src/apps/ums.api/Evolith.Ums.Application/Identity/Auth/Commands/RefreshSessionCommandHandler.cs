using Evolith.Ums.Application.Authorization.Graph;
using Evolith.Ums.Application.Identity.Auth;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity.Auth;

namespace Evolith.Ums.Application.Identity.Auth.Commands;

/// <summary>
/// Renovación de la sesión de portal por cookie (D-019 / ADR-UMS-091). Espeja el login:
///   1. Verifica que el inquilino y el usuario del principal sigan vigentes (activos).
///   2. Resuelve el método de autenticación de portal (local, ADR-0077).
///   3. <b>Regenera el grafo COMPLETO</b> desde el estado actual (IAuthorizationGraphBuilder),
///      cerrando la brecha de latencia de permisos del refresh deslizante.
///   4. Audita el evento de renovación (ADR-UMS-091, invariante de auditoría append-only).
///
/// No toca el flujo de refresh token OPACO (<see cref="RefreshAuthenticationCommandHandler"/>):
/// aquí la credencial es la cookie de sesión viva, no un token opaco, por lo que no hay store,
/// rotación ni detección de reuso — solo la regeneración del grafo que faltaba.
/// </summary>
public sealed class RefreshSessionCommandHandler
    : ICommandHandler<RefreshSessionCommand, RefreshSessionResult>
{
    private readonly ITenantRepository          _tenantRepo;
    private readonly IUserAccountRepository     _userRepo;
    private readonly IAuthMethodResolver        _methodResolver;
    private readonly IAuthorizationGraphBuilder _graphBuilder;
    private readonly IAuthGraphFormatProvider   _formatProvider;
    private readonly IAuthAuditService          _auditService;

    public RefreshSessionCommandHandler(
        ITenantRepository          tenantRepo,
        IUserAccountRepository     userRepo,
        IAuthMethodResolver        methodResolver,
        IAuthorizationGraphBuilder graphBuilder,
        IAuthGraphFormatProvider   formatProvider,
        IAuthAuditService          auditService)
    {
        _tenantRepo     = tenantRepo;
        _userRepo       = userRepo;
        _methodResolver = methodResolver;
        _graphBuilder   = graphBuilder;
        _formatProvider = formatProvider;
        _auditService   = auditService;
    }

    public async Task<Result<RefreshSessionResult>> Handle(
        RefreshSessionCommand command,
        CancellationToken     cancellationToken)
    {
        // ── El inquilino debe seguir activo ───────────────────────────────────────
        var tenant = await _tenantRepo.GetByIdAsync(command.TenantId, cancellationToken);
        if (tenant is null || tenant.Props.Status != Domain.Enums.TenantStatus.Active)
        {
            await AuditFailureAsync(command, "Tenant inactive or missing", cancellationToken);
            return Result<RefreshSessionResult>.Failure("AUTH_007: The tenant is no longer active.");
        }

        // ── El usuario debe seguir activo y pertenecer al inquilino ───────────────
        var user = await _userRepo.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null
            || user.Props.TenantId.GetValue() != command.TenantId
            || user.Props.Status != Domain.Enums.UserStatus.Active)
        {
            // Usuario bloqueado/suspendido/eliminado ⇒ no se renueva (corte en caliente).
            await AuditFailureAsync(command, "User inactive or missing", cancellationToken);
            return Result<RefreshSessionResult>.Failure("AUTH_007: The user account is no longer active.");
        }

        // ── Método de autenticación de portal (ADR-0077: siempre local) ───────────
        var methodResult = await _methodResolver.ResolveAsync(
            command.TenantId, AuthAccessScope.PortalManagement, cancellationToken: cancellationToken);
        if (methodResult.IsFailure)
        {
            await AuditFailureAsync(command, methodResult.Error, cancellationToken);
            return Result<RefreshSessionResult>.Failure(methodResult.Error);
        }

        // ── Regeneración COMPLETA del grafo (ADR-UMS-091, decisión 1) ────────────────
        // G-297: acotado por el sistema que el ingreso pidió, no por `null`. Aquí decía «sin acotar
        // por sistema, que el portal es multiproducto», y era verdad a medias: el portal es
        // multiproducto cuando NO pide sistema, y desde ADR-0156 §3.2 puede pedirlo —`LoginRequest`
        // lo admite y el portal lo envía—. Renovar con `null` deshacía esa petición en la primera
        // renovación automática, y el token volvía con el `sys_suite` del desempate.
        //
        // Cuando el ingreso no pidió sistema, `command.SystemCode` es null y esto se comporta
        // exactamente como antes.
        var graphResult = await _graphBuilder.BuildAsync(
            user, command.TenantId, methodResult.Value, command.SystemCode, cancellationToken);
        if (graphResult.IsFailure)
        {
            await AuditFailureAsync(command, graphResult.Error, cancellationToken);
            return Result<RefreshSessionResult>.Failure(graphResult.Error);
        }

        var graph  = graphResult.Value;
        var format = await _formatProvider.GetDefaultFormatAsync(command.TenantId, cancellationToken);

        await _auditService.RecordAuthEventAsync(new AuthAuditEvent(
            UserId:     command.UserId,
            TenantId:   command.TenantId,
            TenantCode: tenant.Props.Code.GetValue(),
            AuthMethod: methodResult.Value.Type.ToString(),
            EventType:  "Auth.Refresh.Success",
            Succeeded:  true,
            ClientIp:   command.ClientIp), cancellationToken);

        return Result<RefreshSessionResult>.Success(new RefreshSessionResult(
            Graph:       graph,
            GraphFormat: format,
            ExpiresIn:   graph.EffectiveConfig.AccessTokenDurationMs / 1000));
    }

    private async Task AuditFailureAsync(
        RefreshSessionCommand command, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await _auditService.RecordAuthEventAsync(new AuthAuditEvent(
                UserId:        command.UserId,
                TenantId:      command.TenantId,
                TenantCode:    string.Empty,
                AuthMethod:    "Refresh",
                EventType:     "Auth.Refresh.Failure",
                Succeeded:     false,
                ClientIp:      command.ClientIp,
                FailureReason: reason), cancellationToken);
        }
        catch
        {
            // La auditoría no debe enmascarar el resultado de la renovación.
        }
    }
}
