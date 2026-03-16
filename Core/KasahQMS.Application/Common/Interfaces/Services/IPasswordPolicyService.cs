using KasahQMS.Domain.Entities.Security;

namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for password policy management and validation.
/// </summary>
public interface IPasswordPolicyService
{
    Task<PasswordPolicy> GetPolicyAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<(bool IsValid, List<string> Errors)> ValidatePasswordAsync(Guid tenantId, string password, Guid? userId = null, CancellationToken cancellationToken = default);
    Task AddToHistoryAsync(Guid userId, string passwordHash, CancellationToken cancellationToken = default);
}
