using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

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

    public async Task OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;

        if (userId == null || tenantId == null) return;

        // 1. Get Tasks awaiting approval from subordinates
        var subordinateIds = await _hierarchyService.GetSubordinateIdsAsync(userId.Value, recursive: true);
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
                        d.Status == DocumentStatus.Submitted && 
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
