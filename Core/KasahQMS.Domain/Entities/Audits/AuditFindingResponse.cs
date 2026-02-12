using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Audits;

/// <summary>
/// Response to an audit finding.
/// </summary>
public class AuditFindingResponse : BaseEntity
{
    public Guid AuditFindingId { get; set; }
    public string Response { get; set; } = string.Empty;
    public string? ActionTaken { get; set; }
    public Guid RespondedById { get; set; }
    public DateTime RespondedAt { get; set; } = DateTime.UtcNow;
    public bool IsAccepted { get; set; }
    public string? ReviewComments { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    
    public virtual AuditFinding? AuditFinding { get; set; }
    public virtual User? RespondedBy { get; set; }
    public virtual User? ReviewedBy { get; set; }
}

