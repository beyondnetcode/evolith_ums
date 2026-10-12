namespace Evolith.Ums.Domain.Kernel;

public static class DomainGuards
{
    public static string NormalizeCode(string value)
    {
        return value.Trim().ToUpperInvariant().Replace(' ', '_');
    }
}
