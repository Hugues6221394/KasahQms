using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Documents;

/// <summary>
/// Links document types to their required approvers.
/// </summary>
public class DocumentTypeApprover
{
    public Guid DocumentTypeId { get; set; }
    public Guid ApproverId { get; set; }
    public int ApprovalOrder { get; set; }
    public bool IsRequired { get; set; } = true;
    
    public virtual DocumentType? DocumentType { get; set; }
    public virtual User? Approver { get; set; }
}

