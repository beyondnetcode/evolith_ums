using System;

namespace Evolith.Ums.Infrastructure.Persistence.Iga.Entities;

/// <summary>
/// Registro de persistencia del agregado de promoción <c>RolePromotionRequest</c> (IGA, ADR-UMS-093, FR-060/061).
///
/// <para>Tabla acotada por inquilino (<see cref="TenantId"/>). El estado de la máquina se guarda como
/// <see cref="StatusId"/> (valor de la enumeración de dominio <c>RolePromotionStatus</c>) y el
/// <see cref="RiskScore"/> congelado se guarda como entero (nulo en <c>Draft</c>). Los identificadores
/// de aprobador/revisor/ejecutor/verificador se registran a medida que avanza la máquina para poder
/// auditar la segregación de funciones. Los eventos de transición fluyen al Outbox (ADR-0052) al
/// guardar; no se persisten como filas.</para>
/// </summary>
public sealed class RolePromotionRequestRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid TargetUserId { get; set; }
    public Guid RequesterId { get; set; }
    public Guid CurrentRoleId { get; set; }
    public Guid TargetRoleId { get; set; }

    /// <summary>Identificador de la enumeración de dominio <c>RolePromotionStatus</c> (Draft=1 … Cancelled=9).</summary>
    public int StatusId { get; set; }

    /// <summary>RiskScore [0,100] congelado al salir de <c>Draft</c> (nulo mientras es <c>Draft</c>).</summary>
    public int? RiskScore { get; set; }

    public Guid? ApproverId { get; set; }
    public Guid? SecurityReviewerId { get; set; }
    public Guid? ExecutorId { get; set; }
    public Guid? VerifierId { get; set; }
    public string? DecisionReason { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
}
