using System.Collections.Concurrent;

namespace KasahQMS.Web.Pages.Account;

public static class PasswordResetTokenStore
{
    private static readonly ConcurrentDictionary<string, (Guid UserId, DateTime Expiry)> _tokens = new();

    public static string CreateToken(Guid userId)
    {
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var safeToken = token.Replace("+", "-").Replace("/", "_").Replace("=", "");
        var expiry = DateTime.UtcNow.AddHours(1);
        _tokens[safeToken] = (userId, expiry);
        return safeToken;
    }

    public static (bool Valid, Guid UserId) ValidateToken(string token)
    {
        if (_tokens.TryGetValue(token, out var entry) && entry.Expiry > DateTime.UtcNow)
        {
            _tokens.TryRemove(token, out _);
            return (true, entry.UserId);
        }
        return (false, Guid.Empty);
    }
}
