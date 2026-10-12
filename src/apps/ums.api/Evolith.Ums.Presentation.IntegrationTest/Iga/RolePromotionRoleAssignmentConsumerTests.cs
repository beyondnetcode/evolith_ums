using Evolith.Ums.Domain.Events;
using Evolith.Ums.Infrastructure.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ProfileAggregate = Evolith.Ums.Domain.Authorization.Profile.Profile;

namespace Evolith.Ums.Presentation.IntegrationTest.Iga;

/// <summary>
/// G-094: pruebas AISLADAS de <see cref="RolePromotionRoleAssignmentConsumer"/> —el consumidor que
/// aplica el efecto de la promoción de rol IGA entregado por el Transactional Outbox—. Se ejercita su
/// núcleo (<c>ApplyRoleAssignmentAsync</c>) contra un <see cref="InMemoryProfileRepository"/> real, sin
/// bus ni contenedor, para verificar de forma determinista:
///
///  1. Dado el evento, reasigna el rol del perfil ACTIVO del objetivo cuyo rol coincide con el origen.
///  2. Ante un <c>ChangeRole</c> fallido NO confirma éxito: lanza excepción para que MassTransit
///     reintente y, agotados los reintentos, mueva el mensaje a la dead-letter — el efecto de la
///     promoción no se descarta en silencio (fin del fallo silencioso G-093).
///
/// Complementa a <see cref="RolePromotionEffectE2ETests"/> (happy-path E2E por el bus real) cubriendo
/// la rama de fallo, que el harness in-memory no puede forzar de forma determinista.
/// </summary>
public sealed class RolePromotionRoleAssignmentConsumerTests
{
    // El campo del inquilino NO se llama «TenantId» para no ensombrecer el tipo homónimo del dominio.
    private static readonly Guid SeedTenantId = Guid.NewGuid();
    private static readonly Guid TargetUserId = Guid.NewGuid();
    private static readonly Guid CurrentRoleId = Guid.NewGuid();
    private static readonly Guid TargetRoleId = Guid.NewGuid();
    private static readonly Guid ExecutorId = Guid.NewGuid();

    private static RolePromotionRoleAssignmentConsumer CreateConsumer(InMemoryProfileRepository repository)
        => new(repository, NullLogger<RolePromotionRoleAssignmentConsumer>.Instance);

    private static ProfileAggregate SeedActiveProfile(InMemoryProfileRepository repository, Guid roleId)
    {
        var profile = ProfileAggregate.Create(
            TenantId.Load(SeedTenantId),
            UserId.Load(TargetUserId),
            RoleId.Load(roleId),
            branchId: null,
            ActorId.Create(ExecutorId.ToString())).Value;

        repository.Seed(profile);
        return profile;
    }

    [Fact]
    public async Task ApplyRoleAssignment_WhenActiveProfileMatchesSourceRole_ReassignsToTargetRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemoryProfileRepository();
        var seeded = SeedActiveProfile(repository, CurrentRoleId);
        var consumer = CreateConsumer(repository);

        var message = new RolePromotionExecutedIntegrationEvent(
            SeedTenantId, RequestId: Guid.NewGuid(), TargetUserId, CurrentRoleId, TargetRoleId, ExecutorId);

        await consumer.ApplyRoleAssignmentAsync(message, ct);

        var reloaded = await repository.GetByIdAsync(seeded.GetId().GetValue(), ct);
        reloaded.Should().NotBeNull();
        reloaded!.RoleId.GetValue().Should().Be(TargetRoleId, "el consumidor debe reasignar el perfil objetivo al rol destino (G-094)");
        reloaded.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyRoleAssignment_WhenNoMatchingProfile_DoesNothingAndDoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemoryProfileRepository();
        // Perfil activo con OTRO rol: la regla de selección (RoleId == CurrentRoleId) no lo alcanza.
        var seeded = SeedActiveProfile(repository, Guid.NewGuid());
        var consumer = CreateConsumer(repository);

        var message = new RolePromotionExecutedIntegrationEvent(
            SeedTenantId, RequestId: Guid.NewGuid(), TargetUserId, CurrentRoleId, TargetRoleId, ExecutorId);

        // No hay sujeto sobre el que actuar: se confirma el mensaje (reintentar no ayudaría) sin lanzar.
        await consumer.ApplyRoleAssignmentAsync(message, ct);

        var reloaded = await repository.GetByIdAsync(seeded.GetId().GetValue(), ct);
        reloaded!.RoleId.GetValue().Should().NotBe(TargetRoleId, "sin perfil coincidente no debe tocarse ningún rol");
    }

    [Fact]
    public async Task ApplyRoleAssignment_WhenChangeRoleFails_ThrowsAndDoesNotMarkSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemoryProfileRepository();
        var seeded = SeedActiveProfile(repository, CurrentRoleId);
        var consumer = CreateConsumer(repository);

        // Evento degenerado (rol destino == rol origen): el perfil ES seleccionado, pero ChangeRole
        // falla («rol sin cambio»). El consumidor debe LANZAR —no confirmar éxito— para que el mensaje
        // se reintente y termine en la dead-letter en lugar de descartarse en silencio (G-094).
        var message = new RolePromotionExecutedIntegrationEvent(
            SeedTenantId, RequestId: Guid.NewGuid(), TargetUserId, CurrentRoleId, TargetRoleId: CurrentRoleId, ExecutorId);

        var act = async () => await consumer.ApplyRoleAssignmentAsync(message, ct);

        await act.Should().ThrowAsync<InvalidOperationException>("un ChangeRole fallido no debe confirmarse como éxito (G-094)");

        var reloaded = await repository.GetByIdAsync(seeded.GetId().GetValue(), ct);
        reloaded!.RoleId.GetValue().Should().Be(CurrentRoleId, "un efecto fallido no debe dejar el perfil mutado a medias");
    }
}
