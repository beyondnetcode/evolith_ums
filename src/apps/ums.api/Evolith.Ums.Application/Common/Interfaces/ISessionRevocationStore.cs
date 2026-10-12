namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// Lista de sesiones cerradas, por SESIÓN y no por usuario (G-247).
///
/// <para>POR QUÉ EXISTE. Cerrar sesión no cerraba nada: <c>SignOutAsync</c> solo le pide al
/// navegador que borre la cookie, y el portador seguía siendo criptográficamente válido hasta
/// caducar. Medido en vivo: tras el logout, la misma cookie seguía devolviendo 200 — también contra
/// el mismo pod, así que no era un problema de réplicas. Quien tuviera una copia —una máquina
/// compartida, una captura de red— conservaba el acceso.</para>
///
/// <para>POR QUÉ NO SE REUSÓ <see cref="ITokenRevocationStore"/>. Ese almacén revoca por USUARIO y
/// ventana de tiempo: sirve para «esta cuenta queda fuera» (bloqueo, borrado, cambio de
/// contraseña), y usarlo en el logout cerraría también la sesión del móvil y la del portátil del
/// mismo usuario. La decisión de producto fue la contraria: cerrar sesión cierra <b>solo este
/// dispositivo</b>. Son dos preguntas distintas —«¿está vetada la cuenta?» y «¿sigue viva esta
/// sesión?»— y mezclarlas en una clave obligaría a elegir una de las dos semánticas.</para>
///
/// <para>La implementación va sobre <c>IDistributedCache</c>, que es Redis cuando está configurado:
/// una sesión cerrada en un pod queda cerrada en todos, que es lo que exige más de una réplica.</para>
/// </summary>
public interface ISessionRevocationStore
{
    /// <summary>
    /// Cierra la sesión indicada hasta <paramref name="cerrarHastaUtc"/>.
    ///
    /// <para>El plazo no es decorativo: una entrada eterna haría crecer la lista sin límite, y una
    /// demasiado corta reabriría la sesión antes de que el portador caduque. Se pasa el instante en
    /// que el portador deja de ser válido por sí mismo — a partir de ahí, recordarlo no aporta.</para>
    /// </summary>
    Task RevocarAsync(string sessionId, DateTime cerrarHastaUtc, CancellationToken ct = default);

    /// <summary>¿Está cerrada esta sesión? Se consulta en cada petición autenticada.</summary>
    Task<bool> EstaRevocadaAsync(string sessionId, CancellationToken ct = default);
}
