using System;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Evolith.Ums.Infrastructure.Persistence.Audit.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Interceptors;

/// <summary>
/// G-081 — Anti-tamper de la traza de auditoría a NIVEL DE PERSISTENCIA (no repudio).
///
/// <para>
/// La traza de auditoría —<see cref="AuditRecordRecord"/>, tabla <c>audit."AuditRecords"</c>— es
/// APPEND-ONLY: el dominio no expone métodos de mutación (INV-AU1) y el repositorio
/// (<c>PostgreSqlAuditRecordRepository</c>) solo ofrece <c>AppendAsync</c>/<c>GetByIdAsync</c>/
/// <c>QueryBy*</c>. Faltaba, sin embargo, el enforcement en la capa de persistencia: nada impedía
/// que un <c>SaveChanges</c> mutara o borrara una traza ya escrita. Este interceptor cierra esa
/// brecha: intercepta el pipeline de <c>SaveChanges</c> del <see cref="UmsPlatformDbContext"/> y
/// RECHAZA (lanza) cualquier entrada de <see cref="AuditRecordRecord"/> en estado
/// <see cref="EntityState.Modified"/> o <see cref="EntityState.Deleted"/>. Solo se permite
/// <see cref="EntityState.Added"/> (INSERT).
/// </para>
///
/// <para>
/// Defensa app-layer y portable (independiente del motor y del rol de BD). A diferencia de un
/// <c>REVOKE UPDATE, DELETE</c> sobre la tabla de auditoría —que un rol <b>owner/superuser</b>
/// ignora, misma causa que G-020: la app se conecta como <c>postgres</c>—, este guard actúa
/// siempre que la escritura pase por el <see cref="UmsPlatformDbContext"/>.
/// </para>
///
/// <para>
/// NO altera el estampado de <see cref="IAuditableRecord"/> que realiza
/// <see cref="AuditSaveChangesInterceptor"/>: aquel opera sobre la interfaz en TODAS las entidades
/// (y permite <c>Modified</c> para refrescar <c>UpdatedBy</c>/<c>UpdatedAtUtc</c>); éste opera solo
/// sobre la entidad concreta de la traza de auditoría y prohíbe su mutación/borrado. Ambos conviven
/// en el mismo pipeline: si se intenta modificar una traza, este guard aborta el <c>SaveChanges</c>
/// completo antes de tocar la base de datos.
/// </para>
/// </summary>
public sealed class AuditAppendOnlyGuardInterceptor : SaveChangesInterceptor
{
    // Mensaje de no-repudio; el marcador {0} se rellena con el estado rechazado (Modified/Deleted).
    internal const string TamperMessageFormat =
        "No repudio (G-081): la traza de auditoría (AuditRecordRecord) es append-only a nivel de " +
        "persistencia. Se rechazó una operación de {0} sobre una traza de auditoría; solo se permite " +
        "INSERT (Added). UPDATE/DELETE de auditoría está denegado.";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            EnforceAppendOnly(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            EnforceAppendOnly(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Recorre las entradas de la traza de auditoría (<see cref="AuditRecordRecord"/>) en el
    /// <see cref="Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker"/> y lanza
    /// <see cref="InvalidOperationException"/> si alguna intenta MODIFICAR o BORRAR una traza ya
    /// escrita. Se tipa contra la entidad concreta (no contra <see cref="IAuditableRecord"/>) para
    /// no interferir con el estampado general de auditoría.
    /// </summary>
    public static void EnforceAppendOnly(DbContext context)
    {
        var tampered = context.ChangeTracker
            .Entries<AuditRecordRecord>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (tampered is not null)
        {
            throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, TamperMessageFormat, tampered.State));
        }
    }
}
