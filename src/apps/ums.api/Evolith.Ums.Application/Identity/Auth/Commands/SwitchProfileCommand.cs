namespace Evolith.Ums.Application.Identity.Auth.Commands;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Auth;

/// <summary>
/// Cambia el perfil vigente de una sesión ya autenticada.
///
/// Un perfil ata un usuario, un rol y —por el rol— un sistema. Un usuario con varios perfiles
/// tenía que cerrar sesión y volver a entrar para cambiarse de sombrero, y ni siquiera sabía que
/// tenía otros: el grafo elegía uno y descartaba el resto sin dejar rastro (G-177).
///
/// La identidad NO cambia: no se piden credenciales otra vez. Lo que cambia es el conjunto de
/// permisos con el que se opera, y por eso el resultado es un grafo nuevo completo, no un parche
/// sobre el anterior.
/// </summary>
public sealed record SwitchProfileCommand(
    Guid   ProfileId,
    Guid   UserId,
    Guid   TenantId,
    string ClientIp,
    // ADR-0156 §2.5 — acota el bloque `profiles` del grafo resultante al sistema del llamante.
    // No elige el perfil vigente, que llega en `ProfileId`. Lo envía el carril de satélite,
    // tomándolo del claim `sys_suite` de su propio portador; el portal lo deja en null, porque
    // es multiproducto por definición.
    string? SystemCode = null) : ICommand<AuthenticateUserResult>;

public sealed class SwitchProfileCommandHandler : ICommandHandler<SwitchProfileCommand, AuthenticateUserResult>
{
    private readonly IUserAccountRepository      _userRepo;
    private readonly IProfileRepository          _profileRepo;
    private readonly IAuthorizationGraphBuilder  _graphBuilder;
    private readonly IAuthAuditService           _auditService;

    public SwitchProfileCommandHandler(
        IUserAccountRepository     userRepo,
        IProfileRepository         profileRepo,
        IAuthorizationGraphBuilder graphBuilder,
        IAuthAuditService          auditService)
    {
        _userRepo     = userRepo;
        _profileRepo  = profileRepo;
        _graphBuilder = graphBuilder;
        _auditService = auditService;
    }

    public async Task<Result<AuthenticateUserResult>> Handle(
        SwitchProfileCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepo.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null || user.Props.Status != Domain.Enums.UserStatus.Active)
        {
            return Result<AuthenticateUserResult>.Failure(
                "AUTH_005: The account is not active.");
        }

        var profile = await _profileRepo.GetByIdAsync(request.ProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<AuthenticateUserResult>.Failure("AUTH_020: Profile not found.");
        }

        // El identificador del perfil lo envía el CLIENTE: nunca se confía en él. Sin estas dos
        // comprobaciones, este endpoint sería una escalada de privilegios de una línea — bastaría
        // enviar el id del perfil de un administrador.
        if (profile.Props.UserId.GetValue() != request.UserId ||
            profile.Props.TenantId.GetValue() != request.TenantId)
        {
            await _auditService.RecordAuthEventAsync(new AuthAuditEvent(
                UserId:      request.UserId,
                TenantId:    request.TenantId,
                TenantCode:  string.Empty,
                AuthMethod:  "Local",
                EventType:   "Auth.Profile.SwitchDenied",
                Succeeded:   false,
                ClientIp:    request.ClientIp,
                IdpProvider: null), cancellationToken);

            // Mismo mensaje que «no existe»: distinguirlos permitiría a un atacante enumerar
            // perfiles ajenos preguntando por identificadores.
            return Result<AuthenticateUserResult>.Failure("AUTH_020: Profile not found.");
        }

        if (!profile.IsActive)
        {
            return Result<AuthenticateUserResult>.Failure("AUTH_021: Profile is inactive.");
        }

        var graphResult = await _graphBuilder.BuildForProfileAsync(
            user, request.TenantId, request.ProfileId, AuthMethod.Local(),
            request.SystemCode, cancellationToken);

        if (graphResult.IsFailure)
        {
            return Result<AuthenticateUserResult>.Failure(graphResult.Error);
        }

        await _auditService.RecordAuthEventAsync(new AuthAuditEvent(
            UserId:      request.UserId,
            TenantId:    request.TenantId,
            TenantCode:  graphResult.Value.Context.Tenant.Code,
            AuthMethod:  "Local",
            EventType:   "Auth.Profile.Switch",
            Succeeded:   true,
            ClientIp:    request.ClientIp,
            IdpProvider: null), cancellationToken);

        return Result<AuthenticateUserResult>.Success(new AuthenticateUserResult(
            Graph:           graphResult.Value,
            Token:           string.Empty,   // lo emite la capa de presentación, como en el login
            TokenType:       "Bearer",
            ExpiresIn:       graphResult.Value.EffectiveConfig.AccessTokenDurationMs / 1000,
            IssuedAt:        graphResult.Value.GeneratedAt,
            SerializedGraph: string.Empty,
            GraphFormat:     "JSON"));
    }
}
