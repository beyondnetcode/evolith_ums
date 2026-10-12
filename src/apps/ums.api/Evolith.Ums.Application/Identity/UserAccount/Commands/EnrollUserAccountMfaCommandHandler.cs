using Evolith.Ums.Application.Identity.UserAccount.DTOs;
using Evolith.Ums.Application.Configuration.Services;

namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed class EnrollUserAccountMfaCommandHandler : ICommandHandler<EnrollUserAccountMfaCommand, EnrollUserAccountMfaResponse>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserContext _userContext;
    private readonly IConfigurationProvider _configurationProvider;

    public EnrollUserAccountMfaCommandHandler(
        IUserAccountRepository userAccountRepository,
        IUserContext userContext,
        IConfigurationProvider configurationProvider)
    {
        _userAccountRepository = userAccountRepository;
        _userContext = userContext;
        _configurationProvider = configurationProvider;
    }

    [AuthorizationAspect(ResourceCode = "manage_users", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<EnrollUserAccountMfaResponse>> Handle(EnrollUserAccountMfaCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<EnrollUserAccountMfaResponse>.Failure("Authenticated user is required to enroll MFA.");
        }

        var method = DomainEnumerationParser.FromName<MfaMethod>(request.Method);
        if (method is null)
        {
            return Result<EnrollUserAccountMfaResponse>.Failure("MFA method is invalid.");
        }

        var userAccount = await _userAccountRepository.GetByIdAsync(request.UserAccountId, cancellationToken);
        if (userAccount is null)
        {
            return Result<EnrollUserAccountMfaResponse>.Failure("User account was not found.");
        }

        var tenantConfig = _configurationProvider.ForTenant(userAccount.TenantId.GetValue());
        if (!tenantConfig.MfaAllowedMethods.Any(allowed => allowed.Name.Equals(method.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<EnrollUserAccountMfaResponse>.Failure(
                $"MFA method '{method.Name}' is not enabled for this tenant.");
        }

        var result = userAccount.EnrollMfa(method, ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return Result<EnrollUserAccountMfaResponse>.Failure(result.Error);
        }

        await _userAccountRepository.UpdateAsync(userAccount, cancellationToken);
        await _userAccountRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        var enrollmentId = userAccount.MfaEnrollments.Single(x => x.Method == method).GetId().GetValue();
        return Result<EnrollUserAccountMfaResponse>.Success(new EnrollUserAccountMfaResponse(enrollmentId));
    }
}
