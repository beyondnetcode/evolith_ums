namespace Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

/// <summary>Id del nodo recién añadido al árbol del módulo, para evitar un GET posterior (G-053).</summary>
public sealed record AddNodeResponse(Guid NodeId);
