namespace KasahQMS.Domain.Enums;

/// <summary>
/// Status of a data export request.
/// </summary>
public enum DataExportStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

/// <summary>
/// Types of user consent.
/// </summary>
public enum ConsentType
{
    DataProcessing,
    Marketing,
    Analytics,
    ThirdPartySharing
}

/// <summary>
/// Action to take when a data retention policy is executed.
/// </summary>
public enum RetentionAction
{
    Archive,
    Anonymize,
    Delete
}
