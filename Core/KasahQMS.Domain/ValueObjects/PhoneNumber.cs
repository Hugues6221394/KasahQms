using System.Text.RegularExpressions;
using KasahQMS.Domain.Common;

namespace KasahQMS.Domain.ValueObjects;

/// <summary>
/// Phone number value object with validation.
/// </summary>
public sealed class PhoneNumber : ValueObject
{
    private static readonly Regex PhoneRegex = new(
        @"^\+?[1-9]\d{1,14}$",
        RegexOptions.Compiled);
    
    public string Value { get; }
    
    private PhoneNumber(string value)
    {
        Value = value;
    }
    
    public static PhoneNumber Create(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number cannot be empty.");
        
        // Remove common formatting characters
        var cleaned = Regex.Replace(phoneNumber, @"[\s\-\(\)\.]+", "");
        
        if (!PhoneRegex.IsMatch(cleaned))
            throw new ArgumentException("Invalid phone number format.");
        
        return new PhoneNumber(cleaned);
    }
    
    public static bool TryCreate(string phoneNumber, out PhoneNumber? result)
    {
        try
        {
            result = Create(phoneNumber);
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
    
    public static implicit operator string(PhoneNumber phone) => phone.Value;
}
