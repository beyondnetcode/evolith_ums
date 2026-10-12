namespace Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Domain.Identity.Tenant.Branch;
using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;
using Evolith.Ums.Domain.Identity.Tenant.TenantParameter;
using Evolith.Ums.Domain.Identity.Tenant.Events;
using BranchEntity = Evolith.Ums.Domain.Identity.Tenant.Branch.Branch;
using IdentityProviderEntity = Evolith.Ums.Domain.Identity.Tenant.IdentityProvider.IdentityProvider;
using TenantParameterEntity = Evolith.Ums.Domain.Identity.Tenant.TenantParameter.TenantParameter;

public sealed class Tenant : AggregateRoot<Tenant, TenantProps>
{
    private readonly List<BranchEntity> _branches = new();
    private readonly List<IdentityProviderEntity> _identityProviders = new();
    private readonly List<TenantParameterEntity> _parameters = new();

    public new TenantDomainEventsManager DomainEvents { get; }

    private Tenant(TenantProps props) : base(props)
    {
        DomainEvents = new TenantDomainEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new TenantCreatedEvent(Props.Id.GetValue(), Props.Code.GetValue(), Props.Name.GetValue()));
        }
    }

    public Code Code => Props.Code;
    public Name Name => Props.Name;
    public OrganizationType Type => Props.Type;
    public IdpStrategy IdpStrategy => Props.IdpStrategy;
    public CompanyReference? CompanyReference => Props.CompanyReference;
    public TenantId? ParentTenantId => Props.ParentTenantId;
    public bool IsManagementOwner => Props.IsManagementOwner;
    public TenantStatus Status => Props.Status;

    // FR-042 (ADR-UMS-097 §2.2): suite por defecto del inquilino (null = suite única / sin filtro).
    public SystemSuiteId? DefaultSystemSuiteId => Props.DefaultSystemSuiteId;

    /// <summary>
    /// TODAS las sucursales del inquilino, incluidas las cerradas definitivamente. La colección no
    /// encoge nunca (ADR-0164 §2.1): la vía de escritura necesita verlas para que el código siga
    /// ocupado y para que la resolución por id de un perfil antiguo siga encontrando su sucursal.
    /// Quien LISTA para un humano debe filtrar por <c>!IsClosed</c>; ver
    /// <c>GetBranchesByTenantIdQueryHandler</c>.
    /// </summary>
    public IReadOnlyCollection<BranchEntity> Branches => _branches.AsReadOnly();
    public IReadOnlyCollection<IdentityProviderEntity> IdentityProviders => _identityProviders.AsReadOnly();
    public IReadOnlyCollection<TenantParameterEntity> Parameters => _parameters.AsReadOnly();

    public IdentityProviderEntity? GetActiveIdentityProvider()
    {
        return _identityProviders.FirstOrDefault(ip => ip.IsActive);
    }

    public static Result<Tenant> Create(
        Code code,
        Name name,
        OrganizationType type,
        ActorId createdBy,
        IdpStrategy idpStrategy = null!,
        CompanyReference? companyReference = null,
        TenantId? parentTenantId = null,
        TenantId? tenantId = null,
        bool isManagementOwner = false)
    {
        idpStrategy ??= IdpStrategy.InternalBcrypt;

        var id = tenantId ?? IdValueObject.Create();
        var props = new TenantProps(
            id,
            code,
            name,
            type,
            idpStrategy,
            companyReference,
            parentTenantId,
            isManagementOwner,
            createdBy);

        var tenant = new Tenant(props);

        if (!tenant.IsValid())
        {
            return Result<Tenant>.Failure(tenant.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<Tenant>.Success(tenant);
    }

    public Result<BranchEntity> AddBranch(Code code, Name name, ActorId createdBy, Value? geofencingMetadata = null)
    {
        // La comparación NO excluye las sucursales cerradas, y es deliberado: el código de una
        // sucursal cerrada queda ocupado PARA SIEMPRE (ADR-0164 §2.3). Liberarlo permitiría dos
        // sucursales distintas con el mismo código bajo el mismo inquilino, y una consulta sobre un
        // despacho de 2024 no podría decir a cuál de las dos se refiere. El índice único de la base
        // tampoco filtra por estado, así que ambos lados dicen lo mismo.
        if (_branches.Any(b => b.Code.Equals(code)))
        {
            BrokenRules.Add(new BrokenRule(nameof(Branches), DomainErrors.Tenant.BranchCodeNotUnique));
        }

        if (!IsValid())
        {
            return Result<BranchEntity>.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var branchResult = BranchEntity.Create(TenantId.Load(Props.Id.GetValue()), code, name, createdBy, geofencingMetadata);
        if (branchResult.IsFailure)
        {
            return Result<BranchEntity>.Failure(branchResult.Error);
        }

        _branches.Add(branchResult.Value);
        DomainEvents.RaiseEvent(new BranchCreatedEvent(Props.Id.GetValue(), branchResult.Value.GetId().GetValue(), code.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(createdBy.GetValue());
        return Result<BranchEntity>.Success(branchResult.Value);
    }

    /// <summary>
    /// CIERRE DEFINITIVO de una sucursal (ADR-0164 §2.1): el verbo terminal del ciclo de vida.
    ///
    /// <para>Sustituye al antiguo <c>RemoveBranch</c>, que quitaba la sucursal de la colección y el
    /// reconciliador de EF traducía en un <c>DELETE</c> real. Ese borrado dejaba huérfanos en
    /// silencio —<c>Profiles.BranchId</c> y <c>UserAccounts.BranchId</c> no tienen clave ajena
    /// contra <c>TenantBranches</c>— y, sobre todo, hacía imposible explicar un despacho pasado:
    /// la sucursal de la que salió ya no existía. La colección ya NUNCA encoge.</para>
    ///
    /// <para>Es un verbo distinto de <see cref="DeactivateBranch"/>, no otro nombre para lo mismo
    /// (ADR-0164 §2.4): desactivar es una pausa reversible; cerrar no se revierte, y no se llega a
    /// él cambiando <c>IsActive</c>.</para>
    /// </summary>
    /// <param name="activeUserCount">
    /// Cuentas ACTIVAS asignadas a la sucursal. Las cuenta la aplicación —viven en otro agregado— y
    /// el dominio solo decide con ellas, igual que <c>UserAccount.Delete(activeProfileCount)</c>.
    /// Lo ya eliminado no bloquea, porque el recuento solo mira lo activo (ADR-0164 §2.2).
    /// </param>
    /// <param name="activeProfileCount">Perfiles ACTIVOS acotados a la sucursal, con la misma regla.</param>
    public Result CloseBranch(
        IdValueObject branchId,
        ActorId closedBy,
        int activeUserCount = 0,
        int activeProfileCount = 0,
        string? reason = null)
    {
        var branch = FindBranch(branchId);
        if (branch.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Branches), DomainErrors.Common.NotFound));
        }

        if (branch.IsSuccess)
        {
            var canClose = branch.Value.CanClose();
            if (canClose.IsFailure)
            {
                BrokenRules.Add(new BrokenRule(nameof(Branches), canClose.Error));
            }
        }

        // Guarda de cascada, el análogo de un ON DELETE RESTRICT: se verifica ANTES de actuar y se
        // rechaza nombrando qué bloquea, en vez de arrastrar en cascada o de huerfanizar.
        if (activeUserCount > 0 || activeProfileCount > 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(Branches), DomainErrors.Tenant.BranchHasLiveReferences));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        branch.Value.CloseInternal(closedBy, reason);
        DomainEvents.RaiseEvent(new BranchClosedEvent(
            Props.Id.GetValue(),
            branch.Value.GetId().GetValue(),
            branch.Value.Code.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(closedBy.GetValue());
        return Result.Success();
    }

    public Result DeactivateBranch(IdValueObject branchId, ActorId updatedBy, string? reason = null)
    {
        var branch = FindBranch(branchId);
        if (branch.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Branches), DomainErrors.Common.NotFound));
        }

        if (branch.IsSuccess)
        {
            var canDeactivate = branch.Value.CanDeactivate();
            if (canDeactivate.IsFailure)
            {
                // Se propaga el código REAL en vez de colapsarlo a `common.invalid`: «ya está
                // inactiva» (400) y «está cerrada» (409) son rechazos distintos, y la presentación
                // solo puede distinguirlos si el código llega hasta ella.
                BrokenRules.Add(new BrokenRule(nameof(Branches), canDeactivate.Error));
            }
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        branch.Value.DeactivateInternal(updatedBy, reason);
        DomainEvents.RaiseEvent(new BranchDeactivatedEvent(Props.Id.GetValue(), branch.Value.GetId().GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result ReactivateBranch(IdValueObject branchId, ActorId updatedBy, string? reason = null)
    {
        var branch = FindBranch(branchId);
        if (branch.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Branches), DomainErrors.Common.NotFound));
        }

        if (branch.IsSuccess)
        {
            var canReactivate = branch.Value.CanReactivate();
            if (canReactivate.IsFailure)
            {
                // Igual que arriba: una sucursal CERRADA no se reactiva, y ese rechazo debe verse.
                BrokenRules.Add(new BrokenRule(nameof(Branches), canReactivate.Error));
            }
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        branch.Value.ReactivateInternal(updatedBy, reason);
        DomainEvents.RaiseEvent(new BranchReactivatedEvent(Props.Id.GetValue(), branch.Value.GetId().GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// Vacía el búfer de asientos de bitácora ya persistidos. Lo llama el repositorio tras confirmar
    /// la transacción, en paralelo a <c>DomainEvents.MarkChangesAsCommitted()</c>.
    /// </summary>
    public void MarkBranchLifecycleAsCommitted()
    {
        foreach (var branch in _branches)
        {
            branch.MarkLifecycleEntriesAsCommitted();
        }
    }

    public Result RegisterIdentityProvider(Code code, Name name, Description description, IdpStrategy strategy, ActorId createdBy)
    {
        if (_identityProviders.Any(ip => ip.Code.Equals(code)))
        {
            BrokenRules.Add(new BrokenRule(nameof(IdentityProviders), DomainErrors.Tenant.IdpCodeNotUnique));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var idpResult = IdentityProviderEntity.Create(TenantId.Load(Props.Id.GetValue()), code, name, description, strategy, createdBy);
        if (idpResult.IsFailure)
        {
            return Result.Failure(idpResult.Error);
        }

        _identityProviders.Add(idpResult.Value);
        DomainEvents.RaiseEvent(new IdentityProviderRegisteredEvent(Props.Id.GetValue(), idpResult.Value.GetId().GetValue(), code.GetValue(), strategy.Name));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(createdBy.GetValue());
        return Result.Success();
    }

    public Result ActivateIdentityProvider(IdValueObject identityProviderId, ActorId updatedBy)
    {
        var identityProvider = FindIdentityProvider(identityProviderId);
        if (identityProvider.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(IdentityProviders), DomainErrors.Tenant.IdpNotFound));
        }

        if (identityProvider.IsSuccess)
        {
            var canActivate = identityProvider.Value.CanActivate();
            if (canActivate.IsFailure)
            {
                BrokenRules.Add(new BrokenRule(nameof(IdentityProviders), DomainErrors.Tenant.IdpAlreadyActive));
            }
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        foreach (var ip in _identityProviders)
        {
            ip.DeactivateInternal();
        }

        identityProvider.Value.ActivateInternal();
        DomainEvents.RaiseEvent(new IdentityProviderActivatedEvent(Props.Id.GetValue(), identityProvider.Value.GetId().GetValue(), identityProvider.Value.Code.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result DeactivateIdentityProvider(IdValueObject identityProviderId, ActorId updatedBy)
    {
        var identityProvider = FindIdentityProvider(identityProviderId);
        if (identityProvider.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(IdentityProviders), DomainErrors.Tenant.IdpNotFound));
        }

        if (identityProvider.IsSuccess)
        {
            var canDeactivate = identityProvider.Value.CanDeactivate();
            if (canDeactivate.IsFailure)
            {
                BrokenRules.Add(new BrokenRule(nameof(IdentityProviders), DomainErrors.Tenant.IdpAlreadyInactive));
            }
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        identityProvider.Value.DeactivateInternal();
        DomainEvents.RaiseEvent(new IdentityProviderDeactivatedEvent(Props.Id.GetValue(), identityProvider.Value.GetId().GetValue(), identityProvider.Value.Code.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result RemoveIdentityProvider(IdValueObject identityProviderId, ActorId updatedBy)
    {
        var identityProvider = FindIdentityProvider(identityProviderId);
        if (identityProvider.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(IdentityProviders), DomainErrors.Tenant.IdpNotFound));
        }

        if (identityProvider.IsSuccess && identityProvider.Value.IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IdentityProviders), DomainErrors.Common.Invalid));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        _identityProviders.Remove(identityProvider.Value);
        DomainEvents.RaiseEvent(new IdentityProviderRemovedEvent(Props.Id.GetValue(), identityProvider.Value.GetId().GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result AddParameter(
        string code,
        string description,
        string value,
        TenantParameterValueType valueType,
        TenantParameterCategory category,
        bool isSensitive,
        string? defaultValue,
        string? allowedValues,
        ActorId createdBy)
    {
        if (_parameters.Any(p => p.Code.GetValue() == code && p.IsActive))
        {
            BrokenRules.Add(new BrokenRule(nameof(Parameters), DomainErrors.TenantParameter.CodeNotUnique));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var parameterResult = TenantParameterEntity.Create(
            TenantId.Load(Props.Id.GetValue()),
            code,
            description,
            value,
            valueType,
            category,
            isSensitive,
            defaultValue,
            allowedValues,
            createdBy);

        if (parameterResult.IsFailure)
        {
            return Result.Failure(parameterResult.Error);
        }

        _parameters.Add(parameterResult.Value);
        TrackingState.MarkAsDirty();
        Props.Audit.Update(createdBy.GetValue());
        return Result.Success();
    }

    public Result UpdateParameter(string code, string newValue, ActorId updatedBy)
    {
        var parameter = FindParameter(code);
        if (parameter.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Parameters), DomainErrors.TenantParameter.NotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var updateResult = parameter.Value.UpdateValue(newValue, updatedBy);
        if (updateResult.IsFailure)
        {
            return Result.Failure(updateResult.Error);
        }

        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result DeactivateParameter(string code, ActorId updatedBy)
    {
        var parameter = FindActiveParameter(code);
        if (parameter.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Parameters), DomainErrors.TenantParameter.NotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var deactivateResult = parameter.Value.Deactivate(updatedBy);
        if (deactivateResult.IsFailure)
        {
            return Result.Failure(deactivateResult.Error);
        }

        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public TenantParameterEntity? GetParameter(string code)
    {
        return _parameters.FirstOrDefault(p => p.Code.GetValue() == code && p.IsActive);
    }

    public T? GetTypedParameter<T>(string code, Func<string, T?> parser, T? defaultValue = default)
    {
        var parameter = GetParameter(code);
        if (parameter is null) return defaultValue;

        return parser(parameter.Value);
    }

    private Result<TenantParameterEntity> FindParameter(string code)
    {
        var parameter = _parameters.FirstOrDefault(p => p.Code.GetValue() == code);
        return parameter is null
            ? Result<TenantParameterEntity>.Failure(DomainErrors.TenantParameter.NotFound)
            : Result<TenantParameterEntity>.Success(parameter);
    }

    private Result<TenantParameterEntity> FindActiveParameter(string code)
    {
        var parameter = _parameters.FirstOrDefault(p => p.Code.GetValue() == code && p.IsActive);
        return parameter is null
            ? Result<TenantParameterEntity>.Failure(DomainErrors.TenantParameter.NotFound)
            : Result<TenantParameterEntity>.Success(parameter);
    }

    public Result Suspend(ActorId updatedBy, int activeUserCount = 0, int activeBranchCount = 0)
    {
        if (Props.Status == TenantStatus.Archived)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Tenant.ArchivedCannotSuspend));
        }

        if (Props.Status == TenantStatus.Suspended)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Tenant.AlreadySuspended));
        }

        if (activeUserCount > 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Tenant.HasActiveUsers));
        }

        if (activeBranchCount > 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Tenant.HasActiveBranches));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(TenantStatus.Suspended));
        DomainEvents.RaiseEvent(new TenantSuspendedEvent(Props.Id.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result SetManagementOwner(bool value, ActorId updatedBy)
    {
        SetProps(Props.WithManagementOwner(value));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    // FR-042 (ADR-UMS-097 §2.2): fija/limpia la suite por defecto del inquilino, que alimenta la
    // procedencia de la suite pre-autenticación cuando el AccessScope no la fija. Pasar null la limpia
    // (inquilino de suite única → la resolución omite el filtro por suite, comportamiento previo).
    public Result SetDefaultSystemSuite(SystemSuiteId? defaultSystemSuiteId, ActorId updatedBy)
    {
        SetProps(Props.WithDefaultSystemSuite(defaultSystemSuiteId));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    // FS-26 (G-024): actualiza los datos generales editables del tenant (nombre, tipo,
    // referencia fiscal). El Code permanece inmutable (identificador único).
    public Result UpdateGeneralData(Name name, OrganizationType type, CompanyReference? companyReference, ActorId updatedBy)
    {
        SetProps(Props.WithGeneralData(name, type, companyReference));

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        DomainEvents.RaiseEvent(new TenantUpdatedEvent(Props.Id.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    // FS-26 (G-024): actualiza los datos editables de una sucursal (nombre, geocerca).
    // El Code de sucursal es único dentro del tenant y permanece inmutable.
    public Result UpdateBranch(IdValueObject branchId, Name name, Value? geofencingMetadata, ActorId updatedBy)
    {
        var branch = FindBranch(branchId);
        if (branch.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Branches), DomainErrors.Common.NotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        branch.Value.UpdateInternal(name, geofencingMetadata);
        DomainEvents.RaiseEvent(new BranchUpdatedEvent(Props.Id.GetValue(), branch.Value.GetId().GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Activate(ActorId updatedBy)
    {
        if (Props.Status == TenantStatus.Archived)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Tenant.ArchivedCannotActivate));
        }

        if (Props.Status == TenantStatus.Active)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Tenant.AlreadyActive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(TenantStatus.Active));
        DomainEvents.RaiseEvent(new TenantActivatedEvent(Props.Id.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Archive(ActorId updatedBy, int activeIdpCount = 0)
    {
        if (Props.Status == TenantStatus.Archived)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Tenant.ArchivedCannotArchive));
        }

        if (activeIdpCount > 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Tenant.HasActiveIdpConfig));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(TenantStatus.Archived));
        DomainEvents.RaiseEvent(new TenantArchivedEvent(Props.Id.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    private Result<BranchEntity> FindBranch(IdValueObject branchId)
    {
        var branch = _branches.FirstOrDefault(b => b.Props.Id.GetValue() == branchId.GetValue());
        if (branch is null)
            return Result<BranchEntity>.Failure(DomainErrors.Common.NotFound);

        return Result<BranchEntity>.Success(branch);
    }

    private Result<IdentityProviderEntity> FindIdentityProvider(IdValueObject identityProviderId)
    {
        var identityProvider = _identityProviders.FirstOrDefault(ip => ip.Props.Id.GetValue() == identityProviderId.GetValue());
        if (identityProvider is null)
            return Result<IdentityProviderEntity>.Failure(DomainErrors.Common.NotFound);

        return Result<IdentityProviderEntity>.Success(identityProvider);
    }
}
