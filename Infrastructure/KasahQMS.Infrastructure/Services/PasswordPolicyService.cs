using System.Text.Json;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Security;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// Password policy service with history-based reuse prevention.
/// </summary>
public class PasswordPolicyService : IPasswordPolicyService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<PasswordPolicyService> _logger;

    public PasswordPolicyService(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        ILogger<PasswordPolicyService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<PasswordPolicy> GetPolicyAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        var policy = await _dbContext.PasswordPolicies
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && !p.IsDeleted, cancellationToken);

        return policy ?? PasswordPolicy.CreateDefault();
    }

    public async Task<(bool IsValid, List<string> Errors)> ValidatePasswordAsync(
        Guid tenantId, string password, Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(tenantId, cancellationToken);
        var (isValid, errors) = policy.Validate(password);

        // Check password history if userId provided and reuse prevention is enabled
        if (userId.HasValue && policy.PreventReuse > 0)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);

            if (user?.PasswordHistory != null)
            {
                var history = JsonSerializer.Deserialize<List<string>>(user.PasswordHistory) ?? new List<string>();
                var recentHashes = history.Take(policy.PreventReuse);

                foreach (var hash in recentHashes)
                {
                    if (_passwordHasher.Verify(password, hash))
                    {
                        errors.Add($"Password was used recently. You cannot reuse your last {policy.PreventReuse} passwords.");
                        isValid = false;
                        break;
                    }
                }
            }
        }

        return (isValid && errors.Count == 0, errors);
    }

    public async Task AddToHistoryAsync(
        Guid userId, string passwordHash,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null) return;

        var history = string.IsNullOrEmpty(user.PasswordHistory)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(user.PasswordHistory) ?? new List<string>();

        history.Insert(0, passwordHash);

        // Keep only a reasonable number of entries
        if (history.Count > 24)
            history = history.Take(24).ToList();

        user.PasswordHistory = JsonSerializer.Serialize(history);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
