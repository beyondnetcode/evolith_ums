namespace Evolith.Ums.Domain.Identity.Tenant.Branch;

/// <summary>
/// Asiento de la BITÁCORA de una sucursal: un episodio con su fecha, su autor y la foto de cómo era
/// la sucursal en ese instante.
///
/// <para><b>Por qué existe.</b> El estado (<c>IsActive</c>, <c>IsClosed</c>) responde «cómo está la
/// sucursal HOY». La pregunta de auditoría de un operador logístico aduanero es otra: «¿cómo estaba
/// en marzo de 2024, cuando salió ESTE despacho?». Sin bitácora esa pregunta no tiene respuesta,
/// porque la fila de la sucursal solo guarda su último estado y su última marca de auditoría: dos
/// épocas distintas de la misma sucursal se ven idénticas.</para>
///
/// <para><b>Por qué guarda una foto y no solo el evento.</b> Reconstruir «qué había en marzo»
/// necesita el nombre y la geocerca que regían ENTONCES, no los de hoy. Guardarlos en el asiento
/// —al cerrar y al reabrir, que son las fronteras de cada época— hace la respuesta directa: se lee
/// el asiento anterior a la fecha preguntada. La alternativa, versionar la sucursal entera de forma
/// bitemporal, resuelve más casos a un coste desproporcionado para el problema que hay.</para>
///
/// <para><b>Es inmutable y solo crece.</b> No hay corrección ni borrado de asientos: una bitácora
/// que se puede reescribir no prueba nada. Es un <c>record</c> por eso mismo (ADR-0041: dominio
/// inmutable).</para>
/// </summary>
/// <param name="Id">Identidad del asiento.</param>
/// <param name="TenantId">Inquilino dueño de la sucursal; sostiene el aislamiento (filtro global y RLS).</param>
/// <param name="BranchId">Sucursal a la que pertenece el episodio.</param>
/// <param name="Episode">Qué ocurrió (apertura, desactivación, reactivación, cierre definitivo).</param>
/// <param name="OccurredAtUtc">Cuándo ocurrió, en UTC.</param>
/// <param name="ActorId">Quién lo hizo. Es el actor de la operación, no el usuario técnico.</param>
/// <param name="NameSnapshot">Cómo se llamaba la sucursal en ese instante.</param>
/// <param name="GeofencingSnapshot">Dónde estaba (geocerca) en ese instante; nulo si no tenía.</param>
/// <param name="Reason">Motivo declarado por quien operó, si lo dio.</param>
public sealed record BranchLifecycleEntry(
    Guid Id,
    Guid TenantId,
    Guid BranchId,
    BranchLifecycleEpisode Episode,
    DateTime OccurredAtUtc,
    string ActorId,
    string NameSnapshot,
    string? GeofencingSnapshot,
    string? Reason);
