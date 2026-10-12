namespace Evolith.Ums.Infrastructure.Persistence.Seeders;

using Microsoft.Extensions.DependencyInjection;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Infrastructure.Persistence;
using RoleMaturityStatusAggregate = Evolith.Ums.Domain.IGA.RoleMaturityStatus.RoleMaturityStatus;

/// <summary>
/// Datos de desarrollo del contexto acotado IGA (ADR-UMS-093, G-052). Siembra estados de madurez
/// (<see cref="RoleMaturityStatusAggregate"/>) para el inquilino RANSA, de modo que el happy-path de
/// promoción se pueda ejercer de punta a punta: la confirmación de elegibilidad es <em>fail-closed</em>
/// y, sin un <c>RoleMaturityStatus</c> sembrado, siempre RECHAZA (INV-RPR4). Aquí se siembra:
///
///   • un estado ELEGIBLE para <c>RansaAdminUserId</c> en su rol actual (<c>DemoAdminRoleId</c>):
///     nivel Junior, ingreso al nivel &gt; 2 años atrás (supera el mínimo de 6 meses del salto Junior),
///     desempeño 4.5 (≥ 3.0) y sin incidencias de cumplimiento ⇒ <c>EvaluateEligibility</c> aprueba;
///
///   • un estado NO ELEGIBLE (borde) para <c>RansaAnalystUserId</c> en <c>DemoOperatorRoleId</c>:
///     ingreso reciente (10 días) ⇒ no supera el tiempo mínimo en nivel ⇒ RECHAZA, ejercitando el
///     corte fail-closed sin depender de la ausencia del dato.
///
/// Respeta el acotamiento por inquilino (todo se siembra bajo <c>RansaTenantId</c>) y es idempotente
/// bajo PostgreSQL (comprueba existencia por usuario+rol antes de insertar). Sigue el patrón de
/// <see cref="ApprovalsDevDataSeeder"/>: usa la variante en memoria cuando está registrada (dev/tests)
/// y la variante PostgreSQL en el entorno desplegado.
/// </summary>
public static class IgaDevDataSeeder
{
    // Nivel de madurez inicial del estado ELEGIBLE (Junior exige 6 meses en nivel, INV-RMS3).
    private const decimal EligiblePerformanceScore = 4.5m;
    private const decimal BorderlinePerformanceScore = 3.5m;

    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var maturityRepository = serviceProvider.GetService<IRoleMaturityStatusRepository>();
        var inMemoryMaturityRepository = serviceProvider.GetService<InMemoryRoleMaturityStatusRepository>();

        var actor = ActorId.Create(CoreDevDataSeeder.SystemActorId);
        var tenantId = TenantId.Load(Guid.Parse(CoreDevDataSeeder.RansaTenantId));
        var nowUtc = DateTime.UtcNow;

        // Estado ELEGIBLE: objetivo = gerente de operaciones RANSA en su rol actual ADMIN.
        var eligible = BuildMaturityStatus(
            tenantId,
            userId: UserId.Load(Guid.Parse(CoreDevDataSeeder.RansaAdminUserId)),
            roleId: RoleId.Load(Guid.Parse(CoreDevDataSeeder.DemoAdminRoleId)),
            level: RoleMaturityLevel.Junior,
            assignedAtUtc: nowUtc.AddYears(-2), // supera con holgura los 6 meses mínimos del salto Junior
            performanceScore: EligiblePerformanceScore,
            actor);

        // Estado NO ELEGIBLE (borde): objetivo = analista de inventario RANSA en rol OPERATOR,
        // recién ingresado al nivel ⇒ no supera el tiempo mínimo ⇒ elegibilidad rechazada (fail-closed).
        var borderline = BuildMaturityStatus(
            tenantId,
            userId: UserId.Load(Guid.Parse(CoreDevDataSeeder.RansaAnalystUserId)),
            roleId: RoleId.Load(Guid.Parse(CoreDevDataSeeder.DemoOperatorRoleId)),
            level: RoleMaturityLevel.Junior,
            assignedAtUtc: nowUtc.AddDays(-10), // insuficiente tiempo en nivel
            performanceScore: BorderlinePerformanceScore,
            actor);

        var seedItems = new[] { eligible, borderline }.Where(x => x is not null).Select(x => x!).ToList();
        if (seedItems.Count == 0)
        {
            return;
        }

        if (inMemoryMaturityRepository is not null)
        {
            foreach (var item in seedItems)
            {
                inMemoryMaturityRepository.Seed(item);
            }

            return;
        }

        if (maturityRepository is null)
        {
            return;
        }

        // PostgreSQL: idempotente por usuario+rol (no recrea si ya existe).
        var added = false;
        foreach (var item in seedItems)
        {
            var existing = await maturityRepository.GetByUserAndRoleAsync(
                item.TenantId.GetValue(),
                item.UserId.GetValue(),
                item.RoleId.GetValue(),
                cancellationToken);

            if (existing is null)
            {
                await maturityRepository.AddAsync(item, cancellationToken);
                added = true;
            }
        }

        if (added)
        {
            await maturityRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        }
    }

    private static RoleMaturityStatusAggregate? BuildMaturityStatus(
        TenantId tenantId,
        UserId userId,
        RoleId roleId,
        RoleMaturityLevel level,
        DateTime assignedAtUtc,
        decimal performanceScore,
        ActorId actor)
    {
        var result = RoleMaturityStatusAggregate.Create(tenantId, userId, roleId, level, assignedAtUtc, actor);
        if (result.IsFailure)
        {
            return null;
        }

        var status = result.Value;
        status.UpdatePerformanceScore(performanceScore, actor);
        return status;
    }
}
