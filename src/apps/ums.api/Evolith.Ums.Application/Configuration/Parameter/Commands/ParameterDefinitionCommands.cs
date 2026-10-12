namespace Evolith.Ums.Application.Configuration.Parameter.Commands;

using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.Parameter;
using Evolith.Ums.Domain.Configuration.Parameter.ValueObjects;

// ── Create ───────────────────────────────────────────────────────────────────

public sealed record CreateParameterDefinitionCommand(
    string Code,
    string Name,
    string Description,
    int    DataTypeId,
    string DefaultValue,
    int    ScopeId,
    bool   IsMandatory,
    int    DisplayOrder) : ICommand<Guid>;

public sealed class CreateParameterDefinitionCommandHandler(
    IParameterDefinitionRepository repo,
    IUserContext userContext)
    : ICommandHandler<CreateParameterDefinitionCommand, Guid>
{
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<Guid>> Handle(CreateParameterDefinitionCommand cmd, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result<Guid>.Failure("Authenticated user is required.");

        var count = await repo.CountByCodeAsync(cmd.Code.ToUpperInvariant(), cancellationToken);
        if (count > 0)
            return Result<Guid>.Failure(DomainErrors.Configuration.ParameterCodeNotUnique);

        var result = ParameterDefinition.Create(
            Code.Create(cmd.Code),
            ParameterName.Create(cmd.Name),
            Description.Create(cmd.Description),
            ParameterDataType.FromValue(cmd.DataTypeId),
            DefaultValue.Create(cmd.DefaultValue),
            ParameterScope.FromValue(cmd.ScopeId),
            isActive:     true,
            isMandatory:  cmd.IsMandatory,
            displayOrder: cmd.DisplayOrder,
            ActorId.Create(userContext.UserId));

        if (result.IsFailure) return Result<Guid>.Failure(result.Error);

        await repo.AddAsync(result.Value, cancellationToken);
        await repo.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(result.Value.Props.Id.GetValue());
    }
}

// ── Update ───────────────────────────────────────────────────────────────────

public sealed record UpdateParameterDefinitionCommand(
    Guid   Id,
    string Name,
    string Description,
    string DefaultValue,
    int    ScopeId,
    bool   IsActive,
    bool   IsMandatory,
    int    DisplayOrder) : ICommand;

public sealed class UpdateParameterDefinitionCommandHandler(
    IParameterDefinitionRepository repo,
    IUserContext userContext)
    : ICommandHandler<UpdateParameterDefinitionCommand>
{
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(UpdateParameterDefinitionCommand cmd, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var definition = await repo.GetByIdAsync(cmd.Id, cancellationToken);
        if (definition is null) return Result.Failure(DomainErrors.Common.NotFound);

        var result = definition.Update(
            ParameterName.Create(cmd.Name),
            Description.Create(cmd.Description),
            DefaultValue.Create(cmd.DefaultValue),
            ParameterScope.FromValue(cmd.ScopeId),
            cmd.IsActive, cmd.IsMandatory, cmd.DisplayOrder,
            ActorId.Create(userContext.UserId));

        if (result.IsFailure) return result;

        await repo.UpdateAsync(definition, cancellationToken);
        await repo.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ── Archive ──────────────────────────────────────────────────────────────────

public sealed record ArchiveParameterDefinitionCommand(Guid Id) : ICommand;

public sealed class ArchiveParameterDefinitionCommandHandler(
    IParameterDefinitionRepository repo,
    IUserContext userContext)
    : ICommandHandler<ArchiveParameterDefinitionCommand>
{
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ArchiveParameterDefinitionCommand cmd, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var definition = await repo.GetByIdAsync(cmd.Id, cancellationToken);
        if (definition is null) return Result.Failure(DomainErrors.Common.NotFound);

        // Solo bloquean los dependientes VIVOS; uno ya eliminado lógicamente dejó de ser referencia.
        var globalCount = await repo.CountLiveGlobalValuesAsync(cmd.Id, cancellationToken);
        var tenantCount = await repo.CountLiveTenantValuesAsync(cmd.Id, cancellationToken);

        var result = definition.Archive(ActorId.Create(userContext.UserId), globalCount, tenantCount);
        if (result.IsFailure) return result;

        await repo.UpdateAsync(definition, cancellationToken);
        await repo.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ── Delete (borrado LÓGICO) ───────────────────────────────────────────────────
// El frontend invoca DELETE /parameter-definitions/{id} y el contrato HTTP no cambia (204 en
// éxito, 409 con dependientes, 404 si no existe). Lo que cambia es que la fila NO se borra: se
// marca como eliminada y desaparece de las lecturas. Motivo: sobre esta configuración se hacen
// consultas históricas, y una fila borrada de verdad se pierde para siempre — una definición
// retirada hace un año tiene que seguir explicando por qué el sistema se comportó como se comportó.
//
// Regla transaccional (análoga al RESTRICT de una FK): no se elimina lógicamente algo con
// referencias REALES vivas. Los valores globales/de inquilino ya eliminados lógicamente NO cuentan.

public sealed record DeleteParameterDefinitionCommand(Guid Id) : ICommand;

public sealed class DeleteParameterDefinitionCommandHandler(
    IParameterDefinitionRepository repo,
    IUserContext userContext)
    : ICommandHandler<DeleteParameterDefinitionCommand>
{
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(DeleteParameterDefinitionCommand cmd, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        // GetByIdAsync ya no devuelve definiciones eliminadas → un segundo DELETE responde 404.
        var definition = await repo.GetByIdAsync(cmd.Id, cancellationToken);
        if (definition is null) return Result.Failure(DomainErrors.Common.NotFound);

        var globalCount = await repo.CountLiveGlobalValuesAsync(cmd.Id, cancellationToken);
        var tenantCount = await repo.CountLiveTenantValuesAsync(cmd.Id, cancellationToken);

        if (globalCount > 0 || tenantCount > 0)
        {
            // 409 con el detalle de qué bloquea: el actor debe eliminar antes esos valores.
            var deps = new List<BlockingDependency>();
            if (globalCount > 0) deps.Add(new BlockingDependency("ParameterGlobalValue", "Active", globalCount));
            if (tenantCount > 0) deps.Add(new BlockingDependency("ParameterTenantValue", "Active", tenantCount));
            return Result.Failure(
                BlockedOperationError.Encode(DomainErrors.Configuration.ParameterHasActiveValues, deps));
        }

        // La guarda se repite dentro del agregado: la invariante vive en el dominio, no en el handler.
        var result = definition.Delete(ActorId.Create(userContext.UserId), globalCount, tenantCount);
        if (result.IsFailure) return result;

        await repo.UpdateAsync(definition, cancellationToken);
        await repo.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
