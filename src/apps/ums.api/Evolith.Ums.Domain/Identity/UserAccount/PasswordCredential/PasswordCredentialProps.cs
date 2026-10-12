namespace Evolith.Ums.Domain.Identity.UserAccount.PasswordCredential;

public class PasswordCredentialProps : IProps
{
    public IdValueObject Id { get; private set; }
    public UserAccountId UserAccountId { get; private set; }
    public PasswordHash PasswordHash { get; private set; }
    public bool IsActive { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public PasswordCredentialProps(
        IdValueObject id,
        UserAccountId userAccountId,
        PasswordHash passwordHash,
        ActorId createdBy)
    {
        Id = id;
        UserAccountId = userAccountId;
        PasswordHash = passwordHash;
        IsActive = false;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public PasswordCredentialProps(
        IdValueObject id,
        UserAccountId userAccountId,
        PasswordHash passwordHash,
        bool isActive,
        AuditValueObject audit)
    {
        Id = id;
        UserAccountId = userAccountId;
        PasswordHash = passwordHash;
        IsActive = isActive;
        Audit = audit;
    }

    public PasswordCredentialProps WithIsActive(bool isActive)
    {
        var clone = (PasswordCredentialProps)MemberwiseClone();
        clone.IsActive = isActive;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}