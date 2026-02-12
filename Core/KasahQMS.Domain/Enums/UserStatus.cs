namespace KasahQMS.Domain.Enums;

/// <summary>
/// User account status.
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// User is pending activation.
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// User account is active.
    /// </summary>
    Active = 1,
    
    /// <summary>
    /// User account is locked (too many failed attempts, etc.).
    /// </summary>
    Locked = 2,
    
    /// <summary>
    /// User account is suspended by admin.
    /// </summary>
    Suspended = 3,
    
    /// <summary>
    /// User account is deactivated.
    /// </summary>
    Inactive = 4
}
