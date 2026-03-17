using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Tasks;

[Authorize]
public class ApprovalsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;

    public ApprovalsModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IMediator mediator)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _mediator = mediator;
    }

    public List<TaskApprovalRow> PendingTasks { get; set; } = new();
    public bool IsManager { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;

        if (userId == null || tenantId == null)
            return RedirectToPage("/Account/Login");

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .Include(u => u.OrganizationUnit)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
            return RedirectToPage("/Account/Login");

        var roles = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        IsManager = roles.Any(r => 
            r.Contains("Manager", StringComparison.OrdinalIgnoreCase) ||
            r == "TMD" || r == "Deputy" || r == "Deputy Country Manager" ||
            r == "System Admin" || r == "Admin");

        if (!IsManager)
            return RedirectToPage("/Account/AccessDenied");

        // Get tasks awaiting approval
        // Managers see tasks from their department or subordinates
        // TMD/Deputy see all tasks
        var isTmdOrDeputy = roles.Any(r => r == "TMD" || r == "Deputy" || r == "Deputy Country Manager" || r == "System Admin" || r == "Admin");
        
        var query = _dbContext.QmsTasks
            .AsNoTracking()
            .Include(t => t.AssignedTo)
            .ThenInclude(u => u.OrganizationUnit)
            .Include(t => t.CompletedBy)
            .Where(t => t.TenantId == tenantId && t.Status == QmsTaskStatus.AwaitingApproval);

        // Department managers only see tasks from their department
        if (!isTmdOrDeputy && user.OrganizationUnitId.HasValue)
        {
            query = query.Where(t => 
                t.AssignedTo != null && 
                t.AssignedTo.OrganizationUnitId == user.OrganizationUnitId.Value);
        }

        PendingTasks = await query
            .OrderBy(t => t.CompletedAt)
            .Select(t => new TaskApprovalRow(
                t.Id,
                t.TaskNumber,
                t.Title,
                t.Priority.ToString(),
                t.AssignedTo != null ? $"{t.AssignedTo.FirstName} {t.AssignedTo.LastName}" : "—",
                t.AssignedTo != null && t.AssignedTo.OrganizationUnit != null ? t.AssignedTo.OrganizationUnit.Name : "—",
                t.CompletedBy != null ? $"{t.CompletedBy.FirstName} {t.CompletedBy.LastName}" : "—",
                t.CompletedAt.HasValue ? t.CompletedAt.Value.ToString("MMM dd, yyyy HH:mm") : "—",
                t.CompletionNotes ?? ""
            ))
            .ToListAsync();

        return Page();
    }

    public record TaskApprovalRow(
        Guid Id,
        string TaskNumber,
        string Title,
        string Priority,
        string AssignedTo,
        string Department,
        string CompletedBy,
        string CompletedAt,
        string CompletionNotes
    );
}
