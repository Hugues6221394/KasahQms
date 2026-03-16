using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Training;

/// <summary>
/// Entity representing a training activity assigned to a user.
/// </summary>
public class TrainingRecord : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid UserId { get; set; }
    public TrainingType TrainingType { get; set; }
    public TrainingStatus Status { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? TrainerId { get; set; }
    public int? Score { get; set; }
    public int? PassingScore { get; set; }
    public string? CertificateNumber { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public virtual User? User { get; set; }
    public virtual User? Trainer { get; set; }

    public TrainingRecord() { }
}

/// <summary>
/// Entity representing a competency assessment for a user in a specific area.
/// </summary>
public class CompetencyAssessment : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid AssessorId { get; set; }
    public string CompetencyArea { get; set; } = string.Empty;
    public CompetencyLevel Level { get; set; }
    public DateTime AssessedAt { get; set; }
    public DateTime? NextAssessmentDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }

    // Navigation
    public virtual User? User { get; set; }
    public virtual User? Assessor { get; set; }

    public CompetencyAssessment() { }
}
