namespace Evolith.Ums.Domain.Authorization.Template;

using Evolith.Ums.Domain.Authorization.Template.PermissionTemplateItem;
using Evolith.Ums.Domain.Events;
using PermissionTemplateItemEntity = Evolith.Ums.Domain.Authorization.Template.PermissionTemplateItem.PermissionTemplateItem;

public sealed class PermissionTemplate : AggregateRoot<PermissionTemplate, PermissionTemplateProps>
{
    private readonly List<PermissionTemplateItemEntity> _items = new();

    public new PermissionTemplateDomainEventsManager DomainEvents { get; }

    private PermissionTemplate(PermissionTemplateProps props) : base(props)
    {
        DomainEvents = new PermissionTemplateDomainEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new PermissionTemplateCreatedEvent(
                Props.Id.GetValue(),
                Props.TenantId.GetValue(),
                Props.RoleId.GetValue(),
                Props.SystemSuiteId.GetValue(),
                Props.Version.GetValue()));
        }
    }

    public TenantId TenantId => Props.TenantId;
    public RoleId RoleId => Props.RoleId;
    public SystemSuiteId SystemSuiteId => Props.SystemSuiteId;
    public new TemplateVersion Version => Props.Version;
    public TemplateStatus Status => Props.Status;

    /// <summary>Verdadero cuando la plantilla está lógicamente eliminada; las lecturas deben ocultarla.</summary>
    public bool IsDeleted => Props.Status == TemplateStatus.Deleted;

    /// <summary>
    /// TODOS los ítems de la plantilla, vigentes y retirados.
    ///
    /// La colección NUNCA encoge: el único modo de sacar una concesión de la plantilla es retirarla
    /// (<see cref="DeactivateItem"/>), que la marca sin quitarla. Quien necesite «lo que la plantilla
    /// concede» debe usar <see cref="ActiveItems"/>; esta propiedad es la vista completa, que es lo
    /// que la auditoría necesita y lo que la persistencia guarda.
    /// </summary>
    public IReadOnlyCollection<PermissionTemplateItemEntity> Items => _items.AsReadOnly();

    /// <summary>
    /// Ítems VIGENTES: las concesiones que la plantilla otorga de verdad.
    ///
    /// Existe porque la diferencia entre esta lista y <see cref="Items"/> es la diferencia entre
    /// «qué concede» y «qué se decidió alguna vez». Confundirlas es exactamente lo que convertiría
    /// una retirada en una brecha: <c>Profile.AssignTemplate</c> materializa desde AQUÍ.
    /// </summary>
    public IReadOnlyCollection<PermissionTemplateItemEntity> ActiveItems
        => _items.Where(i => i.IsActive).ToList().AsReadOnly();

    public TemplateId GetId() => TemplateId.Load(Props.Id.GetValue());

    public static Result<PermissionTemplate> Create(
        TenantId tenantId,
        RoleId roleId,
        SystemSuiteId systemSuiteId,
        ActorId createdBy)
        => CreateWithVersion(tenantId, roleId, systemSuiteId, TemplateVersion.Initial(), createdBy);

    /// <summary>
    /// Crea una plantilla asignando la versión SIGUIENTE a la máxima existente para la terna
    /// (tenant, rol, suite). Si no existe ninguna, usa la inicial (0.1.0). G-140 / ADR-UMS-140:
    /// antes <see cref="Create"/> asignaba siempre 0.1.0, colisionando con el índice único
    /// IX_PermissionTemplates_TenantId_RoleId_SystemSuiteId_Version sobre roles ya plantillados.
    /// </summary>
    public static Result<PermissionTemplate> CreateNextVersion(
        TenantId tenantId,
        RoleId roleId,
        SystemSuiteId systemSuiteId,
        ActorId createdBy,
        IEnumerable<TemplateVersion> existingVersions)
    {
        var latest = existingVersions?
            .Where(v => v is not null)
            .OrderByDescending(v => v)
            .FirstOrDefault();

        var version = latest is null ? TemplateVersion.Initial() : latest.Next();

        return CreateWithVersion(tenantId, roleId, systemSuiteId, version, createdBy);
    }

    private static Result<PermissionTemplate> CreateWithVersion(
        TenantId tenantId,
        RoleId roleId,
        SystemSuiteId systemSuiteId,
        TemplateVersion version,
        ActorId createdBy)
    {
        var props = new PermissionTemplateProps(
            IdValueObject.Create(),
            tenantId,
            roleId,
            systemSuiteId,
            version,
            TemplateStatus.Draft,
            createdBy);

        var template = new PermissionTemplate(props);

        if (!template.IsValid())
        {
            return Result<PermissionTemplate>.Failure(template.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<PermissionTemplate>.Success(template);
    }

    public Result Publish(ActorId updatedBy)
    {
        if (Status != TemplateStatus.Draft)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Authorization.TemplateNotDraft));
        }

        // Se exige al menos un ítem VIGENTE, no al menos una fila.
        //
        // Mientras retirar un ítem lo borraba de la lista, `Items.Any()` bastaba: una plantilla sin
        // concesiones era una plantilla sin filas. Ahora que la retirada solo marca, la fila
        // sobrevive y `Items.Any()` seguiría siendo cierto para una plantilla que no concede nada.
        // Publicarla dejaría un contrato vacío asignable a perfiles — el mismo agujero que esta
        // guarda existe para tapar, entrando por la puerta que abre el borrado lógico.
        if (!ActiveItems.Any())
        {
            BrokenRules.Add(new BrokenRule(nameof(Items), DomainErrors.Authorization.TemplateItemsRequired));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Status = TemplateStatus.Published;
        DomainEvents.RaiseEvent(new PermissionTemplatePublishedEvent(Props.Id.GetValue(), Props.Version.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Deprecate(ActorId updatedBy)
    {
        if (Status != TemplateStatus.Published)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Authorization.TemplateNotPublished));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Status = TemplateStatus.Deprecated;
        DomainEvents.RaiseEvent(new PermissionTemplateDeprecatedEvent(Props.Id.GetValue(), Props.Version.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// Borrado LÓGICO: transiciona la plantilla al estado terminal <see cref="TemplateStatus.Deleted"/>.
    /// La fila —y sus ítems— permanecen en la base; solo dejan de ser visibles para las lecturas.
    /// Es el mismo patrón que <c>UserAccount.Delete</c> (REC-16).
    ///
    /// POR QUÉ no se borra físicamente: el negocio consulta datos antiguos (auditoría de quién tuvo
    /// qué permiso y cuándo). Un DELETE real dejaría los ítems huérfanos por cascada y perdería para
    /// siempre la trazabilidad de las concesiones que esa plantilla otorgó.
    ///
    /// Reglas de cascada (análogas a las de una base de datos):
    /// 1. Solo se elimina desde Draft o Deprecated. Una plantilla Published está EN USO: primero hay
    ///    que deprecarla, que es la retirada explícita de la oferta.
    /// 2. <paramref name="activeProfileCount"/> — perfiles VIVOS que la referencian. Un perfil ya
    ///    desactivado no bloquea: su referencia también está lógicamente eliminada. El conteo lo
    ///    resuelve el handler contra <c>IProfileRepository.CountActiveByTemplateAsync</c>, porque
    ///    cruza agregados y el dominio no consulta la base.
    /// </summary>
    public Result Delete(ActorId deletedBy, int activeProfileCount = 0)
    {
        if (Status == TemplateStatus.Deleted)
        {
            // Estado terminal: reintentar el borrado no es idempotente, es un conflicto. En la práctica
            // el handler ni llega aquí porque las lecturas ocultan lo eliminado (404); esta guarda es
            // la red que impide resucitar el agregado por una vía interna.
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Authorization.TemplateAlreadyDeleted));
        }
        else if (Status != TemplateStatus.Draft && Status != TemplateStatus.Deprecated)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Authorization.TemplateNotDeletable));
        }

        if (activeProfileCount > 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(Items), DomainErrors.Authorization.TemplateHasActiveProfiles));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Status = TemplateStatus.Deleted;
        DomainEvents.RaiseEvent(new PermissionTemplateDeletedEvent(Props.Id.GetValue(), Props.Version.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(deletedBy.GetValue());
        return Result.Success();
    }

    public Result<Guid> AddItem(ExclusiveArcTarget targetType, IdValueObject targetId, ActionId actionId, bool isAllowed, bool isDenied, ActorId createdBy)
    {
        if (Status != TemplateStatus.Draft)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Authorization.TemplateNotDraft));
        }

        // La clave natural del ítem —(plantilla, tipo de destino, destino, acción)— NO se libera al
        // retirarlo (ADR-0164 §2.3), así que la guarda mira TODAS las filas, vigentes y retiradas.
        // Distinguir los dos casos no es cosmético: «ya existe» sobre un ítem que el operador no ve
        // en la lista de vigentes es un mensaje que no puede accionar; nombrar la retirada le dice
        // que el verbo es reactivar.
        //
        // Se compara por VALOR (GetValue()), no con ==. IdValueObject no sobrecarga los operadores
        // de igualdad, así que == resuelve por REFERENCIA: dos instancias con el mismo Guid se
        // consideraban distintas y esta guarda NUNCA detectaba el duplicado sobre una plantilla
        // rehidratada desde la base —el alta caía al índice único y salía como 23505 → 409 opaco—.
        // Es la misma clase de bug que G-043 documenta en Profile.AssignTemplate.
        var existente = _items.FirstOrDefault(i =>
            i.TargetType.Id == targetType.Id
            && i.TargetId.GetValue() == targetId.GetValue()
            && i.ActionId.GetValue() == actionId.GetValue());

        if (existente is not null)
        {
            BrokenRules.Add(new BrokenRule(
                nameof(Items),
                existente.IsActive
                    ? DomainErrors.Authorization.TemplateItemTargetAlreadyExists
                    : DomainErrors.Authorization.TemplateItemTargetRetired));
        }

        if (!IsValid())
        {
            return Result<Guid>.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var itemResult = PermissionTemplateItemEntity.Create(GetId(), targetType, targetId, actionId, isAllowed, isDenied, createdBy);
        if (itemResult.IsFailure)
        {
            return Result<Guid>.Failure(itemResult.Error);
        }

        _items.Add(itemResult.Value);
        TrackingState.MarkAsDirty();
        DomainEvents.RaiseEvent(new PermissionTemplateMutatedEvent(Props.Id.GetValue(), Props.Version.GetValue()));
        Props.Audit.Update(createdBy.GetValue());
        return Result<Guid>.Success(itemResult.Value.GetId().GetValue());
    }

    public Result SetItemAllow(IdValueObject itemId, ActorId updatedBy)
    {
        return ExecuteOnItem(itemId, item => item.SetAllow(updatedBy), updatedBy);
    }

    public Result SetItemDeny(IdValueObject itemId, ActorId updatedBy)
    {
        return ExecuteOnItem(itemId, item => item.SetDeny(updatedBy), updatedBy);
    }

    public Result SetItemNeutral(IdValueObject itemId, ActorId updatedBy)
    {
        return ExecuteOnItem(itemId, item => item.SetNeutral(updatedBy), updatedBy);
    }

    /// <summary>
    /// Devuelve al servicio un ítem retirado. Es el reverso exacto de <see cref="DeactivateItem"/>.
    /// </summary>
    public Result ActivateItem(IdValueObject itemId, ActorId updatedBy)
    {
        return ExecuteOnItem(itemId, item => item.Activate(updatedBy), updatedBy);
    }

    /// <summary>
    /// RETIRA una concesión de la plantilla. Es la única forma de sacar un ítem: la fila permanece.
    ///
    /// POR QUÉ no hay un borrado, ni físico ni con estado terminal propio (ADR-0164):
    ///
    /// 1. El físico desaparecía de verdad. <c>_items.Remove(...)</c> hacía que el reconciliador de
    ///    colecciones hijas tradujera la ausencia en un DELETE, y con él se iba la prueba de que
    ///    alguien concedió esa acción sobre ese recurso. Es justo lo que el ADR prohíbe.
    ///
    /// 2. Un estado terminal propio —un «Removed» irreversible— respondería la pregunta «¿se retiró
    ///    esta concesión para no volver?», y nadie la hace: los ítems SOLO son mutables en Draft
    ///    (todas las mutaciones exigen <c>Status == Draft</c>) y una plantilla publicada se congela,
    ///    de modo que un ítem retirado NUNCA llegó a conceder nada —<c>Profile.AssignTemplate</c>
    ///    exige Published—. Retirar una línea de un borrador no es un hecho de negocio que haya que
    ///    hacer irreversible; lo que sí es un hecho —lo que una plantilla VIGENTE concedió— ya vive
    ///    en la cadena de versiones, porque cualquier cambio sobre una publicada nace como versión
    ///    nueva (<c>CreateNextVersion</c>) y la anterior queda intacta.
    ///
    /// Lo que el ADR sí exige y aquí se cumple: la fila sobrevive y la clave natural sigue ocupada
    /// (ver la guarda de <see cref="AddItem"/>).
    /// </summary>
    public Result DeactivateItem(IdValueObject itemId, ActorId updatedBy)
    {
        return ExecuteOnItem(itemId, item => item.Deactivate(updatedBy), updatedBy);
    }

    private Result ExecuteOnItem(IdValueObject itemId, Func<PermissionTemplateItemEntity, Result> action, ActorId updatedBy)
    {
        if (Status != TemplateStatus.Draft)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Authorization.TemplateNotDraft));
        }

        var item = FindItem(itemId);
        if (item.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Items), DomainErrors.Common.NotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var result = action(item.Value);
        if (result.IsFailure)
        {
            return result;
        }

        TrackingState.MarkAsDirty();
        DomainEvents.RaiseEvent(new PermissionTemplateMutatedEvent(Props.Id.GetValue(), Props.Version.GetValue()));
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    private Result<PermissionTemplateItemEntity> FindItem(IdValueObject itemId)
    {
        // AT06/F1 (misma clase de bug): identidad canónica = Props.Id; el Id base de Entity<> se
        // regenera aleatorio en cada construcción y la rehidratación no llama SetId → buscar por i.Id
        // fallaba tras recargar (toggle/remove de ítem por id → 404). Cf. FindMfaEnrollment (correcto).
        var item = _items.FirstOrDefault(i => i.Props.Id.GetValue() == itemId.GetValue());
        return item is null
            ? Result<PermissionTemplateItemEntity>.Failure(DomainErrors.Common.NotFound)
            : Result<PermissionTemplateItemEntity>.Success(item);
    }

}
