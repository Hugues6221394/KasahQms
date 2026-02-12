using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Notifications;
using KasahQMS.Domain.Entities.Tasks;
using KasahQMS.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Documents.Commands;

[Authorize(Permissions = Permissions.Documents.Submit)]
public record SubmitDocumentCommand(
    Guid DocumentId,
    Guid? ApproverId = null,
    Guid? ApproverDepartmentId = null) : IRequest<Result>;

public class SubmitDocumentCommandHandler : IRequestHandler<SubmitDocumentCommand, Result>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IWorkflowService _workflowService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubmitDocumentCommandHandler> _logger;

    private readonly IUserRepository _userRepository;

    public SubmitDocumentCommandHandler(
        IDocumentRepository documentRepository,
        ITaskRepository taskRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IWorkflowService workflowService,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ILogger<SubmitDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _taskRepository = taskRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _workflowService = workflowService;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(SubmitDocumentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId == null)
            {
                return Result.Failure(Error.Unauthorized);
            }

            var document = await _documentRepository.GetByIdWithDetailsAsync(request.DocumentId, cancellationToken);
            if (document == null)
            {
                return Result.Failure(Error.NotFound);
            }

            if (document.TenantId != _currentUserService.TenantId)
            {
                return Result.Failure(Error.Forbidden);
            }

            if (document.Status != DocumentStatus.Draft && document.Status != DocumentStatus.Rejected)
            {
                return Result.Failure(Error.Conflict);
            }

            Guid? nextApproverId = request.ApproverId;
            if (nextApproverId == null && request.ApproverDepartmentId.HasValue)
            {
                var deptIds = await _userRepository.GetUserIdsInOrganizationUnitAsync(request.ApproverDepartmentId.Value, cancellationToken);
                var first = deptIds.FirstOrDefault();
                nextApproverId = first == Guid.Empty ? null : first;
            }
            if (nextApproverId == null && document.DocumentTypeId.HasValue)
            {
                nextApproverId = await _workflowService.GetNextApproverAsync(
                    document.Id, 
                    document.DocumentTypeId.Value, 
                    cancellationToken);
            }

            document.Submit(userId.Value, nextApproverId);
            
            // Update status to InReview if approver is assigned
            if (nextApproverId.HasValue)
            {
                document.Status = DocumentStatus.InReview;
            }
            
            await _documentRepository.UpdateAsync(document, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                "DOCUMENT_SUBMITTED",
                "Documents",
                document.Id,
                $"Document '{document.Title}' submitted for review",
                cancellationToken);

            if (nextApproverId.HasValue)
            {
                await _notificationService.SendAsync(
                    nextApproverId.Value,
                    "Document submitted for your approval",
                    $"Document '{document.Title}' has been submitted for your approval.",
                    NotificationType.DocumentApproval,
                    document.Id,
                    cancellationToken);
            }

            // Auto-create task for tender requisitions (as per user flow)
            if (document.Category != null && 
                document.Category.Name.Contains("Tender", StringComparison.OrdinalIgnoreCase) &&
                nextApproverId.HasValue)
            {
                try
                {
                    var tenantId = _currentUserService.TenantId!.Value;
                    var count = await _taskRepository.GetCountForYearAsync(tenantId, DateTime.UtcNow.Year, cancellationToken);
                    var taskNumber = $"TASK-{DateTime.UtcNow.Year}-{(count + 1):D5}";

                    var task = QmsTask.Create(
                        tenantId,
                        $"Review tender requisition: {document.Title}",
                        taskNumber,
                        userId.Value,
                        $"Review budget and compliance for tender requisition {document.DocumentNumber}",
                        TaskPriority.High,
                        DateTime.UtcNow.AddDays(3));

                    task.Assign(nextApproverId.Value);
                    task.LinkToDocument(document.Id);

                    await _taskRepository.AddAsync(task, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    await _notificationService.SendAsync(
                        nextApproverId.Value,
                        "Tender Review Task Assigned",
                        $"A task has been created for you to review tender requisition: {document.Title}",
                        NotificationType.TaskAssignment,
                        task.Id,
                        cancellationToken);

                    _logger.LogInformation("Auto-created task {TaskId} for tender requisition {DocumentId}", task.Id, document.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-create task for tender requisition {DocumentId}", document.Id);
                    // Don't fail the submission if task creation fails
                }
            }

            _logger.LogInformation("Document {DocumentId} submitted by user {UserId}", document.Id, userId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting document {DocumentId}", request.DocumentId);
            return Result.Failure(Error.Custom("Document.SubmitFailed", "Failed to submit document."));
        }
    }
}
