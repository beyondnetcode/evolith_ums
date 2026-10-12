namespace Evolith.Ums.Domain.Enums;

public class SubjectType : DomainEnumeration
{
    public static readonly SubjectType User             = new(1, nameof(User));
    public static readonly SubjectType Admin            = new(2, nameof(Admin));
    public static readonly SubjectType System           = new(3, nameof(System));
    public static readonly SubjectType BackgroundWorker = new(4, "BACKGROUND_WORKER");

    private SubjectType(int id, string name) : base(id, name) { }
}
