using KasahQMS.Domain.Common;

namespace KasahQMS.Domain.Entities.Security;

/// <summary>
/// Entity defining password complexity and lifecycle rules for a tenant.
/// </summary>
public class PasswordPolicy : AuditableEntity
{
    public int MinLength { get; set; }
    public int MaxLength { get; set; }
    public bool RequireUppercase { get; set; }
    public bool RequireLowercase { get; set; }
    public bool RequireDigit { get; set; }
    public bool RequireSpecialChar { get; set; }

    /// <summary>
    /// Number of previous passwords to check to prevent reuse.
    /// </summary>
    public int PreventReuse { get; set; }

    public int MaxAgeDays { get; set; }
    public int MinAgeDays { get; set; }
    public int LockoutThreshold { get; set; }
    public int LockoutDurationMinutes { get; set; }

    public PasswordPolicy() { }

    /// <summary>
    /// Creates a default password policy with sensible defaults.
    /// </summary>
    public static PasswordPolicy CreateDefault()
    {
        return new PasswordPolicy
        {
            Id = Guid.NewGuid(),
            MinLength = 8,
            MaxLength = 128,
            RequireUppercase = true,
            RequireLowercase = true,
            RequireDigit = true,
            RequireSpecialChar = true,
            PreventReuse = 5,
            MaxAgeDays = 90,
            MinAgeDays = 1,
            LockoutThreshold = 5,
            LockoutDurationMinutes = 30,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Validates a password against this policy.
    /// </summary>
    /// <returns>A tuple indicating whether the password is valid and any validation errors.</returns>
    public (bool IsValid, List<string> Errors) Validate(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required.");
            return (false, errors);
        }

        if (password.Length < MinLength)
            errors.Add($"Password must be at least {MinLength} characters.");

        if (password.Length > MaxLength)
            errors.Add($"Password must not exceed {MaxLength} characters.");

        if (RequireUppercase && !password.Any(char.IsUpper))
            errors.Add("Password must contain at least one uppercase letter.");

        if (RequireLowercase && !password.Any(char.IsLower))
            errors.Add("Password must contain at least one lowercase letter.");

        if (RequireDigit && !password.Any(char.IsDigit))
            errors.Add("Password must contain at least one digit.");

        if (RequireSpecialChar && password.All(c => char.IsLetterOrDigit(c)))
            errors.Add("Password must contain at least one special character.");

        return (errors.Count == 0, errors);
    }
}
