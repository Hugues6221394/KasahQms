using System.Collections.Concurrent;

namespace KasahQMS.Web.Services;

/// <summary>
/// In-memory store for password reset tokens. Tokens expire after 1 hour.
/// </summary>
public static class PasswordResetTokenStore
{
    private static readonly ConcurrentDictionary<string, (Guid UserId, DateTime Expiry)> _tokens = new();

    public static string GenerateToken(Guid userId)
    {
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
        var expiry = DateTime.UtcNow.AddHours(1);
        _tokens[token] = (userId, expiry);
        return token;
    }

    public static Guid? ValidateAndConsume(string token)
    {
        if (_tokens.TryRemove(token, out var entry))
        {
            if (entry.Expiry > DateTime.UtcNow)
                return entry.UserId;
        }
        return null;
    }
}
