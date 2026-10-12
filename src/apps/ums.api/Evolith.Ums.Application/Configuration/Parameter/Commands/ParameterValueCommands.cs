namespace Evolith.Ums.Application.Configuration.Parameter.Commands;

using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.Parameter;
using Evolith.Ums.Domain.Configuration.Parameter.ValueObjects;

// ── ParameterGlobalValue: Create ─────────────────────────────────────────────

public sealed record CreateParameterGlobalValueCommand(
    Guid   DefinitionId,
    string Value) : ICommand<Guid>;

public sealed class CreateParameterGlobalValueCommandHandler(
    IParameterDefinitionRepository  definitionRepo,
    IParameterGlobalValueRepository valueRepo,
    IUserContext userContext)
    : ICommandHandler<CreateParameterGlobalValueCommand, Guid>
{
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<Guid>> Handle(CreateParameterGlobalValueCommand cmd, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result<Guid>.Failure("Authenticated user is required.");

        var definition = await definitionRepo.GetByIdAsync(cmd.DefinitionId, cancellationToken);
        if (definition is null) return Result<Guid>.Failure(DomainErrors.Common.NotFound);

        var existing = await valueRepo.GetByDefinitionIdAsync(cmd.DefinitionId, cancellationToken);
        if (existing is not null)
            return Result<Guid>.Failure("A global value already exists for this parameter. Use Update instead.");

        var result = ParameterGlobalValue.Create(
            IdValueObject.Load(cmd.DefinitionId),
            EffectiveValue.Create(cmd.Value),
            definition.DataType,
            ActorId.Create(userContext.UserId));

        if (result.IsFailure) return Result<Guid>.Failure(result.Error);

        await valueRepo.AddAsync(result.Value, cancellationToken);
        await valueRepo.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(result.Value.Props.Id.GetValue());
    }
}

// ── ParameterGlobalValue: Update ─────────────────────────────────────────────

public sealed record UpdateParameterGlobalValueCommand(Guid Id, string Value) : ICommand;

