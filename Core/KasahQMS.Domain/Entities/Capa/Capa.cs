using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Capa;

/// <summary>
/// Corrective and Preventive Action entity.
/// CAPA follows a strict lifecycle: Draft → UnderInvestigation → ActionsDefined → ActionsImplemented → EffectivenessVerified → Closed
/// </summary>
public class Capa : AuditableEntity
{
    public string CapaNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CapaType CapaType { get; set; }
    public CapaPriority Priority { get; set; }
    public CapaStatus Status { get; set; } = CapaStatus.Draft;
    public Guid? OwnerId { get; set; }
    public Guid? SourceAuditId { get; set; }
    public Guid? SourceAuditFindingId { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }
    public string? ImmediateActions { get; set; }
    public string? RootCauseAnalysis { get; set; }
    public string? CorrectiveActions { get; set; }
    public string? PreventiveActions { get; set; }
    public string? ImplementationNotes { get; set; }
    public string? VerificationNotes { get; set; }
    public bool? IsEffective { get; set; }
    public Guid? VerifiedById { get; set; }
    public DateTime? VerifiedAt { get; set; }
    
    // Navigation properties
    public virtual User? Owner { get; set; }
    public virtual User? VerifiedBy { get; set; }
    public virtual ICollection<CapaAction>? Actions { get; set; }
    
    public Capa() { }
    
