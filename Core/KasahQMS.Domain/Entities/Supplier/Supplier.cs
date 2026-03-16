using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Supplier;

/// <summary>
/// Entity representing an external supplier with qualification and performance tracking.
/// </summary>
public class Supplier : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? Category { get; set; }
    public SupplierQualificationStatus QualificationStatus { get; set; }
    public DateTime? QualifiedDate { get; set; }
    public DateTime? NextAuditDate { get; set; }
    public decimal? PerformanceScore { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }

    // Navigation
    public virtual ICollection<SupplierAudit>? Audits { get; set; }

    public Supplier() { }
}

/// <summary>
/// Entity representing an audit conducted on a supplier.
/// </summary>
public class SupplierAudit : BaseEntity
{
    public Guid SupplierId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime AuditDate { get; set; }
    public Guid AuditorId { get; set; }
    public decimal Score { get; set; }
    public string? Findings { get; set; }
    public SupplierAuditStatus Status { get; set; }
    public string? CorrectiveActionsRequired { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public virtual Supplier? Supplier { get; set; }
    public virtual User? Auditor { get; set; }

    public SupplierAudit() { }
}
