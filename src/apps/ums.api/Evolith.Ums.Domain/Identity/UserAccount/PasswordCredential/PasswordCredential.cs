namespace Evolith.Ums.Domain.Identity.UserAccount.PasswordCredential;

public sealed class PasswordCredential : Entity<PasswordCredential, PasswordCredentialProps>
{
    private PasswordCredential(PasswordCredentialProps props) : base(props)
    {
    }

    public UserAccountId UserAccountId => Props.UserAccountId;
    public PasswordHash PasswordHash => Props.PasswordHash;
    public bool IsActive => Props.IsActive;

    public PasswordCredentialId GetId() => PasswordCredentialId.Load(Props.Id.GetValue());

    public static Result<PasswordCredential> Create(
        UserAccountId userAccountId,
        PasswordHash passwordHash,
        ActorId createdBy)
    {
        if (passwordHash.IsEmpty())
        {
            return Result<PasswordCredential>.Failure(DomainErrors.UserAccount.PasswordHashRequired);
        }

        var props = new PasswordCredentialProps(IdValueObject.Create(), userAccountId, passwordHash, createdBy);
        var credential = new PasswordCredential(props);

        if (!credential.IsValid())
        {
            return Result<PasswordCredential>.Failure(credential.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<PasswordCredential>.Success(credential);
    }

    internal void ActivateInternal()
    {
        SetProps(Props.WithIsActive(true));
    }

    internal void DeactivateInternal()
    {
        SetProps(Props.WithIsActive(false));
    }
}
