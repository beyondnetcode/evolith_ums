namespace Evolith.Ums.Domain.Authorization.Profile;

using Evolith.Ums.Domain.Authorization.Profile.ProfilePermission;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Events;
using EffectConst = Evolith.Ums.Domain.Authorization.Profile.PermissionEffectConstants;
using PermissionTemplateEntity = Evolith.Ums.Domain.Authorization.Template.PermissionTemplate;
using ProfilePermissionEntity = Evolith.Ums.Domain.Authorization.Profile.ProfilePermission.ProfilePermission;

public sealed class Profile : AggregateRoot<Profile, ProfileProps>
{
    private readonly List<ProfilePermissionEntity> _permissions = new();

    public new ProfileDomainEventsManager DomainEvents { get; }

    private Profile(ProfileProps props) : base(props)
    {
        DomainEvents = new ProfileDomainEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new ProfileCreatedEvent(
                Props.Id.GetValue(),
                Props.TenantId.GetValue(),
                Props.UserId.GetValue(),
                Props.RoleId.GetValue(),
                Props.BranchId?.GetValue()));
        }
    }

    public TenantId TenantId => Props.TenantId;
    public UserId UserId => Props.UserId;
    public RoleId RoleId => Props.RoleId;
    public BranchId? BranchId => Props.BranchId;
    public ProfileScope Scope => Props.Scope;
    public bool IsActive => Props.IsActive;

    public IReadOnlyCollection<ProfilePermissionEntity> Permissions => _permissions.AsReadOnly();

    public ProfileId GetId() => ProfileId.Load(Props.Id.GetValue());

    public static Result<Profile> Create(
        TenantId tenantId,
        UserId userId,
        RoleId roleId,
        BranchId? branchId,
        ActorId createdBy)
    {
        var scope = branchId is null ? ProfileScope.OrgWide : ProfileScope.BranchScoped;
        var props = new ProfileProps(IdValueObject.Create(), tenantId, userId, roleId, branchId, scope, createdBy);
        var profile = new Profile(props);

        if (!profile.IsValid())
        {
            return Result<Profile>.Failure(profile.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<Profile>.Success(profile);
    }

    public Result AssignTemplate(PermissionTemplateEntity template, ActorId assignedBy)
    {
        if (!IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Authorization.ProfileAlreadyInactive));
        }

        // G-043 (causa raíz): comparar por VALOR (Guid subyacente). El operador != de los
        // value objects de identidad (IdValueObject) resuelve por REFERENCIA, de modo que dos
        // instancias con el mismo Guid se consideraban distintas y esta guarda marcaba SIEMPRE
        // template_tenant_mismatch: AssignTemplate nunca materializaba (permissionCount=0) y el
        // fallo se enmascaraba. El resto del dominio ya compara identidades vía GetValue().
        if (template.TenantId.GetValue() != TenantId.GetValue())
        {
            BrokenRules.Add(new BrokenRule(nameof(Template), DomainErrors.Authorization.TemplateTenantMismatch));
        }

        if (template.Status != TemplateStatus.Published)
        {
            BrokenRules.Add(new BrokenRule(nameof(Template), DomainErrors.Authorization.TemplateNotPublishedForProfile));
        }

        var templateId = TemplateId.Load(template.GetId().GetValue());

        // Solo lo VIVO cuenta como enlazado (ADR-UMS-106 D5). Desligar DESACTIVA en vez de borrar,
        // asi que sin este `IsActive` una plantilla desligada seguiria bloqueando su reasignacion —y
        // reasignar es justamente el flujo con el que se corrige una plantilla mal publicada.
        if (_permissions.Any(p => p.TemplateId.GetValue() == templateId.GetValue() && p.IsActive))
        {
            BrokenRules.Add(new BrokenRule(nameof(Permissions), DomainErrors.Authorization.ProfileTemplateAlreadyLinked));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        // Se materializa desde ActiveItems, NO desde Items.
        //
        // Es la mitad que de verdad importa del borrado lógico. Mientras retirar un ítem lo borraba
        // de la colección, iterar `Items` ya excluía lo retirado por construcción. Al dejar de
        // borrar, `Items` pasa a incluir también las concesiones retiradas: seguir iterándola las
        // copiaría al perfil como permisos ACTIVOS y el grafo resuelto concedería por ellas. Habríamos
        // cambiado un borrado por una brecha — y silenciosa, porque el operador ve el ítem apagado
        // en la plantilla mientras el usuario ejerce el permiso.
        foreach (var templateItem in template.ActiveItems)
        {
            var targetId = IdValueObject.Load(templateItem.TargetId.GetValue());
            var actionId = ActionId.Load(templateItem.ActionId.GetValue());
            var settings = ProfilePermissionSettings.Create(
                templateItem.TargetType,
                targetId,
                actionId,
                templateItem.IsAllowed,
                templateItem.IsDenied);

            var permissionResult = ProfilePermissionEntity.Create(
                GetId(),
                templateId,
                settings,
                assignedBy);

            if (permissionResult.IsFailure)
            {
                return Result.Failure(permissionResult.Error);
            }

            _permissions.Add(permissionResult.Value);
        }

        DomainEvents.RaiseEvent(new TemplateLinkedToProfileEvent(Props.Id.GetValue(), templateId.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(assignedBy.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// Retira de este perfil lo que una plantilla concedia (ADR-UMS-106 D4).
    ///
    /// <para>DESACTIVA en vez de borrar: el grafo ya ignora los permisos inactivos, asi que la
    /// concesion desaparece del grafo resuelto mientras el rastro de que existio permanece. Es el
    /// mismo criterio que el borrado logico fijo para los items de plantilla.</para>
    ///
    /// <para>Existe porque no existia: sin desligar, un perfil no se podia devolver a una sola
    /// version de su plantilla salvo desactivandolo y creando otro, con el identificador cambiado y
    /// las referencias rotas ([[G-272]]).</para>
    /// </summary>
    public Result UnassignTemplate(TemplateId templateId, ActorId unassignedBy)
    {
        if (!IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Authorization.ProfileAlreadyInactive));
        }

        var vivos = _permissions
            .Where(p => p.TemplateId.GetValue() == templateId.GetValue() && p.IsActive)
            .ToList();

        if (vivos.Count == 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(Permissions), DomainErrors.Authorization.ProfileTemplateNotLinked));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        foreach (var permiso in vivos)
        {
            var resultado = permiso.Deactivate(unassignedBy);
            if (resultado.IsFailure)
            {
                return resultado;
            }
        }

        DomainEvents.RaiseEvent(new TemplateUnlinkedFromProfileEvent(Props.Id.GetValue(), templateId.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(unassignedBy.GetValue());
        return Result.Success();
    }

    public Result OverridePermissionAllow(IdValueObject permissionId, ActorId updatedBy)
    {
        return ExecuteOnPermission(permissionId, p => p.OverrideAllow(updatedBy), updatedBy, EffectConst.Allow);
    }

    public Result OverridePermissionDeny(IdValueObject permissionId, ActorId updatedBy)
    {
        return ExecuteOnPermission(permissionId, p => p.OverrideDeny(updatedBy), updatedBy, EffectConst.Deny);
    }

    public Result OverridePermissionNeutral(IdValueObject permissionId, ActorId updatedBy)
    {
        return ExecuteOnPermission(permissionId, p => p.OverrideNeutral(updatedBy), updatedBy, EffectConst.Neutral);
    }

    public Result DeactivatePermission(IdValueObject permissionId, ActorId updatedBy)
    {
        return ExecuteOnPermission(permissionId, p => p.Deactivate(updatedBy), updatedBy, EffectConst.Deactivated);
    }

    public Result ActivatePermission(IdValueObject permissionId, ActorId updatedBy)
    {
        return ExecuteOnPermission(permissionId, p => p.Activate(updatedBy), updatedBy, EffectConst.Activated);
    }

    private Result ExecuteOnPermission(IdValueObject permissionId, Func<ProfilePermissionEntity, Result> action, ActorId updatedBy, string eventType)
    {
        if (!IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Authorization.ProfileAlreadyInactive));
        }

        var permission = FindPermission(permissionId);
        if (permission.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Permissions), DomainErrors.Authorization.PermissionNotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var result = action(permission.Value);
        if (result.IsFailure)
        {
            return result;
        }

        DomainEvents.RaiseEvent(new PermissionOverriddenEvent(
            Props.Id.GetValue(),
            permission.Value.GetId().GetValue(),
            eventType));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Deactivate(ActorId updatedBy)
    {
        if (!IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Authorization.ProfileAlreadyInactive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        foreach (var permission in _permissions.Where(p => p.IsActive))
        {
            permission.Deactivate(updatedBy);
        }

        SetProps(Props.WithIsActive(false));
        DomainEvents.RaiseEvent(new ProfileDeactivatedEvent(Props.Id.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// ADR-UMS-096: reasigna el rol del perfil. Es el efecto real de una promoción de rol IGA
    /// (<c>RolePromotionRequest.Execute</c>), aplicado como paso diferido post-commit en su propia
    /// transacción (D-016: un agregado por transacción). Invariantes: el perfil debe estar activo y
    /// el rol destino debe diferir del actual. Devuelve <see cref="Result"/> (nunca excepción).
    /// </summary>
    public Result ChangeRole(RoleId newRoleId, ActorId updatedBy)
    {
        if (!IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Authorization.ProfileAlreadyInactive));
        }

        if (newRoleId.GetValue() == Props.RoleId.GetValue())
        {
            BrokenRules.Add(new BrokenRule(nameof(RoleId), DomainErrors.Authorization.ProfileRoleUnchanged));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var previousRoleId = Props.RoleId.GetValue();
        SetProps(Props.WithRoleId(newRoleId));
        DomainEvents.RaiseEvent(new ProfileRoleChangedEvent(
            Props.Id.GetValue(),
            Props.TenantId.GetValue(),
            Props.UserId.GetValue(),
            previousRoleId,
            newRoleId.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Activate(ActorId updatedBy)
    {
        if (IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Authorization.ProfileAlreadyActive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithIsActive(true));
        DomainEvents.RaiseEvent(new ProfileActivatedEvent(Props.Id.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    private Result<ProfilePermissionEntity> FindPermission(IdValueObject permissionId)
    {
        // AT06/F1: la identidad canónica de un permiso es Props.Id (id persistido, lo que expone el DTO
        // y reenvía el cliente). El Id base de Entity<> se regenera ALEATORIO en cada construcción
        // (Entity ctor: Id = IdValueObject.Create()) y la rehidratación vía Construct<> NO llama SetId,
        // así que buscar por p.Id nunca casaba tras recargar → override/activate/deactivate daban 404.
        var permission = _permissions.FirstOrDefault(p => p.Props.Id.GetValue() == permissionId.GetValue());
        return permission is null
            ? Result<ProfilePermissionEntity>.Failure(DomainErrors.Common.NotFound)
            : Result<ProfilePermissionEntity>.Success(permission);
    }
}
