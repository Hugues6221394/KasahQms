namespace KasahQMS.Application.Common.Interfaces;

/// <summary>
/// Service to get current authenticated user information.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? Username { get; }
    string? Email { get; }
    IEnumerable<string> Roles { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    bool IsAuthenticated { get; }
}
