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
    public List<DocumentApprovalHistoryItem> DocumentApprovalHistory { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? HistoryFilterAction { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? HistoryFilterText { get; set; }
    [BindProperty(SupportsGet = true)]
    public DateTime? HistoryFrom { get; set; }
    [BindProperty(SupportsGet = true)]
    public DateTime? HistoryTo { get; set; }
    [BindProperty]
    public Guid HistoryEntryId { get; set; }

    public Guid? CurrentUserId { get; set; }
    public bool IsExecutiveUser { get; set; }

    public async Task OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;

        if (userId == null || tenantId == null) return;
        CurrentUserId = userId;

        var currentUser = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (currentUser == null) return;

        var isDepartmentLeader = currentUser.Roles?.Any(r =>
            r.Name.Contains("Manager") ||
            r.Name is "TMD" or "TopManagingDirector" or "Country Manager" or
                     "Deputy" or "DeputyDirector" or "Deputy Country Manager" or
                     "System Admin" or "Admin" or "SystemAdmin") == true;
        IsExecutiveUser = currentUser.Roles?.Any(r =>
            r.Name is "TMD" or "TopManagingDirector" or "Country Manager" or
                     "Deputy" or "DeputyDirector" or "Deputy Country Manager" or
                     "System Admin" or "Admin" or "SystemAdmin") == true;

        // 1. Get Tasks awaiting approval from subordinates
        var subordinateIds = await _hierarchyService.GetSubordinateUserIdsAsync(userId.Value, recursive: true);
        var subordinateIdsList = subordinateIds.ToList();

        PendingTasks = await _dbContext.QmsTasks
            .AsNoTracking()
            .Include(t => t.AssignedTo)
            .Where(t => t.TenantId == tenantId && 
                        t.Status == QmsTaskStatus.AwaitingApproval &&
                        (subordinateIdsList.Contains(t.CreatedById) || subordinateIdsList.Contains(t.AssignedToId ?? Guid.Empty) || t.ReportedToUserId == userId.Value))
            .OrderByDescending(t => t.CompletedAt)
            .Select(t => new ApprovalItem
            {
                Id = t.Id,
                Number = t.TaskNumber,
                Title = t.Title,
                SubmmitedBy = t.AssignedTo != null ? t.AssignedTo.FullName : "Unknown",
                SubmittedTo = "—",
                SubmittedAt = t.CompletedAt ?? t.LastModifiedAt ?? t.CreatedAt,
                Type = "Task"
            })
            .ToListAsync();

        // 2. Get Documents awaiting approval where current user is the approver
        var pendingDocumentsQuery = _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.CreatedBy)
            .Where(d => d.TenantId == tenantId &&
                        (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview));

        if (!IsExecutiveUser)
        {
            pendingDocumentsQuery = pendingDocumentsQuery.Where(d =>
                d.CurrentApproverId == userId.Value ||
                (d.ApproverDepartmentId.HasValue &&
                 currentUser.OrganizationUnitId.HasValue &&
                 d.ApproverDepartmentId == currentUser.OrganizationUnitId &&
                 isDepartmentLeader));
        }

        PendingDocuments = await pendingDocumentsQuery
            .OrderByDescending(d => d.SubmittedAt)
            .Select(d => new ApprovalItem
            {
                Id = d.Id,
                Number = d.DocumentNumber,
                Title = d.Title,
                SubmmitedBy = d.CreatedBy != null ? d.CreatedBy.FullName : "Unknown",
                SubmittedTo = d.CurrentApproverId.HasValue
                    ? (_dbContext.Users.Where(u => u.Id == d.CurrentApproverId.Value).Select(u => u.FullName).FirstOrDefault() ?? "Pending approval queue")
                    : (d.ApproverDepartmentId.HasValue
                        ? (_dbContext.OrganizationUnits.Where(ou => ou.Id == d.ApproverDepartmentId.Value).Select(ou => ou.Name).FirstOrDefault() ?? "Department leaders")
                        : "Pending approval queue"),
                SubmittedAt = d.SubmittedAt ?? d.CreatedAt,
                Type = "Document"
            })
            .ToListAsync();

        var hiddenHistoryEntryIds = _dbContext.UserAuditLogHistoryStates.AsNoTracking()
            .Where(s => s.TenantId == tenantId.Value
                        && s.UserId == userId.Value
                        && (s.IsArchived || s.IsDeleted))
            .Select(s => s.AuditLogEntryId);

        var historyQuery = _dbContext.AuditLogEntries.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.EntityType == "Documents" && (
                a.Action == "DOCUMENT_SUBMITTED" ||
                a.Action == "DOCUMENT_APPROVED_PARTIAL" ||
                a.Action == "DOCUMENT_APPROVED_FINAL" ||
                a.Action == "DOCUMENT_REJECTED" ||
                a.Action == "DOCUMENT_RETURNED_WITH_REVISION") &&
                !hiddenHistoryEntryIds.Contains(a.Id))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(HistoryFilterAction))
        {
            historyQuery = historyQuery.Where(a => a.Action == HistoryFilterAction);
        }
        if (HistoryFrom.HasValue)
        {
            historyQuery = historyQuery.Where(a => a.Timestamp >= HistoryFrom.Value);
        }
        if (HistoryTo.HasValue)
        {
            historyQuery = historyQuery.Where(a => a.Timestamp <= HistoryTo.Value.AddDays(1));
        }
        if (!string.IsNullOrWhiteSpace(HistoryFilterText))
        {
            historyQuery = historyQuery.Where(a =>
                (a.Description != null && a.Description.Contains(HistoryFilterText)) ||
                (a.Action != null && a.Action.Contains(HistoryFilterText)));
        }

        DocumentApprovalHistory = await historyQuery
            .OrderByDescending(a => a.Timestamp)
            .Take(100)
            .Select(a => new DocumentApprovalHistoryItem
            {
                Id = a.Id,
                DocumentId = a.EntityId,
                UserId = a.UserId,
                Action = a.Action,
                Description = a.Description ?? "",
                Timestamp = a.Timestamp
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
                SubmittedTo = "—",
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
        public string SubmittedTo { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    public class DocumentApprovalHistoryItem
    {
        public Guid Id { get; set; }
        public Guid? DocumentId { get; set; }
        public Guid? UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public async Task<IActionResult> OnPostDeleteHistoryAsync()
    {
        var tenantId = _currentUserService.TenantId;
        var userId = _currentUserService.UserId;
        if (!tenantId.HasValue || !userId.HasValue) return RedirectToPage();

        var currentUser = await _dbContext.Users.AsNoTracking().Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value && u.TenantId == tenantId.Value);
        if (currentUser == null) return RedirectToPage();

        var isExecutive = currentUser.Roles?.Any(r =>
            r.Name is "TMD" or "TopManagingDirector" or "Country Manager" or
                     "Deputy" or "DeputyDirector" or "Deputy Country Manager" or
                     "System Admin" or "Admin" or "SystemAdmin") == true;

        var entry = await _dbContext.AuditLogEntries.FirstOrDefaultAsync(a => a.Id == HistoryEntryId && a.TenantId == tenantId);
        if (entry != null)
        {
            if (!isExecutive && entry.UserId != userId.Value)
            {
                return Forbid();
            }
            var state = await _dbContext.UserAuditLogHistoryStates
                .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value
                                       && s.UserId == userId.Value
                                       && s.AuditLogEntryId == entry.Id);
            if (state == null)
            {
                state = new KasahQMS.Domain.Entities.AuditLog.UserAuditLogHistoryState
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    UserId = userId.Value,
                    AuditLogEntryId = entry.Id,
                    IsDeleted = true,
                    IsArchived = false,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.UserAuditLogHistoryStates.Add(state);
            }
            else
            {
                state.IsDeleted = true;
                state.UpdatedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync();
        }
        return RedirectToPage(new { HistoryFilterAction, HistoryFilterText, HistoryFrom, HistoryTo });
    }

    public async Task<IActionResult> OnPostArchiveHistoryAsync()
    {
        var tenantId = _currentUserService.TenantId;
        var userId = _currentUserService.UserId;
        if (!tenantId.HasValue || !userId.HasValue) return RedirectToPage();

        var currentUser = await _dbContext.Users.AsNoTracking().Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value && u.TenantId == tenantId.Value);
        if (currentUser == null) return RedirectToPage();

        var isExecutive = currentUser.Roles?.Any(r =>
            r.Name is "TMD" or "TopManagingDirector" or "Country Manager" or
                     "Deputy" or "DeputyDirector" or "Deputy Country Manager" or
                     "System Admin" or "Admin" or "SystemAdmin") == true;

        var entry = await _dbContext.AuditLogEntries.FirstOrDefaultAsync(a => a.Id == HistoryEntryId && a.TenantId == tenantId);
        if (entry != null)
        {
            if (!isExecutive && entry.UserId != userId.Value)
            {
                return Forbid();
            }
            var state = await _dbContext.UserAuditLogHistoryStates
                .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value
                                       && s.UserId == userId.Value
                                       && s.AuditLogEntryId == entry.Id);
            if (state == null)
            {
                state = new KasahQMS.Domain.Entities.AuditLog.UserAuditLogHistoryState
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    UserId = userId.Value,
                    AuditLogEntryId = entry.Id,
                    IsArchived = true,
                    IsDeleted = false,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.UserAuditLogHistoryStates.Add(state);
            }
            else
            {
                state.IsArchived = true;
                state.UpdatedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync();
        }
        return RedirectToPage(new { HistoryFilterAction, HistoryFilterText, HistoryFrom, HistoryTo });
    }
}
