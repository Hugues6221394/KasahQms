using KasahQMS.Domain.Common;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Privacy;

/// <summary>
/// Entity defining how long data of a specific type is retained and what action is taken upon expiry.
/// </summary>
public class DataRetentionPolicy : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public RetentionAction Action { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public DateTime? LastExecutedAt { get; set; }

    public DataRetentionPolicy() { }
}
