using KasahQMS.Application.Common.Interfaces.Services;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// Password hasher using BCrypt.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
    }

    public bool Verify(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
