using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Audits;

/// <summary>
/// Audit entity for tracking quality audits.
/// </summary>
public class Audit : AuditableEntity
{
    public string AuditNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AuditType AuditType { get; set; }
    public AuditStatus Status { get; set; } = AuditStatus.Planned;
    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public Guid? LeadAuditorId { get; set; }
    public string? Scope { get; set; }
    public string? Objectives { get; set; }
    public string? Conclusion { get; set; }
    
    // Navigation properties
    public virtual User? LeadAuditor { get; set; }
    public virtual ICollection<AuditFinding>? Findings { get; set; }
    public virtual ICollection<AuditTeamMember>? TeamMembers { get; set; }
    
    public Audit() { }
    
    public static Audit Create(
        Guid tenantId,
        string title,
        string auditNumber,
        AuditType auditType,
        DateTime plannedStartDate,
        DateTime plannedEndDate,
        Guid createdById)
    {
        return new Audit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AuditNumber = auditNumber,
            Title = title,
            AuditType = auditType,
            PlannedStartDate = plannedStartDate,
            PlannedEndDate = plannedEndDate,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow,
            Status = AuditStatus.Planned,
            Findings = new List<AuditFinding>(),
            TeamMembers = new List<AuditTeamMember>()
        };
    }
    
    public void SetDescription(string description) => Description = description;
    public void SetScope(string scope) => Scope = scope;
    public void SetObjectives(string objectives) => Objectives = objectives;
    public void SetLeadAuditor(Guid leadAuditorId) => LeadAuditorId = leadAuditorId;
    
    public void AddTeamMember(Guid userId, AuditRole role)
    {
        TeamMembers ??= new List<AuditTeamMember>();
        TeamMembers.Add(new AuditTeamMember
        {
            Id = Guid.NewGuid(),
            AuditId = Id,
            UserId = userId,
            Role = role
        });
    }
    
    public AuditFinding AddFinding(
        string findingNumber,
        string title,
        string description,
        string findingType,
        FindingSeverity severity,
        Guid identifiedById)
    {
        Findings ??= new List<AuditFinding>();
        var finding = new AuditFinding
        {
            Id = Guid.NewGuid(),
            AuditId = Id,
            FindingNumber = findingNumber,
            Title = title,
            Description = description,
            FindingType = findingType,
            Severity = severity,
            IdentifiedById = identifiedById,
            IdentifiedAt = DateTime.UtcNow,
            Status = "Open"
        };
        Findings.Add(finding);
        return finding;
    }
    
    public void StartAudit()
    {
        Status = AuditStatus.InProgress;
        ActualStartDate = DateTime.UtcNow;
    }
    
    public void CompleteAudit(string? conclusion = null)
    {
        Status = AuditStatus.Completed;
        ActualEndDate = DateTime.UtcNow;
        Conclusion = conclusion;
    }
    
    public void CloseAudit()
    {
        Status = AuditStatus.Closed;
    }
}

/// <summary>
/// Audit finding entity.
/// </summary>
public class AuditFinding : BaseEntity
{
    public Guid AuditId { get; set; }
    public string FindingNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FindingType { get; set; } = string.Empty;
    public FindingSeverity Severity { get; set; }
    public string Status { get; set; } = "Open";
    public string? Clause { get; set; }
    public string? Evidence { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public DateTime? ResponseDueDate { get; set; }
    public string? Response { get; set; }
    public Guid? IdentifiedById { get; set; }
    public DateTime IdentifiedAt { get; set; }
    public Guid? LinkedCapaId { get; set; }
    
    // Navigation properties
    public virtual Audit? Audit { get; set; }
    public virtual User? ResponsibleUser { get; set; }
    public virtual User? IdentifiedBy { get; set; }
    
    public void SetClause(string clause) => Clause = clause;
    public void SetEvidence(string evidence) => Evidence = evidence;
    public void AssignResponsible(Guid userId) => ResponsibleUserId = userId;
    public void SetResponseDueDate(DateTime dueDate) => ResponseDueDate = dueDate;
    public void LinkCapa(Guid capaId) => LinkedCapaId = capaId;
}

/// <summary>
/// Audit team member entity.
/// </summary>
public class AuditTeamMember : BaseEntity
{
    public Guid AuditId { get; set; }
    public Guid UserId { get; set; }
    public AuditRole Role { get; set; }
    
    public virtual Audit? Audit { get; set; }
    public virtual User? User { get; set; }
}
