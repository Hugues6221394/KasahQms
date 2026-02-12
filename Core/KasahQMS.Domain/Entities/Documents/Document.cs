using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Configuration;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Documents;

/// <summary>
/// Document entity with versioning and workflow support.
/// </summary>
public class Document : AuditableEntity
{
    public string DocumentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public int CurrentVersion { get; set; } = 1;
    public Guid? DocumentTypeId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? CurrentApproverId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public Guid? ArchivedById { get; set; }
    public string? ArchiveReason { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public Guid? TargetDepartmentId { get; set; }
    /// <summary>When set, document is sent to this specific user</summary>
    public Guid? TargetUserId { get; set; }
    public string? FilePath { get; set; }
    public string? OriginalFileName { get; set; }
    
    /// <summary>If true, this is a template document that can be used as a base for new documents</summary>
    public bool IsTemplate { get; set; }
    /// <summary>Comma-separated list of department IDs authorized to access this template</summary>
    public string? AuthorizedDepartmentIds { get; set; }
    /// <summary>Template source document ID (if this document was created from a template)</summary>
    public Guid? SourceTemplateId { get; set; }
    
    // Navigation properties
    public virtual User? CreatedBy { get; set; }
    public virtual User? TargetUser { get; set; }
    public virtual OrganizationUnit? TargetDepartment { get; set; }
    public virtual DocumentType? DocumentType { get; set; }
    public virtual DocumentCategory? Category { get; set; }
    public virtual User? CurrentApprover { get; set; }
    public virtual User? ApprovedBy { get; set; }
    public virtual ICollection<DocumentVersion>? Versions { get; set; }
    public virtual ICollection<DocumentApproval>? Approvals { get; set; }
    
    public Document() { }
    
    public static Document Create(
        Guid tenantId, 
        string title, 
        string documentNumber, 
        Guid createdById,
        string? description = null,
        Guid? documentTypeId = null,
        Guid? categoryId = null)
    {
        return new Document
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentNumber = documentNumber,
            Title = title,
            Description = description,
            DocumentTypeId = documentTypeId,
            CategoryId = categoryId,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow,
            Status = DocumentStatus.Draft,
            CurrentVersion = 1,
            Versions = new List<DocumentVersion>(),
            Approvals = new List<DocumentApproval>()
        };
    }
    
    public void UpdateTitle(string title)
    {
        if (Status == DocumentStatus.Approved)
            throw new InvalidOperationException("Cannot update approved documents. Create a new version instead.");
        Title = title;
    }
    
    public void UpdateDescription(string description)
    {
        if (Status == DocumentStatus.Approved)
            throw new InvalidOperationException("Cannot update approved documents. Create a new version instead.");
        Description = description;
    }
    
    public void UpdateContent(string content)
    {
        if (Status == DocumentStatus.Approved)
            throw new InvalidOperationException("Cannot update approved documents. Create a new version instead.");
        Content = content;
    }
    public void SetDocumentType(Guid typeId) => DocumentTypeId = typeId;
    public void SetCategory(Guid categoryId) => CategoryId = categoryId;
    
    public void Submit(Guid submittedById, Guid? approverId = null)
    {
        if (Status != DocumentStatus.Draft && Status != DocumentStatus.Rejected)
            throw new InvalidOperationException("Only draft or rejected documents can be submitted.");
        
        Status = DocumentStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        CurrentApproverId = approverId;
        LastModifiedById = submittedById;
        LastModifiedAt = DateTime.UtcNow;
        
        CreateVersionSnapshot(submittedById);
    }
    
    public void Approve(Guid approvedById, string? comments = null)
    {
        var now = DateTime.UtcNow;
        Status = DocumentStatus.Approved;
        ApprovedAt = now;
        ApprovedById = approvedById;
        CurrentApproverId = null;
        LastModifiedById = approvedById;
        LastModifiedAt = now;
        
        Approvals ??= new List<DocumentApproval>();
        Approvals.Add(new DocumentApproval
        {
            Id = Guid.NewGuid(),
            DocumentId = Id,
            ApproverId = approvedById,
            IsApproved = true,
            Comments = comments,
            ApprovedAt = now
        });
    }
    
    public void RecordPartialApproval(Guid approvedById, string? comments = null)
    {
        var now = DateTime.UtcNow;
        LastModifiedById = approvedById;
        LastModifiedAt = now;
        
        Approvals ??= new List<DocumentApproval>();
        Approvals.Add(new DocumentApproval
        {
            Id = Guid.NewGuid(),
            DocumentId = Id,
            ApproverId = approvedById,
            IsApproved = true,
            Comments = comments,
            ApprovedAt = now
        });
    }
    
    public void Reject(Guid rejectedById, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Rejection reason is mandatory.", nameof(reason));
        }

        var now = DateTime.UtcNow;
        Status = DocumentStatus.Draft; // Return to Draft as per user flow
        CurrentApproverId = null;
        LastModifiedById = rejectedById;
        LastModifiedAt = now;
        
        Approvals ??= new List<DocumentApproval>();
        Approvals.Add(new DocumentApproval
        {
            Id = Guid.NewGuid(),
            DocumentId = Id,
            ApproverId = rejectedById,
            IsApproved = false,
            Comments = reason,
            ApprovedAt = now
        });
    }
    
    public void Archive(Guid archivedById, string? reason = null)
    {
        if (Status != DocumentStatus.Approved)
            throw new InvalidOperationException("Only approved documents can be archived.");
        
        Status = DocumentStatus.Archived;
        ArchivedAt = DateTime.UtcNow;
        ArchivedById = archivedById;
        ArchiveReason = reason;
        LastModifiedById = archivedById;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    private void CreateVersionSnapshot(Guid createdById)
    {
        Versions ??= new List<DocumentVersion>();
        Versions.Add(new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = Id,
            VersionNumber = CurrentVersion,
            Content = Content,
            CreatedAt = DateTime.UtcNow,
            CreatedById = createdById
        });
    }
    
    public void IncrementVersion(Guid createdById, string? changeNotes = null)
    {
        CurrentVersion++;
        CreateVersionSnapshot(createdById);
        
        if (Versions?.LastOrDefault() is DocumentVersion lastVersion)
        {
            lastVersion.ChangeNotes = changeNotes;
        }
    }
}

/// <summary>
/// Document version entity for immutable version history.
/// </summary>
public class DocumentVersion : BaseEntity
{
    public Guid DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public string? Content { get; set; }
    public string? ChangeNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    
    // Navigation
    public virtual Document? Document { get; set; }
    public virtual User? CreatedByUser { get; set; }
    public virtual User? ApprovedByUser { get; set; }
}

/// <summary>
/// Document approval record.
/// </summary>
public class DocumentApproval : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Guid ApproverId { get; set; }
    public bool IsApproved { get; set; }
    public string? Comments { get; set; }
    public DateTime ApprovedAt { get; set; }
    
    public virtual Document? Document { get; set; }
    public virtual User? Approver { get; set; }
}

/// <summary>
/// Document type entity.
/// </summary>
public class DocumentType : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Document category entity.
/// </summary>
public class DocumentCategory : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public bool IsActive { get; set; } = true;
}
