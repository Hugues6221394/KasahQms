using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Audits;

/// <summary>
/// Evidence attached to an audit finding.
/// </summary>
public class AuditEvidence : BaseEntity
{
    public Guid AuditFindingId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public Guid UploadedById { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    
    public virtual AuditFinding? AuditFinding { get; set; }
    public virtual User? UploadedBy { get; set; }
}

