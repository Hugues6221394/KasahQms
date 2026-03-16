namespace KasahQMS.Domain.Enums;

/// <summary>
/// Types of training activities.
/// </summary>
public enum TrainingType
{
    Initial,
    Refresher,
    Certification,
    OnTheJob
}

/// <summary>
/// Status of a training record.
/// </summary>
public enum TrainingStatus
{
    Scheduled,
    InProgress,
    Completed,
    Expired
}

/// <summary>
/// Competency proficiency levels.
/// </summary>
public enum CompetencyLevel
{
    Novice,
    Beginner,
    Competent,
    Proficient,
    Expert
}
