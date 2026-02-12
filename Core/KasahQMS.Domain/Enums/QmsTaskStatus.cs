namespace KasahQMS.Domain.Enums;

/// <summary>
/// Task status enumeration.
/// </summary>
public enum QmsTaskStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Overdue = 3,
    Cancelled = 4,
    AwaitingApproval = 5,
    Rejected = 6
}

