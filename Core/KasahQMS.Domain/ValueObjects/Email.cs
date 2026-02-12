using System.Text.RegularExpressions;
using KasahQMS.Domain.Common;

namespace KasahQMS.Domain.ValueObjects;

/// <summary>
/// Email value object with validation.
/// </summary>
public sealed class Email : ValueObject
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    public string Value { get; }
    
    private Email(string value)
    {
        Value = value;
    }
    
    public static Email Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.");
        
        email = email.Trim().ToLowerInvariant();
        
        if (email.Length > 256)
            throw new ArgumentException("Email cannot exceed 256 characters.");
        
        if (!EmailRegex.IsMatch(email))
            throw new ArgumentException("Invalid email format.");
        
        return new Email(email);
    }
    
    public static bool TryCreate(string email, out Email? result)
    {
        try
        {
            result = Create(email);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }
    
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
    
    public override string ToString() => Value;
    
    public static implicit operator string(Email email) => email.Value;
}
