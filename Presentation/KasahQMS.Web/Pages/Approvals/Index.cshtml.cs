using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace KasahQMS.Web.Pages.Approvals;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHierarchyService _hierarchyService;

    public IndexModel(ApplicationDbContext dbContext, ICurrentUserService currentUserService, IHierarchyService hierarchyService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
    }

    public List<ApprovalItem> PendingTasks { get; set; } = new();
    public List<ApprovalItem> PendingDocuments { get; set; } = new();
    public List<ApprovalItem> PendingTrainings { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;

        if (userId == null || tenantId == null) return;

        // 1. Get Tasks awaiting approval from subordinates
        var subordinateIds = await _hierarchyService.GetSubordinateUserIdsAsync(userId.Value, recursive: true);
        var subordinateIdsList = subordinateIds.ToList();

        PendingTasks = await _dbContext.QmsTasks
            .AsNoTracking()
            .Include(t => t.AssignedTo)
            .Where(t => t.TenantId == tenantId && 
                        t.Status == QmsTaskStatus.AwaitingApproval &&
                        (subordinateIdsList.Contains(t.CreatedById) || subordinateIdsList.Contains(t.AssignedToId ?? Guid.Empty)))
            .OrderByDescending(t => t.CompletedAt)
            .Select(t => new ApprovalItem
            {
                Id = t.Id,
                Number = t.TaskNumber,
                Title = t.Title,
                SubmmitedBy = t.AssignedTo != null ? t.AssignedTo.FullName : "Unknown",
                SubmittedAt = t.CompletedAt ?? t.LastModifiedAt ?? t.CreatedAt,
                Type = "Task"
            })
            .ToListAsync();

        // 2. Get Documents awaiting approval where current user is the approver
        PendingDocuments = await _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.CreatedBy)
            .Where(d => d.TenantId == tenantId && 
                        (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview) && 
                        d.CurrentApproverId == userId.Value)
            .OrderByDescending(d => d.SubmittedAt)
            .Select(d => new ApprovalItem
            {
                Id = d.Id,
                Number = d.DocumentNumber,
                Title = d.Title,
                SubmmitedBy = d.CreatedBy != null ? d.CreatedBy.FullName : "Unknown",
                SubmittedAt = d.SubmittedAt ?? d.CreatedAt,
                Type = "Document"
            })
            .ToListAsync();

        var trainingCandidates = await _dbContext.TrainingRecords
            .AsNoTracking()
            .Include(t => t.User)
            .Where(t => t.TenantId == tenantId &&
                        t.CreatedById == userId.Value &&
                        t.Status == TrainingStatus.Completed)
            .OrderByDescending(t => t.CompletedDate)
            .Select(t => new
            {
                t.Id,
                Number = t.TrainingType.ToString(),
                t.Title,
                SubmittedBy = t.User != null ? t.User.FullName : "Unknown",
                SubmittedAt = t.CompletedDate ?? t.LastModifiedAt ?? t.CreatedAt,
                t.Notes
            })
            .ToListAsync();

        PendingTrainings = trainingCandidates
            .Where(t => IsPendingTrainingApproval(t.Notes))
            .Select(t => new ApprovalItem
            {
                Id = t.Id,
                Number = t.Number,
                Title = t.Title,
                SubmmitedBy = t.SubmittedBy,
                SubmittedAt = t.SubmittedAt,
                Type = "Training"
            })
            .ToList();
    }

    private static bool IsPendingTrainingApproval(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return true;
        try
        {
            using var doc = JsonDocument.Parse(notes);
            var root = doc.RootElement;
            var hasDecision = root.TryGetProperty("CreatorDecision", out var decision) &&
                              !string.IsNullOrWhiteSpace(decision.GetString());
            var isArchived = root.TryGetProperty("IsArchived", out var archived) &&
                             archived.ValueKind == JsonValueKind.True;
            return !hasDecision && !isArchived;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    public class ApprovalItem
    {
        public Guid Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string SubmmitedBy { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}
