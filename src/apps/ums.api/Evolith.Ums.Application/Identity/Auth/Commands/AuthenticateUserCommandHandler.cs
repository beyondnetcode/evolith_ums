using Evolith.Ums.Application.Authorization.Graph;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Auth;

namespace Evolith.Ums.Application.Identity.Auth.Commands;

/// <summary>
/// Orchestrates the complete authentication flow:
///   1. Resolve tenant by TenantCode
///   2. Validate user status
///   3. IAuthMethodResolver → Local or IDP
///   4. Execute Local (BCrypt) or IDP auth strategy
///   5. IAuthorizationGraphBuilder.BuildAsync → full graph
///   6. Serialize graph in tenant's configured format (Shell.Factory)
///   7. Record audit event (IAuditService)
/// </summary>
public sealed class AuthenticateUserCommandHandler
    : ICommandHandler<AuthenticateUserCommand, AuthenticateUserResult>
{
    private readonly ITenantRepository          _tenantRepo;
    private readonly IUserAccountRepository     _userRepo;
    private readonly IAuthMethodResolver        _methodResolver;
    private readonly ILocalAuthStrategy         _localStrategy;
    private readonly IIdpChainAuthenticator     _chainAuthenticator;
    private readonly IAuthorizationGraphBuilder _graphBuilder;
    private readonly IAuthorizationGraphSerializer _defaultSerializer;
    private readonly IAuthAuditService          _auditService;
    private readonly IConfigurationProvider     _configProvider;

    public AuthenticateUserCommandHandler(
        ITenantRepository          tenantRepo,
        IUserAccountRepository     userRepo,
        IAuthMethodResolver        methodResolver,
        ILocalAuthStrategy         localStrategy,
        IIdpChainAuthenticator     chainAuthenticator,
        IAuthorizationGraphBuilder graphBuilder,
        IAuthorizationGraphSerializer defaultSerializer,
        IAuthAuditService          auditService,
        IConfigurationProvider     configProvider)
    {
        _tenantRepo         = tenantRepo;
        _userRepo           = userRepo;
        _methodResolver     = methodResolver;
        _localStrategy      = localStrategy;
        _chainAuthenticator = chainAuthenticator;
        _graphBuilder       = graphBuilder;
        _defaultSerializer  = defaultSerializer;
        _auditService       = auditService;
        _configProvider     = configProvider;
    }

    public async Task<Result<AuthenticateUserResult>> Handle(
        AuthenticateUserCommand command,
        CancellationToken       cancellationToken)
    {
        var tenantId = Guid.Empty;
        var userId   = Guid.Empty;

        try
        {
            // ── 1. Tenant ──────────────────────────────────────────────────────
            var tenant = await _tenantRepo.GetByCodeAsync(
                command.TenantCode.ToUpperInvariant(), cancellationToken);

            if (tenant is null)
            {
                await RecordFailureAsync(tenantId, userId, command, "AUTH_002: Tenant not found",
                    "Unknown", cancellationToken);
                return Result<AuthenticateUserResult>.Failure("AUTH_002: Tenant not found.");
            }

            if (tenant.Props.Status != Domain.Enums.TenantStatus.Active)
            {
                await RecordFailureAsync(tenantId, userId, command, "AUTH_003: Tenant inactive",
                    "Unknown", cancellationToken);
                return Result<AuthenticateUserResult>.Failure("AUTH_003: Tenant is not active.");
            }

            tenantId = tenant.Props.Id.GetValue();

            // ── 2. Auth method resolution ──────────────────────────────────────
            // FR-042 (ADR-UMS-097 §2.2): se enhebra el contexto de resolución (suite del comando +
            // dominio de email derivado del Username) hacia el motor de reglas del resolver.
            var methodResult = await _methodResolver.ResolveAsync(
                tenantId,
                command.AccessScope,
                command.SystemSuiteId,
                ExtractEmailDomain(command.Username),
                cancellationToken);
            if (methodResult.IsFailure)
            {
                await RecordFailureAsync(tenantId, userId, command, methodResult.Error,
                    "Unknown", cancellationToken);
                return Result<AuthenticateUserResult>.Failure(methodResult.Error);
            }

            var authMethod = methodResult.Value;
            var methodName = authMethod.Type.ToString();

            // ── 3. Authenticate ────────────────────────────────────────────────
            if (authMethod.Type == AuthMethodType.Local)
            {
                return await AuthenticateLocalAsync(
                    command, tenant, tenantId, authMethod, methodName, cancellationToken);
            }
            else
            {
                return await AuthenticateIdpAsync(
                    command, tenant, tenantId, methodName, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await RecordFailureAsync(tenantId, userId, command,
                $"Unexpected error: {ex.Message}", "Unknown", cancellationToken);
            return Result<AuthenticateUserResult>.Failure(
                "An unexpected error occurred during authentication.");
        }
    }

    // ── Local Auth Flow ───────────────────────────────────────────────────────

    private async Task<Result<AuthenticateUserResult>> AuthenticateLocalAsync(
        AuthenticateUserCommand command,
        Evolith.Ums.Domain.Identity.Tenant.Tenant tenant,
        Guid              tenantId,
        AuthMethod        authMethod,
        string            methodName,
        CancellationToken cancellationToken)
    {
        // G-168: la búsqueda se acota al inquilino. `GetByEmailAsync` consulta sin filtro de
        // inquilino y sin `ORDER BY`: con el mismo correo dado de alta en dos inquilinos —la
        // unicidad es (TenantId, Email), así que es un estado válido— devolvía una fila
        // arbitraria, y qué fila ganaba dependía del plan de ejecución. El usuario legítimo
        // podía quedarse fuera de su propio inquilino sin explicación posible en el log.
        var user = await _userRepo.GetByTenantAndEmailAsync(
            tenantId, Email.Create(command.Username), cancellationToken: cancellationToken);

        if (user is null)
        {
            await RecordFailureAsync(tenantId, Guid.Empty, command,
                "AUTH_006: Invalid credentials", methodName, cancellationToken);
            return Result<AuthenticateUserResult>.Failure("AUTH_006: Invalid username or password.");
        }

        if (user.Props.Status != Domain.Enums.UserStatus.Active)
        {
            await RecordFailureAsync(tenantId, user.Props.Id.GetValue(), command,
                "AUTH_005: User not active", methodName, cancellationToken);
            return Result<AuthenticateUserResult>.Failure(
                "AUTH_005: User account is not active. Contact your administrator.");
        }

        // ADR-UMS-095: política de bloqueo temporal. El instante se resuelve aquí (aplicación) y se
        // inyecta en el dominio, que es determinista. La misma fuente de «ahora» rige la comprobación
        // y el registro del intento. maxAttempts/lockoutMinutes salen de la config efectiva (cascada
        // Global>Suite>Tenant>Module). El bloqueo administrativo permanente ya se rechazó arriba (AUTH_005).
        var cfg = _configProvider.ForTenant(tenantId);
        var now = DateTimeOffset.UtcNow;
        var maxLoginAttempts = cfg.MaxLoginAttempts;
        var lockoutMinutes   = cfg.AccountLockoutDurationMinutes;

        // ADR-UMS-095: enforcement ANTES de validar credenciales y sin revelar su validez.
        // AUTH_017 = bloqueo temporal de cuenta (AUTH_012 ya está tomado: «no IDP adapter registered»).
        if (user.IsLockedOut(now))
        {
            await RecordFailureAsync(tenantId, user.Props.Id.GetValue(), command,
                "AUTH_017: Account temporarily locked", methodName, cancellationToken);
            return Result<AuthenticateUserResult>.Failure(
                "AUTH_017: Account temporarily locked due to failed login attempts.");
        }

        var authResult = _localStrategy.Authenticate(user, command.Password);
        if (authResult.IsFailure)
        {
            user.RecordAuthenticationAttempt(false, now, maxLoginAttempts, lockoutMinutes,
                authResult.Error, command.ClientIp, ActorId.Create("auth:system"));
            await _userRepo.UpdateAsync(user, cancellationToken);
            await _userRepo.UnitOfWork.SaveEntitiesAsync(cancellationToken);

            await RecordFailureAsync(tenantId, user.Props.Id.GetValue(), command,
                "AUTH_006: Invalid credentials", methodName, cancellationToken);
            return Result<AuthenticateUserResult>.Failure("AUTH_006: Invalid username or password.");
        }

        var mfaCheck = CheckMfaPolicy(user, tenantId);
        if (mfaCheck.IsFailure)
        {
            await RecordFailureAsync(tenantId, user.Props.Id.GetValue(), command,
                mfaCheck.Error, methodName, cancellationToken);
            return Result<AuthenticateUserResult>.Failure(mfaCheck.Error);
        }

        user.RecordAuthenticationAttempt(true, now, maxLoginAttempts, lockoutMinutes,
            "Login successful", command.ClientIp, ActorId.Create("auth:system"));
        await _userRepo.UpdateAsync(user, cancellationToken);
        await _userRepo.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return await BuildResultAsync(
            user, tenantId, tenant.Props.Code.GetValue(),
            authMethod, command, methodName, cancellationToken);
    }

    // ── IDP Auth Flow ─────────────────────────────────────────────────────────

    private async Task<Result<AuthenticateUserResult>> AuthenticateIdpAsync(
        AuthenticateUserCommand command,
        Evolith.Ums.Domain.Identity.Tenant.Tenant tenant,
        Guid              tenantId,
        string            methodName,
        CancellationToken cancellationToken)
    {
        // FR-042 (ADR-UMS-097 §2.3/§2.4, slice 2b): la autenticación federada recorre la cadena de
        // fallback FallbackToId partiendo de la config ganadora del selector (2a). El avance de la
        // cadena SOLO ocurre por indisponibilidad de infraestructura; un fallo de credenciales es
        // TERMINAL (anti credential-spraying). El orquestador clasifica fail-closed y audita por intento.
        var chainResult = await _chainAuthenticator.AuthenticateAsync(
            tenant, command.Password, command.SystemSuiteId,
            ExtractEmailDomain(command.Username), command.ClientIp, cancellationToken);

        if (chainResult.IsFailure)
        {
            await RecordFailureAsync(tenantId, Guid.Empty, command,
                chainResult.Error, methodName, cancellationToken);
            return Result<AuthenticateUserResult>.Failure(chainResult.Error);
        }

        var externalId = chainResult.Value.Identity;
        // El proveedor que EFECTIVAMENTE autenticó puede ser uno de respaldo (distinto al primario):
        // el grafo y la auditoría de éxito se construyen con ese proveedor, no con el ganador inicial.
        var resolvedMethod = AuthMethod.Idp(chainResult.Value.Provider);

        // G-168: acotado al inquilino, igual que la rama local. Aquí el defecto era peor: no
        // existía NINGUNA comprobación posterior de `TenantId`, de modo que una cuenta de otro
        // inquilino que compartiera el correo devuelto por el IdP pasaba a construir el grafo
        // con el inquilino solicitado. Eso no es una denegación mal puesta: es un cruce de
        // frontera de inquilino en la emisión de permisos.
        var user = await _userRepo.GetByTenantAndEmailAsync(
            tenantId, Email.Create(externalId.Email), cancellationToken: cancellationToken);

        if (user is null)
        {
            await RecordFailureAsync(tenantId, Guid.Empty, command,
                "AUTH_004: IDP user has no UMS account", methodName, cancellationToken);
            return Result<AuthenticateUserResult>.Failure(
                "AUTH_004: Authenticated IDP user has no UMS account. Contact your administrator.");
        }

        if (user.Props.Status != Domain.Enums.UserStatus.Active)
        {
            await RecordFailureAsync(tenantId, user.Props.Id.GetValue(), command,
                "AUTH_005: User not active", methodName, cancellationToken);
            return Result<AuthenticateUserResult>.Failure(
                "AUTH_005: User account is not active. Contact your administrator.");
        }

        var mfaCheck = CheckMfaPolicy(user, tenantId);
        if (mfaCheck.IsFailure)
        {
            await RecordFailureAsync(tenantId, user.Props.Id.GetValue(), command,
                mfaCheck.Error, methodName, cancellationToken);
            return Result<AuthenticateUserResult>.Failure(mfaCheck.Error);
        }

        // ADR-UMS-095: en el flujo IDP el bloqueo por contraseña no aplica; el registro exitoso
        // resetea cualquier contador previo. maxAttempts/lockoutMinutes salen de la config efectiva.
        var idpCfg = _configProvider.ForTenant(tenantId);
        user.RecordAuthenticationAttempt(true, DateTimeOffset.UtcNow, idpCfg.MaxLoginAttempts, idpCfg.AccountLockoutDurationMinutes,
            "IDP login successful", command.ClientIp, ActorId.Create("auth:system"));
        await _userRepo.UpdateAsync(user, cancellationToken);
        await _userRepo.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return await BuildResultAsync(
            user, tenantId, tenant.Props.Code.GetValue(),
            resolvedMethod, command, methodName, cancellationToken);
    }

    // ── Shared Result Builder ─────────────────────────────────────────────────

    private async Task<Result<AuthenticateUserResult>> BuildResultAsync(
        Evolith.Ums.Domain.Identity.UserAccount.UserAccount user,
        Guid              tenantId,
        string            tenantCode,
        AuthMethod        authMethod,
        AuthenticateUserCommand command,
        string            methodName,
        CancellationToken cancellationToken)
    {
        // ADR-0156 §4: el código de sistema del comando acota el grafo a los perfiles del usuario
        // en ese sistema. Ausente —el caso del portal multiproducto— no acota nada.
        var graphResult = await _graphBuilder.BuildAsync(
            user, tenantId, authMethod, command.SystemCode, cancellationToken);
        if (graphResult.IsFailure)
        {
            await RecordFailureAsync(tenantId, user.Props.Id.GetValue(), command,
                graphResult.Error, methodName, cancellationToken);
            return Result<AuthenticateUserResult>.Failure(graphResult.Error);
        }

        var graph = graphResult.Value;

        // G-176: se declara el formato que REALMENTE se produjo, no el preferido del inquilino.
        // Antes se anunciaba el formato por defecto del inquilino mientras se serializaba
        // siempre con el serializador inyectado (JSON): un inquilino con XML por defecto recibía
        // la cabecera `X-Graph-Format: XML` sobre un cuerpo JSON, que es peor que no soportar XML.
        // El endpoint de cliente sigue re-serializando cuando el llamante pide otro formato por
        // `?format` o `Accept`. Honrar automáticamente la preferencia del inquilino exige
        // resolver el serializador por fábrica en este punto: queda anotado en G-176.
        var serialized = _defaultSerializer.Serialize(graph);
        // Defensivo: un serializador sin extensión declarada no debe tumbar un login.
        var format = string.IsNullOrWhiteSpace(_defaultSerializer.FileExtension)
            ? "JSON"
            : _defaultSerializer.FileExtension.ToUpperInvariant();

        await _auditService.RecordAuthEventAsync(new AuthAuditEvent(
            UserId:      user.Props.Id.GetValue(),
            TenantId:    tenantId,
            TenantCode:  tenantCode,
            AuthMethod:  methodName,
            EventType:   "Auth.Login.Success",
            Succeeded:   true,
            ClientIp:    command.ClientIp,
            IdpProvider: authMethod.Provider?.Props.Name.GetValue()), cancellationToken);

        return Result<AuthenticateUserResult>.Success(new AuthenticateUserResult(
            Graph:           graph,
            Token:           string.Empty,
            TokenType:       "Bearer",
            ExpiresIn:       graph.EffectiveConfig.AccessTokenDurationMs / 1000,
            IssuedAt:        graph.GeneratedAt,
            SerializedGraph: serialized,
            GraphFormat:     format));
    }

    // ── MFA Policy Check ──────────────────────────────────────────────────────

    private Result CheckMfaPolicy(
        Evolith.Ums.Domain.Identity.UserAccount.UserAccount user,
        Guid tenantId)
    {
        var cfg = _configProvider.ForTenant(tenantId);
        if (!cfg.MfaRequiredForAdmin)
        {
            return Result.Success();
        }

        if (!user.HasVerifiedMfaEnrollment(cfg.MfaAllowedMethods))
        {
            return Result.Failure("AUTH_011: MFA enrollment required. Please enroll and verify an MFA method before logging in.");
        }

        return Result.Success();
    }

    // FR-042 (ADR-UMS-097 §2.2): el dominio de email para el routing por dominio sale del identificador
    // de login cuando es un email; si no lo es, se devuelve null y el motor omite el filtro por dominio.
    private static string? ExtractEmailDomain(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var atIndex = username.IndexOf('@');
        return atIndex >= 0 && atIndex < username.Length - 1
            ? username[(atIndex + 1)..]
            : null;
    }

    private async Task RecordFailureAsync(
        Guid              tenantId,
        Guid              userId,
        AuthenticateUserCommand command,
        string            reason,
        string            methodName,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditService.RecordAuthEventAsync(new AuthAuditEvent(
                UserId:        userId,
                TenantId:      tenantId,
                TenantCode:    command.TenantCode,
                AuthMethod:    methodName,
                EventType:     "Auth.Login.Failure",
                Succeeded:     false,
                ClientIp:      command.ClientIp,
                FailureReason: reason), cancellationToken);
        }
        catch
        {
            // Audit errors must not surface as authentication errors
        }
    }
}
