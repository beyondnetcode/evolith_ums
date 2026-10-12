using MediatR;
using Microsoft.Extensions.Logging;
using Evolith.Ums.Domain.Events;

namespace Evolith.Ums.Infrastructure.Hosting.EventHandlers;

// G-066 / ADR-0098 D4/D7: la revocación de tokens es un handoff EN PROCESO (efecto intra-servicio,
// no cruza frontera de sistema). Se maneja como manejador de notificación MediatR despachado
// post-commit por UmsPlatformDbContext, no como consumidor del bróker inter-sistema (antes
// IConsumer<> sobre MassTransit, lo que exponía el evento de dominio crudo al transporte).

/// <summary>
/// HARDENING-03: revoca los tokens de inmediato cuando se elimina un usuario. La ventana de
/// revocación es de 24 h — muy por encima de cualquier TTL razonable de JWT. Cuando la entrada
/// expira, el usuario (eliminado) ya no puede tener token válido.
/// </summary>
public sealed class UserDeletedTokenRevocationHandler(
    ITokenRevocationStore revocationStore,
    ILogger<UserDeletedTokenRevocationHandler> logger)
    : INotificationHandler<UserDeletedEvent>
{
    private static readonly TimeSpan RevocationWindow = TimeSpan.FromHours(24);

    public async Task Handle(UserDeletedEvent notification, CancellationToken cancellationToken)
    {
        var userId    = notification.UserId.ToString();
        var revokeUntil = DateTime.UtcNow.Add(RevocationWindow);

        // G-202: SE REVOCAN LAS DOS CLAVES. El token de sesión web porta el id en
        // `NameIdentifier`, pero el token semántico de cliente evita identificadores internos a
        // propósito y su `sub` es el nombre de usuario. El middleware busca por lo que el token
        // traiga, así que revocar solo el id dejaba vivo el token del satélite hasta que caducara.
        await revocationStore.RevokeAsync(userId, revokeUntil, cancellationToken);
        if (!string.IsNullOrWhiteSpace(notification.Subject))
        {
            await revocationStore.RevokeAsync(notification.Subject!, revokeUntil, cancellationToken);
        }

        logger.LogInformation(
            "HARDENING-03 / G-202: Revoked tokens for deleted user {UserId} (subject {Subject}) until {RevokeUntil:O}.",
            userId, notification.Subject ?? "(sin sujeto)", revokeUntil);
    }
}

/// <summary>
/// HARDENING-03: revoca los tokens de inmediato cuando se bloquea un usuario. A diferencia de la
/// eliminación, el bloqueo puede ser temporal, así que la ventana es más corta (4 h). Si luego se
/// restaura, su próximo login exitoso emitirá un token nuevo fuera de la lista de revocación.
/// </summary>
public sealed class UserBlockedTokenRevocationHandler(
    ITokenRevocationStore revocationStore,
    ILogger<UserBlockedTokenRevocationHandler> logger)
    : INotificationHandler<UserBlockedEvent>
{
    private static readonly TimeSpan RevocationWindow = TimeSpan.FromHours(4);

    public async Task Handle(UserBlockedEvent notification, CancellationToken cancellationToken)
    {
        var userId    = notification.UserId.ToString();
        var revokeUntil = DateTime.UtcNow.Add(RevocationWindow);

        // G-202: las dos claves, por lo mismo que en la eliminación.
        await revocationStore.RevokeAsync(userId, revokeUntil, cancellationToken);
        if (!string.IsNullOrWhiteSpace(notification.Subject))
        {
            await revocationStore.RevokeAsync(notification.Subject!, revokeUntil, cancellationToken);
        }

        logger.LogInformation(
            "HARDENING-03: Revoked tokens for blocked user {UserId} until {RevokeUntil:O}. Reason: {Reason}",
            userId, revokeUntil, notification.Reason);
    }
}