public sealed class UpdateParameterGlobalValueCommandHandler(
    IParameterDefinitionRepository  definitionRepo,
    IParameterGlobalValueRepository valueRepo,
    IUserContext userContext)
    : ICommandHandler<UpdateParameterGlobalValueCommand>
{
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(UpdateParameterGlobalValueCommand cmd, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var value = await valueRepo.GetByIdAsync(cmd.Id, cancellationToken);
        if (value is null) return Result.Failure(DomainErrors.Common.NotFound);

        var definition = await definitionRepo.GetByIdAsync(value.ParameterDefinitionId.GetValue(), cancellationToken);
        var dataType   = definition?.DataType ?? ParameterDataType.String;

        var result = value.UpdateValue(EffectiveValue.Create(cmd.Value), dataType, ActorId.Create(userContext.UserId));
        if (result.IsFailure) return result;

        await valueRepo.UpdateAsync(value, cancellationToken);
        await valueRepo.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ── ParameterGlobalValue: Publish ────────────────────────────────────────────

public sealed record PublishParameterGlobalValueCommand(Guid Id) : ICommand;

public sealed class PublishParameterGlobalValueCommandHandler(
    IParameterGlobalValueRepository valueRepo,
    IUserContext userContext)
    : ICommandHandler<PublishParameterGlobalValueCommand>
{
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(PublishParameterGlobalValueCommand cmd, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var value = await valueRepo.GetByIdAsync(cmd.Id, cancellationToken);
        if (value is null) return Result.Failure(DomainErrors.Common.NotFound);

        var result = value.Publish(ActorId.Create(userContext.UserId));
        if (result.IsFailure) return result;

        await valueRepo.UpdateAsync(value, cancellationToken);
        await valueRepo.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ── ParameterGlobalValue: Archive ────────────────────────────────────────────

public sealed record ArchiveParameterGlobalValueCommand(Guid Id) : ICommand;

public sealed class ArchiveParameterGlobalValueCommandHandler(
    IParameterGlobalValueRepository valueRepo,
    IUserContext userContext)
    : ICommandHandler<ArchiveParameterGlobalValueCommand>
{
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ArchiveParameterGlobalValueCommand cmd, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var value = await valueRepo.GetByIdAsync(cmd.Id, cancellationToken);
        if (value is null) return Result.Failure(DomainErrors.Common.NotFound);

        var result = value.Archive(ActorId.Create(userContext.UserId));
        if (result.IsFailure) return result;

        await valueRepo.UpdateAsync(value, cancellationToken);
        await valueRepo.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ── ParameterGlobalValue: Delete (borrado LÓGICO) ────────────────────────────
// Sin esta ruta la regla transaccional del borrado de definiciones sería un callejón sin salida:
// exige eliminar antes los dependientes, y no habría forma de eliminarlos. Archivar no basta —una
// fila archivada sigue siendo una referencia real—, así que hace falta el estado terminal Deleted.

public sealed record DeleteParameterGlobalValueCommand(Guid Id) : ICommand;

public sealed class DeleteParameterGlobalValueCommandHandler(
    IParameterGlobalValueRepository valueRepo,
    IUserContext userContext)
    : ICommandHandler<DeleteParameterGlobalValueCommand>
{
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(DeleteParameterGlobalValueCommand cmd, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var value = await valueRepo.GetByIdAsync(cmd.Id, cancellationToken);
        if (value is null) return Result.Failure(DomainErrors.Common.NotFound);

        var result = value.Delete(ActorId.Create(userContext.UserId));
        if (result.IsFailure) return result;

        await valueRepo.UpdateAsync(value, cancellationToken);
        await valueRepo.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ── ParameterTenantValue: Create ─────────────────────────────────────────────

public sealed record CreateParameterTenantValueCommand(
    Guid   DefinitionId,
    Guid   TenantId,
    string Value) : ICommand<Guid>;

public sealed class CreateParameterTenantValueCommandHandler(
    IParameterDefinitionRepository   definitionRepo,
    IParameterTenantValueRepository  valueRepo,
    IUserContext userContext)
    : ICommandHandler<CreateParameterTenantValueCommand, Guid>
{
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<Guid>> Handle(CreateParameterTenantValueCommand cmd, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result<Guid>.Failure("Authenticated user is required.");

        var definition = await definitionRepo.GetByIdAsync(cmd.DefinitionId, cancellationToken);
        if (definition is null) return Result<Guid>.Failure(DomainErrors.Common.NotFound);

        var existing = await valueRepo.GetByTenantAndDefinitionAsync(cmd.TenantId, cmd.DefinitionId, cancellationToken);
        if (existing is not null)
            return Result<Guid>.Failure("A tenant value already exists for this parameter and tenant.");

        var result = ParameterTenantValue.Create(
            Domain.Kernel.ValueObjects.TenantId.Load(cmd.TenantId),
            IdValueObject.Load(cmd.DefinitionId),
            OverrideValue.Create(cmd.Value),
            definition.DataType,
            definition.Scope,
            ActorId.Create(userContext.UserId));

        if (result.IsFailure) return Result<Guid>.Failure(result.Error);

        await valueRepo.AddAsync(result.Value, cancellationToken);
        await valueRepo.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(result.Value.Props.Id.GetValue());
    }
}

// ── ParameterTenantValue: Update ─────────────────────────────────────────────

public sealed record UpdateParameterTenantValueCommand(Guid Id, string Value) : ICommand;

public sealed class UpdateParameterTenantValueCommandHandler(
    IParameterDefinitionRepository  definitionRepo,
    IParameterTenantValueRepository valueRepo,
    IUserContext userContext)
    : ICommandHandler<UpdateParameterTenantValueCommand>
{
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(UpdateParameterTenantValueCommand cmd, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var value = await valueRepo.GetByIdAsync(cmd.Id, cancellationToken);
        if (value is null) return Result.Failure(DomainErrors.Common.NotFound);

        var definition = await definitionRepo.GetByIdAsync(value.ParameterDefinitionId.GetValue(), cancellationToken);
        var dataType   = definition?.DataType ?? ParameterDataType.String;

        var scope  = definition?.Scope ?? ParameterScope.GlobalAndTenant;
        var result = value.UpdateValue(OverrideValue.Create(cmd.Value), dataType, scope, ActorId.Create(userContext.UserId));
        if (result.IsFailure) return result;

        await valueRepo.UpdateAsync(value, cancellationToken);
        await valueRepo.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ── ParameterTenantValue: Delete (borrado LÓGICO) ────────────────────────────

public sealed record DeleteParameterTenantValueCommand(Guid Id) : ICommand;

public sealed class DeleteParameterTenantValueCommandHandler(
    IParameterTenantValueRepository valueRepo,
    IUserContext userContext)
    : ICommandHandler<DeleteParameterTenantValueCommand>
{
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(DeleteParameterTenantValueCommand cmd, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var value = await valueRepo.GetByIdAsync(cmd.Id, cancellationToken);
        if (value is null) return Result.Failure(DomainErrors.Common.NotFound);

        var result = value.Delete(ActorId.Create(userContext.UserId));
        if (result.IsFailure) return result;

        await valueRepo.UpdateAsync(value, cancellationToken);
        await valueRepo.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
