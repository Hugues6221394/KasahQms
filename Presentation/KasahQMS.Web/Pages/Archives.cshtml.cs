using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages;

[Authorize]
public class ArchivesModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ArchivesModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public List<ArchivedTaskRow> ArchivedTasks { get; set; } = new();
    public List<ArchivedDocumentRow> ArchivedDocuments { get; set; } = new();
    public string ActiveTab { get; set; } = "tasks";

    public async Task<IActionResult> OnGetAsync(string? tab = "tasks")
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;

        if (userId == null || tenantId == null)
            return RedirectToPage("/Account/Login");

        ActiveTab = tab ?? "tasks";

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .Include(u => u.OrganizationUnit)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
            return RedirectToPage("/Account/Login");

        var roles = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var isTmdOrDeputy = roles.Any(r => r == "TMD" || r == "Deputy" || r == "Deputy Country Manager" || r == "System Admin" || r == "Admin");
        var isManager = roles.Any(r => r.Contains("Manager", StringComparison.OrdinalIgnoreCase)) || isTmdOrDeputy;

        // Load archived tasks
        var taskQuery = _dbContext.QmsTasks
            .AsNoTracking()
            .Include(t => t.AssignedTo)
            .ThenInclude(u => u.OrganizationUnit)
            .Where(t => t.TenantId == tenantId && t.Status == QmsTaskStatus.Archived);

        // Filter based on role
        if (!isTmdOrDeputy)
        {
            if (isManager && user.OrganizationUnitId.HasValue)
            {
                // Managers see archived tasks from their department
                taskQuery = taskQuery.Where(t =>
                    (t.AssignedTo != null && t.AssignedTo.OrganizationUnitId == user.OrganizationUnitId.Value) ||
                    t.AssignedToId == userId.Value ||
                    t.CreatedById == userId.Value);
            }
            else
            {
                // Staff see only their own archived tasks
                taskQuery = taskQuery.Where(t => t.AssignedToId == userId.Value || t.CreatedById == userId.Value);
            }
        }

        ArchivedTasks = await taskQuery
            .OrderByDescending(t => t.LastModifiedAt)
            .Select(t => new ArchivedTaskRow(
                t.Id,
                t.TaskNumber,
                t.Title,
                t.Priority.ToString(),
                t.AssignedTo != null ? $"{t.AssignedTo.FirstName} {t.AssignedTo.LastName}" : "—",
                t.CompletedAt.HasValue ? t.CompletedAt.Value.ToString("MMM dd, yyyy") : "—",
                t.LastModifiedAt.HasValue ? t.LastModifiedAt.Value.ToString("MMM dd, yyyy") : t.CreatedAt.ToString("MMM dd, yyyy")
            ))
            .ToListAsync();

        // Load archived documents
        var docQuery = _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.DocumentType)
            .Where(d => d.TenantId == tenantId && d.Status == DocumentStatus.Archived);

        // Similar filtering for documents
        if (!isTmdOrDeputy)
        {
            if (isManager && user.OrganizationUnitId.HasValue)
            {
                docQuery = docQuery.Where(d => d.CreatedById == userId.Value);
            }
            else
            {
                docQuery = docQuery.Where(d => d.CreatedById == userId.Value);
            }
        }

        ArchivedDocuments = await docQuery
            .OrderByDescending(d => d.LastModifiedAt)
            .Select(d => new ArchivedDocumentRow(
                d.Id,
                d.DocumentNumber,
                d.Title,
                d.DocumentType != null ? d.DocumentType.Name : "—",
                d.LastModifiedAt.HasValue ? d.LastModifiedAt.Value.ToString("MMM dd, yyyy") : d.CreatedAt.ToString("MMM dd, yyyy")
            ))
            .ToListAsync();

        return Page();
    }

    public record ArchivedTaskRow(
        Guid Id,
        string TaskNumber,
        string Title,
        string Priority,
        string AssignedTo,
        string CompletedAt,
        string ArchivedAt
    );

    public record ArchivedDocumentRow(
        Guid Id,
        string DocumentNumber,
        string Title,
        string Type,
        string ArchivedAt
    );
}
