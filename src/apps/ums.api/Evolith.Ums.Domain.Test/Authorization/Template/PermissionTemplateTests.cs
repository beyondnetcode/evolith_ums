namespace Evolith.Ums.Domain.Test.Authorization.Template;

using Evolith.Ums.Domain.Authorization.Template;
using Xunit;

public class PermissionTemplateTests
{
    private static readonly TenantId ValidTenantId = TenantId.Load(Guid.NewGuid().ToString());
    private static readonly RoleId ValidRoleId = RoleId.Load(Guid.NewGuid().ToString());
    private static readonly SystemSuiteId ValidSystemSuiteId = SystemSuiteId.Load(Guid.NewGuid().ToString());
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    private static readonly ExclusiveArcTarget ValidTargetType = ExclusiveArcTarget.Module;
    private static readonly IdValueObject ValidTargetId = IdValueObject.Create();
    private static readonly ActionId ValidActionId = ActionId.Load(Guid.NewGuid().ToString());
    #region Create

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidTenantId, result.Value.TenantId);
        Assert.Equal(ValidRoleId, result.Value.RoleId);
        Assert.Equal(ValidSystemSuiteId, result.Value.SystemSuiteId);
        Assert.Equal(TemplateStatus.Draft, result.Value.Status);
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public void Create_RaisesPermissionTemplateCreatedEvent()
    {
        var result = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor);

        Assert.True(result.IsSuccess);
        var events = result.Value.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Single(events);
        Assert.IsType<PermissionTemplateCreatedEvent>(events[0]);
    }

    #endregion

    #region CreateNextVersion

    [Fact]
    public void CreateNextVersion_WhenNoExistingVersions_UsesInitial()
    {
        var result = PermissionTemplate.CreateNextVersion(
            ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor, Array.Empty<TemplateVersion>());

        Assert.True(result.IsSuccess);
        Assert.Equal("0.1.0", result.Value.Version.GetValue());
    }

    [Fact]
    public void CreateNextVersion_WhenNullExistingVersions_UsesInitial()
    {
        var result = PermissionTemplate.CreateNextVersion(
            ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor, null!);

        Assert.True(result.IsSuccess);
        Assert.Equal("0.1.0", result.Value.Version.GetValue());
    }

    [Fact]
    public void CreateNextVersion_WhenInitialExists_IncrementsMinor()
    {
        var result = PermissionTemplate.CreateNextVersion(
            ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor,
            new[] { TemplateVersion.Initial() });

        Assert.True(result.IsSuccess);
        Assert.Equal("0.2.0", result.Value.Version.GetValue());
    }

    [Fact]
    public void CreateNextVersion_UsesMaxExistingVersion_NotStringOrder()
    {
        // 2.0.0 es la máxima real; el orden alfabético colocaría "10.0.0" < "2.0.0" pero
        // aquí probamos la comparación numérica por segmento.
        var result = PermissionTemplate.CreateNextVersion(
            ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor,
            new[] { TemplateVersion.Initial(), TemplateVersion.Create(2, 0, 0), TemplateVersion.Create(1, 5, 0) });

        Assert.True(result.IsSuccess);
        Assert.Equal("2.1.0", result.Value.Version.GetValue());
    }

    #endregion

    #region Publish

    [Fact]
    public void Publish_WhenDraft_ReturnsSuccess()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);

        var result = template.Publish(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(TemplateStatus.Published, template.Status);
    }

    [Fact]
    public void Publish_WhenDraftWithNoItems_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;

        var result = template.Publish(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateItemsRequired, result.Error);
    }

    [Fact]
    public void Publish_WhenNotDraft_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        template.Publish(ValidActor);

        var result = template.Publish(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateNotDraft, result.Error);
    }

    [Fact]
    public void Publish_RaisesPermissionTemplatePublishedEvent()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);

        template.Publish(ValidActor);

        var events = template.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is PermissionTemplatePublishedEvent);
    }

    #endregion

    #region Deprecate

    [Fact]
    public void Deprecate_WhenPublished_ReturnsSuccess()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        template.Publish(ValidActor);

        var result = template.Deprecate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(TemplateStatus.Deprecated, template.Status);
    }

    [Fact]
    public void Deprecate_WhenDraft_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;

        var result = template.Deprecate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateNotPublished, result.Error);
    }

    [Fact]
    public void Deprecate_WhenAlreadyDeprecated_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.Publish(ValidActor);
        template.Deprecate(ValidActor);

        var result = template.Deprecate(ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Deprecate_RaisesPermissionTemplateDeprecatedEvent()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        template.Publish(ValidActor);

        template.Deprecate(ValidActor);

        var events = template.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is PermissionTemplateDeprecatedEvent);
    }

    #endregion

    #region Delete

    [Fact]
    public void Delete_WhenDraft_ReturnsSuccess()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;

        var result = template.Delete(ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Delete_WhenDeprecated_ReturnsSuccess()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        template.Publish(ValidActor);
        template.Deprecate(ValidActor);

        var result = template.Delete(ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Delete_WhenPublished_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        template.Publish(ValidActor);

        var result = template.Delete(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateNotDeletable, result.Error);
    }

    [Fact]
    public void Delete_WhenHasActiveProfiles_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;

        var result = template.Delete(ValidActor, activeProfileCount: 1);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateHasActiveProfiles, result.Error);
    }

    [Fact]
    public void Delete_RaisesPermissionTemplateDeletedEvent()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;

        template.Delete(ValidActor);

        var events = template.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is PermissionTemplateDeletedEvent);
    }

    // ── Borrado LÓGICO: el estado terminal es la política, no un efecto colateral ──

    [Fact]
    public void Delete_TransicionaAlEstadoTerminalDeleted_YNoDestruyeElAgregado()
    {
        // Fija la política: eliminar es cambiar de estado, no desaparecer. Los ítems —el rastro de qué
        // concesiones otorgó la plantilla— siguen ahí, que es lo que el negocio consulta hacia atrás.
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);

        var result = template.Delete(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(TemplateStatus.Deleted, template.Status);
        Assert.True(template.IsDeleted);
        Assert.Single(template.Items);
    }

    [Fact]
    public void Delete_CuandoYaEstaEliminada_ReturnsFailure()
    {
        // El estado terminal no es repetible: un segundo borrado es conflicto, no éxito silencioso.
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.Delete(ValidActor);

        var result = template.Delete(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateAlreadyDeleted, result.Error);
    }

    [Fact]
    public void Delete_ConReferenciaViva_Bloquea_YConLaReferenciaYaEliminada_Permite()
    {
        // Regla de cascada en su forma más pura: el MISMO agregado se rechaza mientras la referencia
        // está viva (activeProfileCount > 0) y se acepta en cuanto esa referencia deja de estarlo.
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;

        var bloqueado = template.Delete(ValidActor, activeProfileCount: 2);

        Assert.True(bloqueado.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateHasActiveProfiles, bloqueado.Error);
        Assert.NotEqual(TemplateStatus.Deleted, template.Status);

        // Cada petición HTTP rehidrata el agregado y el repositorio limpia las reglas rotas
        // (`aggregate.BrokenRules.Clear()`); se replica aquí para que el segundo intento parta del
        // mismo estado limpio que tendría en producción.
        template.BrokenRules.Clear();

        var permitido = template.Delete(ValidActor, activeProfileCount: 0);

        Assert.True(permitido.IsSuccess);
        Assert.Equal(TemplateStatus.Deleted, template.Status);
    }

    [Fact]
    public void Delete_NoPermiteMutarLaPlantillaDespues()
    {
        // Una plantilla eliminada no puede recibir ítems ni volver a publicarse: el ciclo de vida acabó.
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.Delete(ValidActor);

        var addResult = template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        Assert.True(addResult.IsFailure);

        var publishResult = template.Publish(ValidActor);
        Assert.True(publishResult.IsFailure);
    }

    #endregion

    #region AddItem

    [Fact]
    public void AddItem_WhenDraft_ReturnsSuccess()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;

        var result = template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(template.Items);
    }

    [Fact]
    public void AddItem_WhenNotDraft_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        template.Publish(ValidActor);
        var secondTargetId = IdValueObject.Create();

        var result = template.AddItem(ValidTargetType, secondTargetId, ValidActionId, true, false, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateNotDraft, result.Error);
    }

    [Fact]
    public void AddItem_WithDuplicateTarget_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);

        var result = template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, false, true, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateItemTargetAlreadyExists, result.Error);
    }

    /// <summary>
    /// G-192 — el agregado NO restringe el arco a los cuatro destinos de navegación: admite
    /// cualquier valor de <see cref="ExclusiveArcTarget"/>, incluidos los objetos de dominio.
    /// Esta prueba fija esa verdad para que la capa de aplicación no vuelva a estrecharla por su
    /// cuenta: el validador rechazaba Aggregate y Entity mientras el grafo sí sabía proyectarlos.
    /// </summary>
    [Theory]
    [InlineData(nameof(ExclusiveArcTarget.SystemSuite))]
    [InlineData(nameof(ExclusiveArcTarget.Module))]
    [InlineData(nameof(ExclusiveArcTarget.Submodule))]
    [InlineData(nameof(ExclusiveArcTarget.Option))]
    [InlineData(nameof(ExclusiveArcTarget.Aggregate))]
    [InlineData(nameof(ExclusiveArcTarget.Entity))]
    public void AddItem_AdmiteTodosLosDestinosDelArcoExclusivo(string targetTypeName)
    {
        var targetType = DomainEnumeration.GetAll<ExclusiveArcTarget>().Single(t => t.Name == targetTypeName);
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;

        var result = template.AddItem(targetType, ValidTargetId, ValidActionId, true, false, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(targetType, template.Items.Single().TargetType);
    }

    /// <summary>
    /// G-192 — el destino forma parte de la clave del ítem: el mismo recurso y la misma acción
    /// pueden concederse como Aggregate y como Entity sin que el agregado lo tome por duplicado.
    /// </summary>
    [Fact]
    public void AddItem_MismoDestinoConTipoDistinto_NoEsDuplicado()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        Assert.True(template.AddItem(ExclusiveArcTarget.Aggregate, ValidTargetId, ValidActionId, true, false, ValidActor).IsSuccess);

        var result = template.AddItem(ExclusiveArcTarget.Entity, ValidTargetId, ValidActionId, true, false, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, template.Items.Count);
    }

    [Fact]
    public void AddItem_RaisesPermissionTemplateMutatedEvent()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;

        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);

        var events = template.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is PermissionTemplateMutatedEvent);
    }

    #endregion

    #region Retirada de ítems (ADR-0164)

    // El borrado FÍSICO de ítems se retiró del dominio: `RemoveItem` ya no existe y la colección no
    // puede encoger. Estas pruebas fijan las cuatro afirmaciones que sostienen la política.

    [Fact]
    public void ElDominioNoOfreceBorradoFisicoDeItems()
    {
        // La garantía es la AUSENCIA del método (ADR-0164 §2.1: «una capacidad que no debe usarse y
        // sigue disponible acaba usándose»). Se comprueba por reflexión porque una llamada que no
        // compila no puede escribirse como prueba: sin esto, nada impediría reintroducirlo mañana.
        var metodos = typeof(PermissionTemplate).GetMethods().Select(m => m.Name).ToList();

        Assert.DoesNotContain("RemoveItem", metodos);
        Assert.DoesNotContain("DeleteItem", metodos);
    }

    [Fact]
    public void DeactivateItem_NoEncogeLaColeccion_LaConcesionSigueAhiRetirada()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        var itemId = template.Items.First().GetId();

        var result = template.DeactivateItem(itemId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(template.Items);
        Assert.False(template.Items.First().IsActive);
        // Y la separación que evita la brecha: retirado deja de estar entre lo que la plantilla concede.
        Assert.Empty(template.ActiveItems);
    }

    [Fact]
    public void CicloRetirarYReactivar_DevuelveLaConcesionAlServicio_SobreLaMismaFila()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        var itemId = template.Items.First().GetId();

        Assert.True(template.DeactivateItem(itemId, ValidActor).IsSuccess);
        Assert.True(template.ActivateItem(itemId, ValidActor).IsSuccess);

        Assert.Single(template.Items);
        Assert.Single(template.ActiveItems);
        Assert.Equal(itemId.GetValue(), template.Items.First().GetId().GetValue(),
            comparer: EqualityComparer<Guid>.Default);
    }

    [Fact]
    public void AddItem_SobreLaClaveDeUnItemRetirado_EsConflicto_LaClaveNoSeLibera()
    {
        // ADR-0164 §2.3: la clave natural no se reutiliza. El alta responde conflicto de DOMINIO
        // legible —no un error de índice— y nombra la retirada, para que el cliente pueda proponer
        // reactivar en vez de repetir el alta.
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        template.DeactivateItem(template.Items.First().GetId(), ValidActor);

        var result = template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateItemTargetRetired, result.Error);
        Assert.Single(template.Items);
    }

    [Fact]
    public void AddItem_ConLaMismaClaveEnInstanciasDistintas_DetectaElDuplicado()
    {
        // Contraprueba de la comparación por VALOR. IdValueObject no sobrecarga ==, así que la guarda
        // anterior (i.TargetId == targetId) comparaba REFERENCIAS: sobre una plantilla rehidratada
        // desde la base —donde cada ítem trae sus propias instancias— el duplicado pasaba de largo y
        // el alta reventaba contra el índice único como 23505. Misma clase de bug que G-043.
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        var targetId = IdValueObject.Create();
        var actionGuid = Guid.NewGuid();

        Assert.True(template.AddItem(
            ValidTargetType, IdValueObject.Load(targetId.GetValue()), ActionId.Load(actionGuid),
            true, false, ValidActor).IsSuccess);

        var result = template.AddItem(
            ValidTargetType, IdValueObject.Load(targetId.GetValue()), ActionId.Load(actionGuid),
            true, false, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateItemTargetAlreadyExists, result.Error);
    }

    [Fact]
    public void Publish_ConTodosLosItemsRetirados_Falla()
    {
        // Puerta que abre el borrado lógico: antes, retirar el último ítem lo borraba de la lista y
        // `Items.Any()` bastaba para rechazar la publicación. Ahora la fila sobrevive, así que la
        // guarda tiene que mirar lo VIGENTE; si no, se publicaría un contrato que no concede nada y
        // sería asignable a perfiles.
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        template.DeactivateItem(template.Items.First().GetId(), ValidActor);

        var result = template.Publish(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateItemsRequired, result.Error);
    }

    [Fact]
    public void DeactivateItem_CuandoNoEsBorrador_Falla()
    {
        // Publicada, la plantilla se congela: retirar una concesión de una plantilla vigente exige
        // una versión nueva. Es lo que hace que el histórico viva en la cadena de versiones y no
        // necesite un mecanismo propio.
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        template.Publish(ValidActor);
        var itemId = template.Items.First().GetId();

        var result = template.DeactivateItem(itemId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateNotDraft, result.Error);
    }

    [Fact]
    public void DeactivateItem_CuandoElItemNoExiste_Falla()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;

        var result = template.DeactivateItem(IdValueObject.Create(), ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.NotFound, result.Error);
    }

    #endregion

    #region SetItemAllow

    [Fact]
    public void SetItemAllow_WhenItemExists_ReturnsSuccess()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, false, false, ValidActor);
        var itemId = template.Items.First().GetId();

        var result = template.SetItemAllow(itemId, ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SetItemAllow_WhenItemNotFound_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = template.SetItemAllow(fakeId, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void SetItemAllow_WhenNotDraft_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        template.Publish(ValidActor);
        var itemId = template.Items.First().GetId();

        var result = template.SetItemAllow(itemId, ValidActor);

        Assert.True(result.IsFailure);
    }

    #endregion

    #region SetItemDeny

    [Fact]
    public void SetItemDeny_WhenItemExists_ReturnsSuccess()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, false, false, ValidActor);
        var itemId = template.Items.First().GetId();

        var result = template.SetItemDeny(itemId, ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SetItemDeny_WhenNotDraft_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        template.Publish(ValidActor);
        var itemId = template.Items.First().GetId();

        var result = template.SetItemDeny(itemId, ValidActor);

        Assert.True(result.IsFailure);
    }

    #endregion

    #region SetItemNeutral

    [Fact]
    public void SetItemNeutral_WhenItemExists_ReturnsSuccess()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        var itemId = template.Items.First().GetId();

        var result = template.SetItemNeutral(itemId, ValidActor);

        Assert.True(result.IsSuccess);
    }

    #endregion

    #region ActivateItem

    [Fact]
    public void ActivateItem_WhenItemExists_ReturnsSuccess()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        var itemId = template.Items.First().GetId();

        var result = template.ActivateItem(itemId, ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ActivateItem_WhenNotDraft_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        template.Publish(ValidActor);
        var itemId = template.Items.First().GetId();

        var result = template.ActivateItem(itemId, ValidActor);

        Assert.True(result.IsFailure);
    }

    #endregion

    #region DeactivateItem

    [Fact]
    public void DeactivateItem_WhenItemExists_ReturnsSuccess()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        var itemId = template.Items.First().GetId();

        var result = template.DeactivateItem(itemId, ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void DeactivateItem_WhenNotDraft_ReturnsFailure()
    {
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, ValidSystemSuiteId, ValidActor).Value;
        template.AddItem(ValidTargetType, ValidTargetId, ValidActionId, true, false, ValidActor);
        template.Publish(ValidActor);
        var itemId = template.Items.First().GetId();

        var result = template.DeactivateItem(itemId, ValidActor);

        Assert.True(result.IsFailure);
    }

    #endregion
}
