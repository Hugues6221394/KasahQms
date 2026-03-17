using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace KasahQMS.Web.Pages.Templates;

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

    [BindProperty] public Guid Id { get; set; }
    [BindProperty, Required] public string Title { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public Guid? DocumentTypeId { get; set; }
    [BindProperty] public Guid? CategoryId { get; set; }
    [BindProperty] public List<string> AuthorizedDepartmentIds { get; set; } = new();
    [BindProperty] public string? TemplateContent { get; set; }

    public List<DepartmentOption> Departments { get; set; } = new();
    public List<DocumentTypeOption> DocumentTypes { get; set; } = new();
    public List<CategoryOption> Categories { get; set; } = new();
    public bool CanEdit { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null)
            return RedirectToPage("/Account/Login");

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
            return RedirectToPage("/Account/Login");

        var roles = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        bool isTmd = roles.Any(r => r == "TMD" || r == "TopManagingDirector" || r == "Country Manager");
        bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin" or "TenantAdmin");

        CanEdit = isTmd || isAdmin;

        if (!CanEdit)
            return RedirectToPage("/Account/AccessDenied");

        var template = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && d.IsTemplate);

        if (template == null)
            return NotFound();

        Id = template.Id;
        Title = template.Title;
        Description = template.Description;
        DocumentTypeId = template.DocumentTypeId;
        CategoryId = template.CategoryId;
        TemplateContent = template.Content;
        AuthorizedDepartmentIds = string.IsNullOrWhiteSpace(template.AuthorizedDepartmentIds) 
            ? new List<string>() 
            : template.AuthorizedDepartmentIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        await LoadSelectLists(tenantId);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null)
            return RedirectToPage("/Account/Login");

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
            return RedirectToPage("/Account/Login");

        var roles = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        bool isTmd = roles.Any(r => r == "TMD" || r == "TopManagingDirector" || r == "Country Manager");
        bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin" or "TenantAdmin");

        if (!isTmd && !isAdmin)
            return RedirectToPage("/Account/AccessDenied");

        if (!ModelState.IsValid)
        {
            await LoadSelectLists(tenantId);
            return Page();
        }

        var template = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == Id && d.TenantId == tenantId && d.IsTemplate);

        if (template == null)
            return NotFound();

        template.Title = Title;
        template.Description = Description;
        template.DocumentTypeId = DocumentTypeId;
        template.CategoryId = CategoryId;
        template.Content = TemplateContent;
        template.AuthorizedDepartmentIds = AuthorizedDepartmentIds.Any() 
            ? string.Join(",", AuthorizedDepartmentIds) 
            : null;
        template.LastModifiedById = userId;
        template.LastModifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} updated template {TemplateId}", userId, template.Id);

        return RedirectToPage("./Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null)
            return RedirectToPage("/Account/Login");

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
            return RedirectToPage("/Account/Login");

        var roles = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        bool isTmd = roles.Any(r => r == "TMD" || r == "TopManagingDirector" || r == "Country Manager");
        bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin" or "TenantAdmin");

        if (!isTmd && !isAdmin)
            return RedirectToPage("/Account/AccessDenied");

        var template = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == Id && d.TenantId == tenantId && d.IsTemplate);

        if (template == null)
            return NotFound();

        // Soft delete by setting status to Archived
        template.Status = KasahQMS.Domain.Enums.DocumentStatus.Archived;
        template.LastModifiedById = userId;
        template.LastModifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} deleted template {TemplateId}", userId, template.Id);

        return RedirectToPage("./Index");
    }

    private async Task LoadSelectLists(Guid? tenantId)
    {
        Departments = await _dbContext.OrganizationUnits
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.IsActive)
            .OrderBy(o => o.Name)
            .Select(o => new DepartmentOption(o.Id.ToString(), o.Name))
            .ToListAsync();

        DocumentTypes = await _dbContext.DocumentTypes
            .AsNoTracking()
            .Where(dt => dt.TenantId == tenantId && dt.IsActive)
            .OrderBy(dt => dt.Name)
            .Select(dt => new DocumentTypeOption(dt.Id, dt.Name))
            .ToListAsync();

        Categories = await _dbContext.DocumentCategories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryOption(c.Id, c.Name))
            .ToListAsync();
    }

    public record DepartmentOption(string Id, string Name);
    public record DocumentTypeOption(Guid Id, string Name);
    public record CategoryOption(Guid Id, string Name);
}
