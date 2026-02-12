using System.Collections.Generic;

namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for determining document workflow routing and approvers.
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// Determines the next approver for a document based on its type and current state.
    /// </summary>
    Task<Guid?> GetNextApproverAsync(Guid documentId, Guid documentTypeId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Determines if a document requires additional approvals after the current approver.
    /// </summary>
    Task<bool> RequiresAdditionalApprovalsAsync(Guid documentId, Guid currentApproverId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the approval workflow for a document type.
    /// </summary>
    Task<IEnumerable<Guid>> GetApprovalWorkflowAsync(Guid documentTypeId, CancellationToken cancellationToken = default);
}

