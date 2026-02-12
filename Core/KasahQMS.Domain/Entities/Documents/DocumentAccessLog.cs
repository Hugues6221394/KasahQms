using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Documents;

/// <summary>
/// Log entry for document access tracking.
/// </summary>
public class DocumentAccessLog : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty; // View, Download, Print, etc.
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    
    public virtual Document? Document { get; set; }
    public virtual User? User { get; set; }
}

