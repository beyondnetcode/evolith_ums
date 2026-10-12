using Evolith.Ums.Application.Authorization.Template.DTOs;

namespace Evolith.Ums.Application.Authorization.Template.Queries;

public sealed record GetPermissionTemplateByIdQuery(Guid TemplateId) : IQuery<PermissionTemplateDetailDto>;
