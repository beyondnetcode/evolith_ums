using Evolith.Ums.Application.Authorization.Template.DTOs;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Template;
using MenuNodeEntity = Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode.MenuNode;
using SystemSuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;

namespace Evolith.Ums.Application.Authorization.Template.Queries;

public sealed class GetPermissionTemplateByIdQueryHandler : IQueryHandler<GetPermissionTemplateByIdQuery, PermissionTemplateDetailDto>
{
    private readonly IPermissionTemplateRepository _templateRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ISystemSuiteRepository _systemSuiteRepository;

    public GetPermissionTemplateByIdQueryHandler(
        IPermissionTemplateRepository templateRepository,
        IRoleRepository roleRepository,
        ISystemSuiteRepository systemSuiteRepository)
    {
        _templateRepository = templateRepository;
        _roleRepository = roleRepository;
        _systemSuiteRepository = systemSuiteRepository;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_templates", ActionCode = "view")]
    public async Task<Result<PermissionTemplateDetailDto>> Handle(
        GetPermissionTemplateByIdQuery request,
        CancellationToken cancellationToken)
    {
        var template = await _templateRepository.GetByIdAsync(request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result<PermissionTemplateDetailDto>.Failure("Permission template not found.");
        }

        // Resolve role and suite names
        var role = await _roleRepository.GetByIdAsync(template.Props.RoleId.GetValue(), cancellationToken);
        var roleName = role?.Props.Value.GetValue() ?? "—";

        var suite = await _systemSuiteRepository.GetByIdAsync(template.Props.SystemSuiteId.GetValue(), cancellationToken);
        var suiteName = suite?.Props.Name.GetValue() ?? "—";

        // Build flat action lookup: actionId → actionName
        var actionLookup = suite is null
            ? new Dictionary<Guid, string>()
            : suite.Actions.ToDictionary(
                a => a.GetId().GetValue(),
                a => a.Name.GetValue());

        // Build flat target lookup: id → name (across all nesting levels)
        var targetLookup = BuildTargetNameLookup(suite);

        var items = template.Items
            .Select(i => new PermissionTemplateItemDto(
                i.Props.Id.GetValue(),
                i.TargetType.Name,
                i.TargetId.GetValue(),
                targetLookup.GetValueOrDefault(i.TargetId.GetValue(), "—"),
                i.ActionId.GetValue(),
                actionLookup.GetValueOrDefault(i.ActionId.GetValue(), "—"),
                i.IsAllowed,
                i.IsDenied,
                i.IsActive))
            .ToList();

        return Result<PermissionTemplateDetailDto>.Success(new PermissionTemplateDetailDto(
            template.Props.Id.GetValue(),
            template.Props.TenantId.GetValue(),
            template.Props.RoleId.GetValue(),
            roleName,
            template.Props.SystemSuiteId.GetValue(),
            suiteName,
            template.Props.Version.GetValue(),
            template.Props.Status.ToString(),
            items));
    }

    /// <summary>
    /// Walks the SystemSuite tree and builds a flat id→displayName lookup
    /// covering Modules, Menus, SubMenus, and Options.
    /// </summary>
    private static Dictionary<Guid, string> BuildTargetNameLookup(SystemSuiteAggregate? suite)
    {
        var lookup = new Dictionary<Guid, string>();
        if (suite is null) return lookup;

        void WalkNodes(IEnumerable<MenuNodeEntity> nodes)
        {
            foreach (var node in nodes)
            {
                lookup[node.GetId().GetValue()] = node.Label.GetValue();
                WalkNodes(node.Children);
            }
        }

        foreach (var module in suite.Modules)
        {
            lookup[module.Props.Id.GetValue()] = module.Name.GetValue();
            WalkNodes(module.Nodes);
        }

        return lookup;
    }
}
