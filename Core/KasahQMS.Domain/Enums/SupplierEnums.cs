namespace KasahQMS.Domain.Enums;

/// <summary>
/// Qualification status of a supplier.
/// </summary>
public enum SupplierQualificationStatus
{
    Pending,
    Qualified,
    Conditionally,
    Disqualified,
    Suspended
}

/// <summary>
/// Status of a supplier audit.
/// </summary>
public enum SupplierAuditStatus
{
    Scheduled,
    InProgress,
    Completed
}
