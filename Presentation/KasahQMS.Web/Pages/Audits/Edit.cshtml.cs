using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Entities.Audits;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Audits;

[Authorize]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EditModel> _logger;

    public EditModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<EditModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public string Title { get; set; } = string.Empty;

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public AuditType AuditType { get; set; }

    [BindProperty]
    public DateTime PlannedStartDate { get; set; }

    [BindProperty]
    public DateTime PlannedEndDate { get; set; }

    [BindProperty]
    public Guid? LeadAuditorId { get; set; }

    [BindProperty]
    public string? Scope { get; set; }

    [BindProperty]
    public string? Objectives { get; set; }

    public string? ErrorMessage { get; set; }
    public List<AuditorItem> Auditors { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var audit = await LoadAuditForEditAsync(Id);
        if (audit == null)
        {
            ErrorMessage = "Audit not found or you don't have permission to edit it.";
            return Page();
        }

        Title = audit.Title;
        Description = audit.Description;
        AuditType = audit.AuditType;
        PlannedStartDate = audit.PlannedStartDate;
        PlannedEndDate = audit.PlannedEndDate;
        LeadAuditorId = audit.LeadAuditorId;
        Scope = audit.Scope;
        Objectives = audit.Objectives;

        await LoadAuditorsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var audit = await LoadAuditForEditAsync(Id);
        if (audit == null)
        {
            ErrorMessage = "Audit not found or you don't have permission to edit it.";
            await LoadAuditorsAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            ModelState.AddModelError(nameof(Title), "Title is required.");
        }

        if (PlannedEndDate < PlannedStartDate)
        {
            ModelState.AddModelError(nameof(PlannedEndDate), "End date cannot be before start date.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAuditorsAsync();
            return Page();
        }

        audit.Title = Title.Trim();
        audit.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        audit.AuditType = AuditType;
        audit.PlannedStartDate = PlannedStartDate;
        audit.PlannedEndDate = PlannedEndDate;
        audit.LeadAuditorId = LeadAuditorId;
        audit.Scope = string.IsNullOrWhiteSpace(Scope) ? null : Scope.Trim();
        audit.Objectives = string.IsNullOrWhiteSpace(Objectives) ? null : Objectives.Trim();
        audit.LastModifiedById = _currentUserService.UserId;
        audit.LastModifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Audit {AuditId} edited by user {UserId}", audit.Id, _currentUserService.UserId);

        return RedirectToPage("./Details", new { id = audit.Id });
    }

    private async Task<Audit?> LoadAuditForEditAsync(Guid id)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return null;
        }

        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == currentUserId.Value);

        if (currentUser == null)
        {
            return null;
        }

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var isTmdOrDeputy = roles.Any(r =>
            r.Contains("TMD", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Top Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Deputy", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Country Manager", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Operations", StringComparison.OrdinalIgnoreCase));

        var audit = await _dbContext.Audits.FirstOrDefaultAsync(a => a.Id == id);
        if (audit == null)
        {
            return null;
        }

        return (isTmdOrDeputy || audit.CreatedById == currentUserId.Value) ? audit : null;
    }

    private async Task LoadAuditorsAsync()
    {
        var auditorRoleIds = await _dbContext.Roles
            .Where(r => r.Name == "Auditor" || r.Name == "Internal Auditor")
            .Select(r => r.Id)
            .ToListAsync();

        var auditorUserIds = await _dbContext.UserRoles
            .Where(ur => auditorRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync();

        Auditors = await _dbContext.Users
            .Where(u => auditorUserIds.Contains(u.Id))
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new AuditorItem(u.Id, u.FullName))
            .ToListAsync();
    }

    public record AuditorItem(Guid Id, string Name);
}
