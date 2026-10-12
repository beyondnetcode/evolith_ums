namespace Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

/// <summary>Identificador de la acción recién registrada (G-053). Code es la clave de negocio.</summary>
public sealed record RegisterActionResponse(Guid ActionId, string Code);
