using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Documents;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Persistence.Services;

/// <summary>
/// Workflow service implementation for document routing.
/// </summary>
public class WorkflowService : IWorkflowService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(
        ApplicationDbContext dbContext,
        IDocumentRepository documentRepository,
        ILogger<WorkflowService> logger)
    {
        _dbContext = dbContext;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<Guid?> GetNextApproverAsync(Guid documentId, Guid documentTypeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
            if (document == null)
            {
                return null;
            }

            // Get approval workflow for this document type
            var workflow = await GetApprovalWorkflowAsync(documentTypeId, cancellationToken);
            var approvers = workflow.ToList();

            if (!approvers.Any())
            {
                _logger.LogWarning("No approvers configured for document type {DocumentTypeId}", documentTypeId);
                return null;
            }

            // If document has no current approver, return first in workflow
            if (!document.CurrentApproverId.HasValue)
            {
                return approvers.FirstOrDefault();
            }

            // Find current approver position and return next
            var currentIndex = approvers.IndexOf(document.CurrentApproverId.Value);
            if (currentIndex < 0 || currentIndex >= approvers.Count - 1)
            {
                // No more approvers
                return null;
            }

            return approvers[currentIndex + 1];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining next approver for document {DocumentId}", documentId);
            return null;
        }
    }

    public async Task<bool> RequiresAdditionalApprovalsAsync(Guid documentId, Guid currentApproverId, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
            if (document?.DocumentTypeId == null)
            {
                return false;
            }

            var workflow = await GetApprovalWorkflowAsync(document.DocumentTypeId.Value, cancellationToken);
            var approvers = workflow.ToList();

            if (!approvers.Any())
            {
                return false;
            }

            var currentIndex = approvers.IndexOf(currentApproverId);
            return currentIndex >= 0 && currentIndex < approvers.Count - 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if additional approvals required for document {DocumentId}", documentId);
            return false;
        }
    }

    public async Task<IEnumerable<Guid>> GetApprovalWorkflowAsync(Guid documentTypeId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get approvers for this document type, ordered by approval order
            var approvers = await _dbContext.Set<DocumentTypeApprover>()
                .Where(dta => dta.DocumentTypeId == documentTypeId && dta.IsRequired)
                .OrderBy(dta => dta.ApprovalOrder)
                .Select(dta => dta.ApproverId)
                .ToListAsync(cancellationToken);

            return approvers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting approval workflow for document type {DocumentTypeId}", documentTypeId);
            return Enumerable.Empty<Guid>();
        }
    }
}

