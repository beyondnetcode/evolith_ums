namespace Evolith.Ums.Application.Common.Interfaces;

public interface IPasswordHashingService
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
