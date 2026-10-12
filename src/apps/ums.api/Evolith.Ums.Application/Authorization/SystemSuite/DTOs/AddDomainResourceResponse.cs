namespace Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

/// <summary>Id del recurso de dominio recién creado, para evitar un GET posterior (G-053).</summary>
public sealed record AddDomainResourceResponse(Guid DomainResourceId);
