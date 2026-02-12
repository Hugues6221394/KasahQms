﻿using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Services;

/// <summary>
/// Service for enforcing document lifecycle and workflow state machine.
/// Ensures documents follow proper state transitions and immutability rules.
/// </summary>
public interface IDocumentStateService
{
    /// <summary>
    /// Validate if document can transition to target state.
    /// </summary>
    Task<(bool IsValid, string? ErrorMessage)> ValidateStateTransitionAsync(Guid documentId, DocumentStatus targetState);

    /// <summary>
    /// Transition document to new state (with validation).
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> TransitionStateAsync(Guid documentId, DocumentStatus targetState, Guid? transitionedBy = null);

    /// <summary>
    /// Check if document is in editable state.
    /// </summary>
    Task<bool> IsEditableAsync(Guid documentId);

    /// <summary>
    /// Get current document state.
    /// </summary>
    Task<DocumentStatus?> GetCurrentStateAsync(Guid documentId);

    /// <summary>
    /// Check if document is submitted for approval.
    /// </summary>
    Task<bool> IsSubmittedAsync(Guid documentId);

    /// <summary>
    /// Check if document is published/approved.
    /// </summary>
    Task<bool> IsPublishedAsync(Guid documentId);
}

public class DocumentStateService : IDocumentStateService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DocumentStateService> _logger;

    public DocumentStateService(ApplicationDbContext dbContext, ILogger<DocumentStateService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateStateTransitionAsync(Guid documentId, DocumentStatus targetState)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            return (false, "Document not found.");

        var currentState = document.Status;

        // Valid state transitions
        var validTransitions = new Dictionary<DocumentStatus, List<DocumentStatus>>
        {
            { DocumentStatus.Draft, new List<DocumentStatus> { DocumentStatus.Submitted } },
            { DocumentStatus.Submitted, new List<DocumentStatus> { DocumentStatus.Approved, DocumentStatus.Rejected, DocumentStatus.Draft } },
            { DocumentStatus.Rejected, new List<DocumentStatus> { DocumentStatus.Draft } }
        };

        if (!validTransitions.ContainsKey(currentState))
            return (false, $"Unknown current state: {currentState}");

        if (!validTransitions[currentState].Contains(targetState))
        {
            return (false, $"Invalid transition from {currentState} to {targetState}. " +
                $"Valid transitions: {string.Join(", ", validTransitions[currentState])}");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> TransitionStateAsync(Guid documentId, DocumentStatus targetState, Guid? transitionedBy = null)
    {
        var validation = await ValidateStateTransitionAsync(documentId, targetState);
        if (!validation.IsValid)
            return (false, validation.ErrorMessage);

        var document = await _dbContext.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
        if (document == null)
            return (false, "Document not found.");

        var oldState = document.Status;
        document.Status = targetState;

        try
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation(
                "Document {DocumentId} transitioned from {OldState} to {NewState}",
                documentId, oldState, targetState);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transition document {DocumentId} to {State}", documentId, targetState);
            return (false, "Failed to update document state.");
        }
    }

    public async Task<bool> IsEditableAsync(Guid documentId)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            return false;

        // Only Draft status is editable
        return document.Status == DocumentStatus.Draft;
    }

    public async Task<DocumentStatus?> GetCurrentStateAsync(Guid documentId)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId);

        return document?.Status;
    }

    public async Task<bool> IsSubmittedAsync(Guid documentId)
    {
        var status = await GetCurrentStateAsync(documentId);
        return status == DocumentStatus.Submitted;
    }

    public async Task<bool> IsPublishedAsync(Guid documentId)
    {
        var status = await GetCurrentStateAsync(documentId);
        return status == DocumentStatus.Approved;
    }
}

/// <summary>
/// DTO for document version history.
/// </summary>
public class DocumentVersionInfo
{
    public int VersionNumber { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DocumentStatus Status { get; set; }
    public string? ChangeReason { get; set; }
}
