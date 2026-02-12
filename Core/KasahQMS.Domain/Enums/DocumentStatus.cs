namespace KasahQMS.Domain.Enums;

/// <summary>
/// Document lifecycle status.
/// </summary>
public enum DocumentStatus
{
    Draft = 0,
    Submitted = 1,
    InReview = 2,
    Approved = 3,
    Rejected = 4,
    Archived = 5
}
