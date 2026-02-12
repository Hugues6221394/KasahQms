namespace KasahQMS.Domain.Common;

/// <summary>
/// Represents an error with a code and description.
/// </summary>
public record Error(string Code, string Description)
{
    // Common errors
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NotFound = new("Error.NotFound", "The requested resource was not found.");
    public static readonly Error Unauthorized = new("Error.Unauthorized", "You are not authorized to perform this action.");
    public static readonly Error Forbidden = new("Error.Forbidden", "Access to this resource is forbidden.");
    public static readonly Error Validation = new("Error.Validation", "One or more validation errors occurred.");
    public static readonly Error Conflict = new("Error.Conflict", "A conflict occurred with the current state.");
    public static readonly Error InternalError = new("Error.Internal", "An internal error occurred.");
    
    /// <summary>
    /// Creates a custom error with the specified code and description.
    /// </summary>
    public static Error Custom(string code, string description) => new(code, description);
    
    /// <summary>
    /// Implicit conversion to string for compatibility with string-based error handling.
    /// </summary>
    public static implicit operator string(Error error) => error.Description;
    
    public override string ToString() => Description;
}

