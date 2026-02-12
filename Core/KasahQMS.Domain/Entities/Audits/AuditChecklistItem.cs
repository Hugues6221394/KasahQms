using KasahQMS.Domain.Common;

namespace KasahQMS.Domain.Entities.Audits;

/// <summary>
/// Audit checklist item for structured audit processes.
/// </summary>
public class AuditChecklistItem : BaseEntity
{
    public Guid AuditId { get; set; }
    public string Requirement { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int Order { get; set; }
    public bool IsChecked { get; set; }
    public string? Notes { get; set; }
    public string? Evidence { get; set; }
    public Guid? CheckedById { get; set; }
    public DateTime? CheckedAt { get; set; }
    
    public virtual Audit? Audit { get; set; }
}

