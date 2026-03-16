using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Security;

/// <summary>
/// Entity for managing two-factor authentication settings for a user.
/// </summary>
public class UserTwoFactorAuth : BaseEntity
{
    public Guid UserId { get; set; }
    public string SecretKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? EnabledAt { get; set; }
    public DateTime? DisabledAt { get; set; }

    /// <summary>
    /// JSON-serialized collection of recovery codes.
    /// </summary>
    public string? RecoveryCodes { get; set; }

    public DateTime? LastUsedAt { get; set; }

    // Navigation
    public virtual User? User { get; set; }

    public UserTwoFactorAuth() { }

    /// <summary>
    /// Enables two-factor authentication.
    /// </summary>
    public void Enable()
    {
        IsEnabled = true;
        EnabledAt = DateTime.UtcNow;
        DisabledAt = null;
    }

    /// <summary>
    /// Disables two-factor authentication.
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
        DisabledAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Uses a recovery code. Returns true if the code was valid and consumed.
    /// </summary>
    public bool UseRecoveryCode(string code)
    {
        if (string.IsNullOrEmpty(RecoveryCodes))
            return false;

        var codes = System.Text.Json.JsonSerializer.Deserialize<List<string>>(RecoveryCodes);
        if (codes == null || !codes.Contains(code))
            return false;

        codes.Remove(code);
        RecoveryCodes = System.Text.Json.JsonSerializer.Serialize(codes);
        LastUsedAt = DateTime.UtcNow;
        return true;
    }
}
