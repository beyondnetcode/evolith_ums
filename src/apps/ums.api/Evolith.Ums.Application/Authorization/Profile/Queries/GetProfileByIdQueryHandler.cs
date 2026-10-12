using Evolith.Ums.Application.Authorization.Profile.DTOs;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Profile;
using Evolith.Ums.Domain.Authorization.Profile.ProfilePermission;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Authorization.Template.PermissionTemplateItem;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserAccount;
using MenuNodeEntity = Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode.MenuNode;
using SystemSuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;

namespace Evolith.Ums.Application.Authorization.Profile.Queries;

public sealed class GetProfileByIdQueryHandler : IQueryHandler<GetProfileByIdQuery, ProfileDto>
{
    private readonly IProfileRepository _profileRepository;
    private readonly IPermissionTemplateRepository _templateRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ISystemSuiteRepository _systemSuiteRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserAccountRepository _userAccountRepository;

    public GetProfileByIdQueryHandler(
        IProfileRepository profileRepository,
        IPermissionTemplateRepository templateRepository,
        IRoleRepository roleRepository,
        ISystemSuiteRepository systemSuiteRepository,
        ITenantRepository tenantRepository,
        IUserAccountRepository userAccountRepository)
    {
        _profileRepository = profileRepository;
        _templateRepository = templateRepository;
        _roleRepository = roleRepository;
        _systemSuiteRepository = systemSuiteRepository;
        _tenantRepository = tenantRepository;
        _userAccountRepository = userAccountRepository;
    }

    [AuthorizationAspect(ResourceCode = "view_profiles", ActionCode = "view")]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<ProfileDto>> Handle(
        GetProfileByIdQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await _profileRepository.GetByIdAsync(request.ProfileId, cancellationToken);

        if (profile is null)
        {
            return Result<ProfileDto>.Failure("Profile not found.");
        }

        var role = await _roleRepository.GetByIdAsync(profile.Props.RoleId.GetValue(), cancellationToken);
        var suite = role is null ? null : await _systemSuiteRepository.GetByIdAsync(role.Props.SystemSuiteId.GetValue(), cancellationToken);
        var tenant = await _tenantRepository.GetByIdAsync(profile.Props.TenantId.GetValue(), cancellationToken);
        var user = await _userAccountRepository.GetByIdAsync(profile.Props.UserId.GetValue(), cancellationToken);

        var actionLookup = suite is null
            ? new Dictionary<Guid, string>()
            : suite.Actions.ToDictionary(
                a => a.GetId().GetValue(),
                a => a.Name.GetValue());

        var targetLookup = BuildTargetNameLookup(suite);

        var templateIds = profile.Permissions
            .Select(p => p.Props.TemplateId.GetValue())
            .Distinct()
            .ToList();

        var templates = new Dictionary<Guid, PermissionTemplate>();
        foreach (var templateId in templateIds)
        {
            var template = await _templateRepository.GetByIdAsync(templateId, cancellationToken);
            if (template is not null)
            {
                templates[templateId] = template;
            }
        }

        TemplateItemOriginalDto? FindOriginalItem(ProfilePermission p)
        {
            if (!templates.TryGetValue(p.Props.TemplateId.GetValue(), out var template))
                return null;

            var item = template.Items.FirstOrDefault(i =>
                i.TargetType.Name == p.TargetType.Name &&
                i.TargetId.GetValue() == p.TargetId.GetValue() &&
                i.ActionId.GetValue() == p.ActionId.GetValue());

            if (item is null)
                return null;

            return new TemplateItemOriginalDto(
                item.GetId().GetValue(),
                item.TargetType.Name,
                item.TargetId.GetValue(),
                targetLookup.GetValueOrDefault(item.TargetId.GetValue(), "—"),
                item.ActionId.GetValue(),
                actionLookup.GetValueOrDefault(item.ActionId.GetValue(), "—"),
                item.IsAllowed,
                item.IsDenied,
                item.IsActive);
        }

        var permissions = profile.Permissions
            .Select(p => new ProfilePermissionDto(
                p.Props.Id.GetValue(),
                p.Props.ProfileId.GetValue(),
                p.Props.TemplateId.GetValue(),
                p.TargetType.Name,
                p.TargetId.GetValue(),
                targetLookup.GetValueOrDefault(p.TargetId.GetValue(), "—"),
                p.ActionId.GetValue(),
                actionLookup.GetValueOrDefault(p.ActionId.GetValue(), "—"),
                p.IsAllowed,
                p.IsDenied,
                p.IsActive,
                p.IsOverride,
                FindOriginalItem(p)))
            .ToList();

        return Result<ProfileDto>.Success(new ProfileDto(
            profile.Props.Id.GetValue(),
            profile.Props.TenantId.GetValue(),
            tenant?.Props.Code.GetValue() ?? "—",
            tenant?.Props.Name.GetValue() ?? "—",
            profile.Props.UserId.GetValue(),
            user?.Props.Email.GetValue() ?? "—",
            profile.Props.RoleId.GetValue(),
            role?.Props.Code.GetValue() ?? "—",
            role?.Props.Value.GetValue() ?? "—",
            role?.Props.SystemSuiteId.GetValue() ?? Guid.Empty,
            suite?.Props.Code.GetValue() ?? "—",
            suite?.Props.Name.GetValue() ?? "—",
            profile.Props.BranchId?.GetValue(),
            null,
            profile.Props.Scope.ToString(),
            profile.Props.IsActive,
            // Cuenta lo que CONCEDE, no lo que hay (ADR-UMS-106). Desligar desactiva en vez de
            // borrar, asi que sumar las filas inactivas mostraria al operador el mismo numero
            // inflado que [[G-272]] venia a corregir —94 a 188— cuando ya no concede ni uno.
            // La lista sigue completa, con su `isActive`, para que la pantalla pueda mostrarlas.
            permissions.Count(p => p.IsActive),
            permissions));
    }

    private static Dictionary<Guid, string> BuildTargetNameLookup(SystemSuiteAggregate? suite)
    {
        var lookup = new Dictionary<Guid, string>();
        if (suite is null) return lookup;

        lookup[suite.GetId().GetValue()] = suite.Props.Name.GetValue();

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

        foreach (var resource in suite.DomainResources)
        {
            lookup[resource.Id.GetValue()] = resource.Name.GetValue();

            // Note: If you have CRUD operations stored with specific IDs, they would need to be added here too.
            // But usually DomainResources are just the root Entity/Aggregate.
        }

        return lookup;
    }
}
