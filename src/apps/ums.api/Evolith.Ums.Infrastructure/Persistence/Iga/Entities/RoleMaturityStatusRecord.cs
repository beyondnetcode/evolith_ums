using System;

namespace Evolith.Ums.Infrastructure.Persistence.Iga.Entities;

/// <summary>
/// Registro de persistencia del agregado de elegibilidad <c>RoleMaturityStatus</c> (IGA, ADR-UMS-093, FR-062).
///
/// <para>Tabla acotada por inquilino (<see cref="TenantId"/>): la fuente de verdad de elegibilidad
/// nunca cruza fronteras de inquilino. Los niveles de madurez (<see cref="CurrentMaturityLevel"/> /
/// <see cref="NextEligibleMaturityLevel"/>) se guardan como el valor entero del enum
/// <c>RoleMaturityLevel</c>; los eventos de dominio no se persisten como filas — fluyen al Outbox
/// (ADR-0052) al guardar.</para>
/// </summary>
public sealed class RoleMaturityStatusRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }

    /// <summary>Valor entero del enum <c>RoleMaturityLevel</c> (Junior=1 … Principal=5).</summary>
    public int CurrentMaturityLevel { get; set; }

    /// <summary>Siguiente nivel elegible (nulo mientras no se confirme la elegibilidad).</summary>
    public int? NextEligibleMaturityLevel { get; set; }

    public DateTime AssignedAt { get; set; }
    public DateTime CurrentLevelSince { get; set; }
    public DateTime? EligibleForPromotionAt { get; set; }
    public int CompletedCertificationsCount { get; set; }
    public int CompletedTrainingsCount { get; set; }
    public decimal PerformanceScore { get; set; }
    public bool HasNoComplianceIssues { get; set; }

    /// <summary>Factor de bloqueo de cumplimiento activo (nulo si no hay incidencia).</summary>
    public string? BlockingFactor { get; set; }

    public DateTime? LastReviewedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
}
