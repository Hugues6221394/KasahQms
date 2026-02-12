namespace KasahQMS.Domain.Enums;

/// <summary>
/// Status of an audit.
/// </summary>
public enum AuditStatus
{
    Planned,
    InProgress,
    Completed,
    Closed,
    Cancelled
}

/// <summary>
/// Types of audits.
/// </summary>
public enum AuditType
{
    Internal,
    External,
    Compliance,
    Operational,
    Financial,
    Quality,
    Supplier
}

/// <summary>
/// Finding severity levels.
/// </summary>
public enum FindingSeverity
{
    Minor,
    Major,
    Critical,
    Observation
}

/// <summary>
/// Roles in an audit team.
/// </summary>
public enum AuditRole
{
    LeadAuditor,
    Auditor,
    Observer,
    TechnicalExpert
}

