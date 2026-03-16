using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Risk;

/// <summary>
/// Entity representing a risk assessment with likelihood and impact scoring.
/// </summary>
public class RiskAssessment : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RiskNumber { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Likelihood { get; set; }
    public int Impact { get; set; }

    /// <summary>
    /// Computed risk score (Likelihood × Impact).
    /// </summary>
    public int RiskScore => Likelihood * Impact;

    public RiskStatus Status { get; set; }
    public Guid OwnerId { get; set; }
    public string? MitigationPlan { get; set; }
    public int? ResidualLikelihood { get; set; }
    public int? ResidualImpact { get; set; }
    public DateTime? ReviewDate { get; set; }

    // Navigation
    public virtual User? Owner { get; set; }
    public virtual ICollection<RiskRegisterEntry>? Entries { get; set; }

    public RiskAssessment() { }
}

/// <summary>
/// Entity representing an action item in the risk register tied to a risk assessment.
/// </summary>
public class RiskRegisterEntry : BaseEntity
{
    public Guid RiskAssessmentId { get; set; }
    public string? Action { get; set; }
    public Guid? ActionOwnerId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public virtual RiskAssessment? RiskAssessment { get; set; }
    public virtual User? ActionOwner { get; set; }

    public RiskRegisterEntry() { }
}