    public static Capa Create(
        Guid tenantId,
        string title,
        string capaNumber,
        CapaType capaType,
        CapaPriority priority,
        Guid createdById)
    {
        return new Capa
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CapaNumber = capaNumber,
            Title = title,
            CapaType = capaType,
            Priority = priority,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow,
            Status = CapaStatus.Draft,
            Actions = new List<CapaAction>()
        };
    }
    
    public void SetDescription(string description) => Description = description;
    public void LinkToAudit(Guid auditId) => SourceAuditId = auditId;
    public void LinkToAuditFinding(Guid findingId) => SourceAuditFindingId = findingId;
    public void AssignOwner(Guid ownerId) => OwnerId = ownerId;
    public void SetTargetCompletionDate(DateTime date)
    {
        // Ensure date is in UTC for PostgreSQL compatibility
        TargetCompletionDate = date.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : date.ToUniversalTime();
    }
    public void SetImmediateActions(string actions) => ImmediateActions = actions;
    public void SetRootCauseAnalysis(string analysis) => RootCauseAnalysis = analysis;
    public void SetCorrectiveActions(string actions) => CorrectiveActions = actions;
    public void SetPreventiveActions(string actions) => PreventiveActions = actions;
    public void SetImplementationNotes(string notes) => ImplementationNotes = notes;
    
    /// <summary>
    /// Checks if this CAPA can be deleted (only if not yet verified or closed)
    /// </summary>
    public bool CanBeDeleted => Status != CapaStatus.EffectivenessVerified && Status != CapaStatus.Closed;
    
    /// <summary>
    /// Gets the next valid status in the lifecycle
    /// </summary>
    public CapaStatus? GetNextStatus()
    {
        return Status switch
        {
            CapaStatus.Draft => CapaStatus.UnderInvestigation,
            CapaStatus.UnderInvestigation => CapaStatus.ActionsDefined,
            CapaStatus.ActionsDefined => CapaStatus.ActionsImplemented,
            CapaStatus.ActionsImplemented => CapaStatus.EffectivenessVerified,
            CapaStatus.EffectivenessVerified => CapaStatus.Closed,
            CapaStatus.Closed => null,
            _ => null
        };
    }
    
    /// <summary>
    /// Gets the previous valid status in the lifecycle (for rollback scenarios)
    /// </summary>
    public CapaStatus? GetPreviousStatus()
    {
        return Status switch
        {
            CapaStatus.UnderInvestigation => CapaStatus.Draft,
            CapaStatus.ActionsDefined => CapaStatus.UnderInvestigation,
            CapaStatus.ActionsImplemented => CapaStatus.ActionsDefined,
            CapaStatus.EffectivenessVerified => CapaStatus.ActionsImplemented,
            CapaStatus.Closed => null, // Cannot go back from Closed
            _ => null
        };
    }
    
    /// <summary>
    /// Checks if transitioning to the target status is valid
    /// </summary>
    public bool CanTransitionTo(CapaStatus targetStatus)
    {
        // Can only move to next status or previous (except from Closed)
        var next = GetNextStatus();
        var prev = GetPreviousStatus();
        return targetStatus == next || (targetStatus == prev && Status != CapaStatus.Closed);
    }
    
    /// <summary>
    /// Transitions CAPA to the next lifecycle state
    /// </summary>
    public bool AdvanceStatus()
    {
        var next = GetNextStatus();
        if (next == null) return false;
        
        Status = next.Value;
        LastModifiedAt = DateTime.UtcNow;
        return true;
    }
    
    public CapaAction AddAction(
        string description,
        string actionType,
        DateTime dueDate,
        Guid? assigneeId = null)
    {
        Actions ??= new List<CapaAction>();
        var action = new CapaAction
        {
            Id = Guid.NewGuid(),
            CapaId = Id,
            Description = description,
            ActionType = actionType,
            DueDate = dueDate,
            AssigneeId = assigneeId,
            CreatedAt = DateTime.UtcNow,
            IsCompleted = false
        };
        Actions.Add(action);
        return action;
    }
    
    /// <summary>
    /// Starts investigation - transitions from Draft to UnderInvestigation
    /// </summary>
    public bool StartInvestigation()
    {
        if (Status != CapaStatus.Draft) return false;
        Status = CapaStatus.UnderInvestigation;
        LastModifiedAt = DateTime.UtcNow;
        return true;
    }
    
    /// <summary>
    /// Marks actions as defined - transitions from UnderInvestigation to ActionsDefined
    /// </summary>
    public bool DefineActions()
    {
        if (Status != CapaStatus.UnderInvestigation) return false;
        Status = CapaStatus.ActionsDefined;
        LastModifiedAt = DateTime.UtcNow;
        return true;
    }
    
    /// <summary>
    /// Marks actions as implemented - transitions from ActionsDefined to ActionsImplemented
    /// </summary>
    public bool ImplementActions(string? implementationNotes = null)
    {
        if (Status != CapaStatus.ActionsDefined) return false;
        Status = CapaStatus.ActionsImplemented;
        ImplementationNotes = implementationNotes;
        LastModifiedAt = DateTime.UtcNow;
        return true;
    }
    
    /// <summary>
    /// Verifies effectiveness - transitions from ActionsImplemented to EffectivenessVerified
    /// The verifier cannot be the same person who created the CAPA
    /// </summary>
    public bool VerifyEffectiveness(Guid verifiedById, string notes, bool isEffective)
    {
        if (Status != CapaStatus.ActionsImplemented) return false;
        if (verifiedById == CreatedById) return false; // Creator cannot verify their own CAPA
        
        Status = CapaStatus.EffectivenessVerified;
        VerifiedById = verifiedById;
        VerificationNotes = notes;
        IsEffective = isEffective;
        VerifiedAt = DateTime.UtcNow;
        LastModifiedAt = DateTime.UtcNow;
        return true;
    }
    
    /// <summary>
    /// Closes the CAPA - transitions from EffectivenessVerified to Closed
    /// </summary>
    public bool Close()
    {
        if (Status != CapaStatus.EffectivenessVerified) return false;
        
        Status = CapaStatus.Closed;
        ActualCompletionDate = DateTime.UtcNow;
        LastModifiedAt = DateTime.UtcNow;
        return true;
    }
}

/// <summary>
/// CAPA action item entity.
/// </summary>
public class CapaAction : BaseEntity
{
    public Guid CapaId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public Guid? AssigneeId { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public string? CompletionNotes { get; set; }
    public string? ExpectedOutcome { get; set; }
    public string? ActualOutcome { get; set; }
    
    // Navigation properties
    public virtual Capa? Capa { get; set; }
    public virtual User? Assignee { get; set; }
    public virtual User? CompletedBy { get; set; }
    
    public void SetExpectedOutcome(string outcome) => ExpectedOutcome = outcome;
    
    public void Complete(Guid completedById, string? notes = null, string? actualOutcome = null)
    {
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
        CompletedById = completedById;
        CompletionNotes = notes;
        ActualOutcome = actualOutcome;
    }
}
