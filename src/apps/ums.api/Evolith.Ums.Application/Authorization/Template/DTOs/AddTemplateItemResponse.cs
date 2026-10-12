namespace Evolith.Ums.Application.Authorization.Template.DTOs;

/// <summary>Id del ítem de plantilla recién creado, para evitar un GET posterior (G-053).</summary>
public sealed record AddTemplateItemResponse(Guid ItemId);
