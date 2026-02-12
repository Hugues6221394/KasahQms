using KasahQMS.Domain.Common;

namespace KasahQMS.Domain.Entities.Configuration;

/// <summary>
/// System configuration stored in the database.
/// </summary>
public class SystemSetting : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsLocked { get; set; }

    public SystemSetting() { }

    public static SystemSetting Create(
        Guid tenantId,
        string key,
        string value,
        Guid createdById,
        string? description = null)
    {
        return new SystemSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = key,
            Value = value,
            Description = description,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow,
            IsLocked = false
        };
    }
}

