namespace Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

/// <summary>Id del módulo recién creado, para evitar un GET posterior (G-053).</summary>
public sealed record AddModuleResponse(Guid ModuleId);
