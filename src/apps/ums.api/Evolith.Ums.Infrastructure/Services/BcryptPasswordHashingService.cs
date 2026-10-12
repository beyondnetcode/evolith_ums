namespace Evolith.Ums.Infrastructure.Services;

using Evolith.Ums.Application.Common.Interfaces;

public sealed class BcryptPasswordHashingService : IPasswordHashingService
{
    private const int WorkFactor = 12;

    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);
}
