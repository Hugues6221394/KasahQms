using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Models;
using KasahQMS.Application.Features.Documents.Commands;
using KasahQMS.Application.Features.Documents.Dtos;
using KasahQMS.Application.Features.Documents.Queries;
using KasahQMS.Domain.Enums;
using KasahQMS.Web.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasahQMS.Web.Controllers;

/// <summary>
/// Controller for document management operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DocumentsController> _logger;
    private readonly KasahQMS.Web.Services.IAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDocumentStateService _documentStateService;
    private readonly IAuditLoggingService _auditLoggingService;

    public DocumentsController(
        IMediator mediator, 
        ILogger<DocumentsController> logger,
        KasahQMS.Web.Services.IAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IDocumentStateService documentStateService,
        IAuditLoggingService auditLoggingService)
    {
        _mediator = mediator;
        _logger = logger;
        _authorizationService = authorizationService;
        _currentUserService = currentUserService;
        _documentStateService = documentStateService;
        _auditLoggingService = auditLoggingService;
    }

    /// <summary>
    /// Get paginated list of documents.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Authenticated")]
    [ProducesResponseType(typeof(PaginatedList<DocumentListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<DocumentListDto>>> GetDocuments(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        DocumentStatus? parsedStatus = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DocumentStatus>(status, out var s))
        {
            parsedStatus = s;
        }
        
        var query = new GetDocumentsQuery(pageNumber, pageSize, searchTerm, parsedStatus);
        var result = await _mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get document by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDto>> GetDocument(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetDocumentQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return NotFound(new { error = result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Create a new document. Only authenticated non-auditor users can create.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Authenticated")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Guid>> CreateDocument(
        [FromBody] CreateDocumentDto dto,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Unauthorized(new { error = "User not authenticated" });

        // Authorization check: Only non-auditors can create documents
        var canCreate = await _authorizationService.CanCreateDocumentAsync(userId.Value);
        if (!canCreate)
        {
            _logger.LogWarning("User {UserId} attempted to create document but lacks permission", userId);
            await _auditLoggingService.LogActionAsync("DOCUMENT_CREATE_DENIED", "Document", null, 
                $"User attempted unauthorized document creation", false);
            return Forbid();
        }

        var command = new CreateDocumentCommand(
            dto.Title,
            dto.Description,
            dto.Content,
            dto.DocumentTypeId,
            dto.CategoryId);

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            await _auditLoggingService.LogActionAsync("DOCUMENT_CREATE_FAILED", "Document", null,
                $"Error: {result.ErrorMessage}", false);
            return BadRequest(new { error = result.ErrorMessage });
        }

        // Log successful creation
        await _auditLoggingService.LogDocumentCreatedAsync(result.Value, dto.Title);

        return CreatedAtAction(nameof(GetDocument), new { id = result.Value }, result.Value);
    }

    /// <summary>
    /// Submit document for approval. Document must be in Draft state.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "Authenticated")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitDocument(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Unauthorized(new { error = "User not authenticated" });

        // Verify document is in submittable state
        var isEditable = await _documentStateService.IsEditableAsync(id);
        if (!isEditable)
        {
            _logger.LogWarning("Document {DocumentId} is not in Draft state for submission", id);
            return BadRequest(new { error = "Document must be in Draft state to submit" });
        }

        // Authorization check: Can submit this document
        var canSubmit = await _authorizationService.CanSubmitDocumentAsync(userId.Value, id);
        if (!canSubmit)
        {
            _logger.LogWarning("User {UserId} unauthorized to submit document {DocumentId}", userId, id);
            await _auditLoggingService.LogActionAsync("DOCUMENT_SUBMIT_DENIED", "Document", id,
                "User attempted unauthorized submission", false);
            return Forbid();
        }

        var command = new SubmitDocumentCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            await _auditLoggingService.LogActionAsync("DOCUMENT_SUBMIT_FAILED", "Document", id,
                $"Error: {result.ErrorMessage}", false);
            return BadRequest(new { error = result.ErrorMessage });
        }

        // Log submission and transition to Submitted state
        await _documentStateService.TransitionStateAsync(id, Domain.Enums.DocumentStatus.Submitted, userId);
        await _auditLoggingService.LogDocumentSubmittedAsync(id);

        return Ok(new { message = "Document submitted for approval." });
    }

    /// <summary>
    /// Approve a document. Only assigned approvers can approve. Document must be in Submitted state.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "Authenticated")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApproveDocument(
        Guid id,
        [FromBody] ApproveDocumentDto dto,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Unauthorized(new { error = "User not authenticated" });

        // Verify document is in approvable state
        var currentState = await _documentStateService.GetCurrentStateAsync(id);
        if (currentState != Domain.Enums.DocumentStatus.Submitted)
        {
            _logger.LogWarning("Document {DocumentId} is in {State}, cannot approve", id, currentState);
            return BadRequest(new { error = "Document must be in Submitted state to approve" });
        }

        // Authorization check: Can approve this document
        var canApprove = await _authorizationService.CanApproveDocumentAsync(userId.Value, id);
        if (!canApprove)
        {
            _logger.LogWarning("User {UserId} unauthorized to approve document {DocumentId}", userId, id);
            await _auditLoggingService.LogActionAsync("DOCUMENT_APPROVE_DENIED", "Document", id,
                "User attempted unauthorized approval", false);
            return Forbid();
        }

        var command = new ApproveDocumentCommand(id, dto.Comments);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            await _auditLoggingService.LogActionAsync("DOCUMENT_APPROVE_FAILED", "Document", id,
                $"Error: {result.ErrorMessage}", false);
            return BadRequest(new { error = result.ErrorMessage });
        }

        // Transition to Approved state and log
        await _documentStateService.TransitionStateAsync(id, Domain.Enums.DocumentStatus.Approved, userId);
        await _auditLoggingService.LogDocumentApprovedAsync(id, dto.Comments);

        return Ok(new { message = "Document approved." });
    }

    /// <summary>
    /// Reject a document. Only assigned approvers can reject. Document must be in Submitted state.
    /// Rejection reason is mandatory and logged in audit trail.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "Authenticated")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RejectDocument(
        Guid id,
        [FromBody] RejectDocumentDto dto,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Unauthorized(new { error = "User not authenticated" });

        // Rejection reason is mandatory
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { error = "Rejection reason is mandatory" });

        // Verify document is in rejectable state
        var currentState = await _documentStateService.GetCurrentStateAsync(id);
        if (currentState != Domain.Enums.DocumentStatus.Submitted)
        {
            _logger.LogWarning("Document {DocumentId} is in {State}, cannot reject", id, currentState);
            return BadRequest(new { error = "Document must be in Submitted state to reject" });
        }

        // Authorization check: Can reject this document (same as approval)
        var canReject = await _authorizationService.CanRejectDocumentAsync(userId.Value, id);
        if (!canReject)
        {
            _logger.LogWarning("User {UserId} unauthorized to reject document {DocumentId}", userId, id);
            await _auditLoggingService.LogActionAsync("DOCUMENT_REJECT_DENIED", "Document", id,
                "User attempted unauthorized rejection", false);
            return Forbid();
        }

        var command = new RejectDocumentCommand(id, dto.Reason);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            await _auditLoggingService.LogActionAsync("DOCUMENT_REJECT_FAILED", "Document", id,
                $"Error: {result.ErrorMessage}", false);
            return BadRequest(new { error = result.ErrorMessage });
        }

        // Transition back to Draft state and log rejection
        await _documentStateService.TransitionStateAsync(id, Domain.Enums.DocumentStatus.Draft, userId);
        await _auditLoggingService.LogDocumentRejectedAsync(id, dto.Reason);

        return Ok(new { message = "Document rejected. Returned to Draft for revision." });
    }
}
