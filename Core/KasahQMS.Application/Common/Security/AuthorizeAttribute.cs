namespace KasahQMS.Application.Common.Security;

/// <summary>
/// Specifies authorization requirements for a request.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class AuthorizeAttribute : Attribute
{
    /// <summary>
    /// Comma-separated list of required roles.
    /// </summary>
    public string? Roles { get; set; }

    /// <summary>
    /// Required permission (uses string-based permission names).
    /// </summary>
    public string? Permissions { get; set; }

    /// <summary>
    /// If true, user must have all specified permissions.
    /// If false, user needs any one of the specified permissions.
    /// </summary>
    public bool RequireAll { get; set; } = false;

    public AuthorizeAttribute() { }

    public AuthorizeAttribute(string permissions)
    {
        Permissions = permissions;
    }
}

/// <summary>
/// Marks a request as allowing anonymous access.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AllowAnonymousAttribute : Attribute { }
