namespace Evolith.Ums.Domain.IGA;

using Evolith.Ums.Domain.IGA.RoleMaturityStatus;
using Evolith.Ums.Domain.IGA.RolePromotionRequest;
using RoleMaturityStatusAggregate = Evolith.Ums.Domain.IGA.RoleMaturityStatus.RoleMaturityStatus;
using RolePromotionRequestAggregate = Evolith.Ums.Domain.IGA.RolePromotionRequest.RolePromotionRequest;

/// <summary>
/// Repositorio del agregado de elegibilidad <see cref="RoleMaturityStatusAggregate"/> (IGA, ADR-UMS-093).
/// Todas las lecturas se acotan por inquilino: la fuente de verdad de elegibilidad no cruza fronteras
/// de inquilino. El identificador de usuario/rol resuelve el <c>RoleMaturityStatus</c> que consume
/// la confirmación de elegibilidad de una promoción.
/// </summary>
public interface IRoleMaturityStatusRepository : IAggregateRepository<RoleMaturityStatusAggregate>
{
    Task<RoleMaturityStatusAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Devuelve los estados de madurez del usuario dentro del inquilino (uno por rol asignado).</summary>
    Task<IReadOnlyList<RoleMaturityStatusAggregate>> GetByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Resuelve el estado de madurez del usuario en un rol concreto (fuente de elegibilidad al hacer Submit).</summary>
    Task<RoleMaturityStatusAggregate?> GetByUserAndRoleAsync(Guid tenantId, Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleMaturityStatusAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repositorio del agregado de promoción <see cref="RolePromotionRequestAggregate"/> (IGA, ADR-UMS-093).
/// Las consultas se acotan por inquilino y, opcionalmente, por estado de la máquina de estados.
/// </summary>
public interface IRolePromotionRequestRepository : IAggregateRepository<RolePromotionRequestAggregate>
{
    Task<RolePromotionRequestAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RolePromotionRequestAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RolePromotionRequestAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Solicitudes del inquilino filtradas por el nombre del estado (<see cref="RolePromotionStatus"/>).</summary>
    Task<IReadOnlyList<RolePromotionRequestAggregate>> GetByTenantAndStatusAsync(Guid tenantId, string status, CancellationToken cancellationToken = default);
}
