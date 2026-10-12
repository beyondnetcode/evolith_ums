namespace Evolith.Ums.Domain.Configuration.AppConfiguration;

using Evolith.Ums.Domain.Configuration.AppConfiguration.Events;

public sealed class AppConfiguration : AggregateRoot<AppConfiguration, AppConfigurationProps>
{
    public new AppConfigurationDomainEventsManager DomainEvents { get; }

    private AppConfiguration(AppConfigurationProps props) : base(props)
    {
        DomainEvents = new AppConfigurationDomainEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new AppConfigCreatedEvent(
                props.Id.GetValue(), props.Scope.Name, props.Code.GetValue(), props.Version));
        }
    }

    public Code Code => Props.Code;
    public ConfigurationScope Scope => Props.Scope;
    public ConfigStatus Status => Props.Status;
    public bool IsInheritable => Props.IsInheritable;
    public string Version => Props.Version;

    public AppConfigurationId GetId() => AppConfigurationId.Load(Props.Id.GetValue());

    public static Result<AppConfiguration> Create(
        TenantId? tenantId,
        SystemSuiteId? systemSuiteId,
        IdValueObject? moduleId,
        Code code,
        ConfigurationValue value,
        Description description,
        bool isInheritable,
        bool isEncrypted,
        ActorId createdBy,
        bool isNonOverridable = false)
    {
        var props = new AppConfigurationProps(
            IdValueObject.Create(), tenantId, systemSuiteId, moduleId,
            code, value, description, isInheritable, isEncrypted, createdBy, isNonOverridable);

        var config = new AppConfiguration(props);

        if (!config.IsValid())
        {
            return Result<AppConfiguration>.Failure(config.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<AppConfiguration>.Success(config);
    }

    public Result Publish(ActorId updatedBy)
    {
        if (Status != ConfigStatus.Draft)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Configuration.AppConfigNotDraft));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Status = ConfigStatus.Published;
        DomainEvents.RaiseEvent(new AppConfigPublishedEvent(Props.Id.GetValue(), Props.Code.GetValue(), Props.Version));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Archive(ActorId updatedBy)
    {
        if (Status != ConfigStatus.Published)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Configuration.AppConfigNotPublished));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Status = ConfigStatus.Archived;
        DomainEvents.RaiseEvent(new AppConfigArchivedEvent(Props.Id.GetValue(), Props.Code.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// Borrado LÓGICO (política del propietario: el borrado físico no existe en este dominio).
    ///
    /// Transición TERMINAL desde cualquier estado vivo (Draft, Published o Archived): a diferencia
    /// de <see cref="Archive"/> —que exige Published porque es un paso del ciclo de vida—, borrar
    /// es una retirada del catálogo y debe poder ejercerse sobre un borrador que nunca se publicó.
    /// La fila NO se elimina: se marca, y las lecturas dejan de verla.
    ///
    /// No se modela como `Archive` porque archivar deja la configuración visible y consultable;
    /// si borrar mapeara a Archived habría que elegir entre ocultar también lo archivado (romper
    /// el archivado) o no ocultar lo borrado (romper el borrado). Un estado propio evita el falso dilema.
    /// </summary>
    public Result Delete(ActorId deletedBy)
    {
        if (Status == ConfigStatus.Deleted)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Configuration.AppConfigAlreadyDeleted));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Status = ConfigStatus.Deleted;
        DomainEvents.RaiseEvent(new AppConfigDeletedEvent(Props.Id.GetValue(), Props.Code.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(deletedBy.GetValue());
        return Result.Success();
    }

    // Publish/Archive/Update exigen Draft o Published, así que una configuración Deleted ya queda
    // congelada por esas guardas: no hace falta repetir la comprobación del estado terminal.
    public Result Update(ConfigurationValue newValue, Description newDescription, ActorId updatedBy)
    {
        if (Status != ConfigStatus.Draft)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Configuration.AppConfigNotDraft));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Value = newValue;
        Props.Description = newDescription;
        Props.Version = BumpMinorVersion(Props.Version);
        DomainEvents.RaiseEvent(new AppConfigUpdatedEvent(Props.Id.GetValue(), Props.Code.GetValue(), Props.Version));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    private static string BumpMinorVersion(string semver)
    {
        if (System.Version.TryParse(semver, out var v))
            return $"{v.Major}.{v.Minor + 1}.0";
        return semver;
    }
}
