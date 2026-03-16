using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Privacy;

/// <summary>
/// Entity representing a user's request to export their personal data.
/// </summary>
public class DataExportRequest : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DataExportStatus Status { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? DownloadUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }

    // Navigation
    public virtual User? User { get; set; }

    public DataExportRequest() { }
}
