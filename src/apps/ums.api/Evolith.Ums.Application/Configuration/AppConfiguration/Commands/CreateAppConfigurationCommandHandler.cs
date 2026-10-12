using Evolith.Ums.Application.Configuration.AppConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.AppConfiguration.Commands;

using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration;

public sealed class CreateAppConfigurationCommandHandler : ICommandHandler<CreateAppConfigurationCommand, CreateAppConfigurationResponse>
{
    private readonly IAppConfigurationRepository _repository;
    private readonly IUserContext _userContext;
    private readonly IValueEncryptionService _encryption;

    public CreateAppConfigurationCommandHandler(
        IAppConfigurationRepository repository,
        IUserContext userContext,
        IValueEncryptionService encryption)
    {
        _repository = repository;
        _userContext = userContext;
        _encryption = encryption;
    }

    [AuthorizationAspect(ResourceCode = "manage_params", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateAppConfigurationResponse>> Handle(CreateAppConfigurationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<CreateAppConfigurationResponse>.Failure("Authenticated user is required.");
        }

        var code = Code.Create(request.Code);
        var existing = await _repository.GetByScopeAndCodeAsync(
            request.TenantId,
            request.SystemSuiteId,
            request.ModuleId,
            code.GetValue(),
            cancellationToken);

        // CFG-06: an Archived (scope, code) must not block creating a new configuration.
        if (existing is not null && existing.Props.Status.Id != ConfigStatus.Archived.Id)
        {
            return Result<CreateAppConfigurationResponse>.Failure("App configuration code already exists for the selected scope.");
        }

        // BR-2: reject if any parent scope has IsNonOverridable=true for the same code.
        var parentScopeCheck = await CheckParentNonOverridableAsync(request, code.GetValue(), cancellationToken);
        if (parentScopeCheck is not null)
            return parentScopeCheck;

        var storedValue = request.IsEncrypted ? _encryption.Encrypt(request.Value) : request.Value;

        var result = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration.Create(
            request.TenantId.HasValue ? TenantId.Load(request.TenantId.Value) : null,
            request.SystemSuiteId.HasValue ? SystemSuiteId.Load(request.SystemSuiteId.Value) : null,
            request.ModuleId.HasValue ? IdValueObject.Load(request.ModuleId.Value) : null,
            code,
            ConfigurationValue.Create(storedValue),
            Description.Create(request.Description),
            request.IsInheritable,
            request.IsEncrypted,
            ActorId.Create(_userContext.UserId),
            request.IsNonOverridable);

        if (result.IsFailure)
        {
            return Result<CreateAppConfigurationResponse>.Failure(result.Error);
        }

        await _repository.AddAsync(result.Value, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateAppConfigurationResponse>.Success(new CreateAppConfigurationResponse(result.Value.Props.Id.GetValue()));
    }

    /// <summary>
    /// Checks all parent scopes (Global → Tenant → Suite) for a non-overridable config
    /// with the same code. Returns a failure result if one is found, null otherwise.
    /// </summary>
    private async Task<Result<CreateAppConfigurationResponse>?> CheckParentNonOverridableAsync(
        CreateAppConfigurationCommand request,
        string code,
        CancellationToken cancellationToken)
    {
        // Build the list of parent scopes to check, from most general to most specific.
        // A scope is a parent if it is above the scope being created in the hierarchy.
        var parentScopes = new List<(Guid? TenantId, Guid? SuiteId, Guid? ModuleId)>();

        if (request.ModuleId.HasValue)
        {
            // Module → parents: Suite, Tenant, Global
            if (request.SystemSuiteId.HasValue)
                parentScopes.Add((request.TenantId, request.SystemSuiteId, null));
            if (request.TenantId.HasValue)
                parentScopes.Add((request.TenantId, null, null));
            parentScopes.Add((null, null, null));
        }
        else if (request.SystemSuiteId.HasValue)
        {
            // Suite → parents: Tenant, Global
            if (request.TenantId.HasValue)
                parentScopes.Add((request.TenantId, null, null));
            parentScopes.Add((null, null, null));
        }
        else if (request.TenantId.HasValue)
        {
            // Tenant → parent: Global
            parentScopes.Add((null, null, null));
        }
        // Global scope has no parents.

        foreach (var (tenantId, suiteId, moduleId) in parentScopes)
        {
            var parent = await _repository.GetByScopeAndCodeAsync(tenantId, suiteId, moduleId, code, cancellationToken);
            if (parent is not null && parent.Props.IsNonOverridable)
            {
                return Result<CreateAppConfigurationResponse>.Failure(
                    DomainErrors.Configuration.AppConfigNonOverridable);
            }
        }

        return null;
    }
}
