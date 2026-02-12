using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Documents;

[Authorize]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<EditModel> _logger;

    public EditModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        ILogger<EditModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [BindProperty] public Guid DocumentId { get; set; }
    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string? Content { get; set; }
    [BindProperty] public Guid? DocumentTypeId { get; set; }
    [BindProperty] public Guid? CategoryId { get; set; }
    [BindProperty] public Guid? TargetDepartmentId { get; set; }
    [BindProperty] public Guid? TargetUserId { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    public string? ErrorMessage { get; set; }

    public List<LookupItem> DocumentTypes { get; set; } = new();
    public List<LookupItem> Categories { get; set; } = new();
    public List<LookupItem> Departments { get; set; } = new();
    public List<UserOption> Users { get; set; } = new();
    public bool IsTmdOrDeputy { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null)
            return RedirectToPage("/Account/Login");

        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);

        if (document == null)
            return NotFound();

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
            return RedirectToPage("/Account/Login");

        var roles = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        
        // Check authorization
        var isAuditor = roles.Any(r => r == "Auditor");
        if (isAuditor)
        {
            CanEdit = false;
            ErrorMessage = "Auditors cannot edit documents.";
            await LoadLookupsAsync(tenantId, user);
            LoadDocument(document);
            return Page();
        }

        bool isTmd = roles.Any(r => r == "TMD" || r == "TopManagingDirector" || r == "Country Manager");
        bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin");
        bool isDeputy = roles.Any(r => r.Contains("Deputy", StringComparison.OrdinalIgnoreCase));
        IsTmdOrDeputy = isTmd || isAdmin || isDeputy;

        // Can edit if:
        // 1. User is creator and document is Draft
        // 2. User is TMD/Admin (can edit all drafts)
        bool isCreator = document.CreatedById == userId;
        bool isDraft = document.Status == DocumentStatus.Draft;

        CanEdit = (isCreator && isDraft) || (IsTmdOrDeputy && isDraft);

        if (!CanEdit)
        {
            ErrorMessage = document.Status != DocumentStatus.Draft 
                ? "Only draft documents can be edited." 
                : "You don't have permission to edit this document.";
        }

        LoadDocument(document);
        await LoadLookupsAsync(tenantId, user);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null)
            return RedirectToPage("/Account/Login");

        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == DocumentId && d.TenantId == tenantId);

        if (document == null)
            return NotFound();

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
            return RedirectToPage("/Account/Login");

        var roles = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        bool isTmd = roles.Any(r => r == "TMD" || r == "TopManagingDirector" || r == "Country Manager");
        bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin");
        bool isDeputy = roles.Any(r => r.Contains("Deputy", StringComparison.OrdinalIgnoreCase));
        IsTmdOrDeputy = isTmd || isAdmin || isDeputy;

        bool isCreator = document.CreatedById == userId;
        bool isDraft = document.Status == DocumentStatus.Draft;
        CanEdit = (isCreator && isDraft) || (IsTmdOrDeputy && isDraft);

        if (!CanEdit)
        {
            ErrorMessage = "You don't have permission to edit this document.";
            LoadDocument(document);
            await LoadLookupsAsync(tenantId, user);
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            ModelState.AddModelError(nameof(Title), "Title is required.");
            LoadDocument(document);
            await LoadLookupsAsync(tenantId, user);
            return Page();
        }

        try
        {
            document.UpdateTitle(Title);
            document.UpdateDescription(Description ?? "");
            document.UpdateContent(Content ?? "");
            document.DocumentTypeId = DocumentTypeId;
            document.CategoryId = CategoryId;
            document.TargetDepartmentId = IsTmdOrDeputy ? TargetDepartmentId : document.TargetDepartmentId;
            document.TargetUserId = IsTmdOrDeputy ? TargetUserId : document.TargetUserId;
            document.LastModifiedById = userId;
            document.LastModifiedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "DOCUMENT_UPDATED",
                "Documents",
                document.Id,
                $"Document '{Title}' updated",
                CancellationToken.None);

            _logger.LogInformation("Document {DocumentId} updated by user {UserId}", document.Id, userId);

            return RedirectToPage("./Details", new { id = document.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document {DocumentId}", DocumentId);
            ErrorMessage = "Failed to update document. Please try again.";
            LoadDocument(document);
            await LoadLookupsAsync(tenantId, user);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null)
            return RedirectToPage("/Account/Login");

        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == DocumentId && d.TenantId == tenantId);

        if (document == null)
            return NotFound();

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
            return RedirectToPage("/Account/Login");

        var roles = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        bool isTmd = roles.Any(r => r == "TMD" || r == "TopManagingDirector" || r == "Country Manager");
        bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin");
        bool isCreator = document.CreatedById == userId;
        bool isDraft = document.Status == DocumentStatus.Draft;

        // Can delete if:
        // 1. User is creator and document is Draft
        // 2. User is TMD/Admin
        bool canDelete = (isCreator && isDraft) || isTmd || isAdmin;

        if (!canDelete)
        {
            return RedirectToPage("./Details", new { id = document.Id, message = "You don't have permission to delete this document." });
        }

        try
        {
            _dbContext.Documents.Remove(document);
            await _dbContext.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "DOCUMENT_DELETED",
                "Documents",
                document.Id,
                $"Document '{document.Title}' deleted",
                CancellationToken.None);

            _logger.LogInformation("Document {DocumentId} deleted by user {UserId}", document.Id, userId);

            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId}", DocumentId);
            return RedirectToPage("./Details", new { id = document.Id, message = "Failed to delete document." });
        }
    }

    private void LoadDocument(Domain.Entities.Documents.Document document)
    {
        DocumentId = document.Id;
        DocumentNumber = document.DocumentNumber;
        Title = document.Title;
        Description = document.Description;
        Content = document.Content;
        DocumentTypeId = document.DocumentTypeId;
        CategoryId = document.CategoryId;
        TargetDepartmentId = document.TargetDepartmentId;
        TargetUserId = document.TargetUserId;
        Status = document.Status.ToString();
    }

    private async Task LoadLookupsAsync(Guid tenantId, Domain.Entities.Identity.User user)
    {
        DocumentTypes = await _dbContext.DocumentTypes.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.Name)
            .Select(t => new LookupItem(t.Id, t.Name))
            .ToListAsync();

        Categories = await _dbContext.DocumentCategories.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.Name)
            .Select(c => new LookupItem(c.Id, c.Name))
            .ToListAsync();

        Departments = await _dbContext.OrganizationUnits.AsNoTracking()
            .Where(ou => ou.TenantId == tenantId)
            .OrderBy(ou => ou.Name)
            .Select(ou => new LookupItem(ou.Id, ou.Name))
            .ToListAsync();

        Users = await _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Include(u => u.OrganizationUnit)
            .Select(u => new UserOption(u.Id, $"{u.FirstName} {u.LastName}", u.OrganizationUnit != null ? u.OrganizationUnit.Name : "—"))
            .ToListAsync();
    }

    public record LookupItem(Guid Id, string Name);
    public record UserOption(Guid Id, string Name, string Department);
}
