using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.Branch.Commands;

/// <summary>
/// Cierre DEFINITIVO de una sucursal (ADR-0164 §2.1). Sustituye a <c>RemoveBranchCommand</c>, que
/// borraba la fila: el nombre cambia con la semántica porque dejar vivo el vocabulario del borrado
/// físico es la vía más rápida a que alguien lo reintroduzca.
/// </summary>
/// <param name="Reason">
/// Motivo declarado del cierre. Va a la bitácora, no al estado: dentro de dos años la pregunta no
/// será si la sucursal está cerrada —eso se ve— sino por qué se cerró y quién lo decidió.
/// </param>
public sealed record CloseBranchCommand(
    Guid TenantId,
    Guid BranchId,
    string? Reason = null) : ICommand<CloseBranchResponse>;
