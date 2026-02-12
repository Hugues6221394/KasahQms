using KasahQMS.Domain.Enums;

namespace KasahQMS.Application.Features.Documents.Dtos;

public class DocumentDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DocumentStatus Status { get; set; }
    public int CurrentVersion { get; set; }
    public Guid? DocumentTypeId { get; set; }
    public string? DocumentTypeName { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public List<DocumentVersionDto> Versions { get; set; } = new();
    public List<DocumentApprovalDto> Approvals { get; set; } = new();
}

public class DocumentVersionDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public string? Content { get; set; }
    public string? ChangeNotes { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DocumentApprovalDto
{
    public Guid Id { get; set; }
    public Guid ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public DateTime ApprovedAt { get; set; }
    public string? Comments { get; set; }
    public bool IsApproved { get; set; }
}

public class CreateDocumentDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }
    public Guid? DocumentTypeId { get; set; }
    public Guid? CategoryId { get; set; }
}

public class UpdateDocumentDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string? ChangeNotes { get; set; }
}

public class DocumentListDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public int CurrentVersion { get; set; }
    public string? DocumentTypeName { get; set; }
    public string? CategoryName { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}

public class DocumentBriefDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public int CurrentVersion { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ApproveDocumentDto
{
    public string? Comments { get; set; }
}

public class RejectDocumentDto
{
    public string Reason { get; set; } = string.Empty;
}

public class DocumentAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
}
