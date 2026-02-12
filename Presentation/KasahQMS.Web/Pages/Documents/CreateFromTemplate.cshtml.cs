using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Entities.Documents;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Documents;

[Authorize]
public class CreateFromTemplateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateFromTemplateModel> _logger;
    private readonly IWebHostEnvironment _environment;

    public CreateFromTemplateModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<CreateFromTemplateModel> logger,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
        _environment = environment;
    }

    public TemplateInfo? Template { get; set; }
    
    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string? EditedContent { get; set; }
    [BindProperty] public Guid? TargetDepartmentId { get; set; }
    [BindProperty] public Guid? TargetUserId { get; set; }
    [BindProperty] public Guid TemplateId { get; set; }
    
    public List<DeptOption> Departments { get; set; } = new();
    public List<UserOption> Users { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public bool CanShareToDepartment { get; set; }
    public string UserRoleContext { get; set; } = "Staff";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null)
            return RedirectToPage("/Account/Login");

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .Include(u => u.OrganizationUnit)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
            return RedirectToPage("/Account/Login");

        var roles = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var userOrgUnitId = user.OrganizationUnitId?.ToString() ?? "";

        // Determine role context
        bool isTmd = roles.Any(r => r == "TMD" || r == "TopManagingDirector" || r == "Country Manager");
        bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin" or "TenantAdmin");
        bool isDeputy = roles.Any(r => r.Contains("Deputy", StringComparison.OrdinalIgnoreCase));
        bool isManager = roles.Any(r => r.Contains("Manager", StringComparison.OrdinalIgnoreCase));
        bool isAuditor = roles.Any(r => r == "Auditor");

        if (isTmd || isAdmin) UserRoleContext = "TMD";
        else if (isDeputy) UserRoleContext = "Deputy";
        else if (isManager) UserRoleContext = "Manager";
        else if (isAuditor) UserRoleContext = "Auditor";
        else UserRoleContext = "Staff";

        // Auditors cannot create documents
        if (isAuditor)
        {
            ErrorMessage = "Auditors cannot create documents from templates. This is a read-only role.";
            return Page();
        }

        // Load template
        var template = await _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && d.IsTemplate);

        if (template == null)
        {
            ErrorMessage = "Template not found or you don't have access to it.";
            return Page();
        }

        // Check if user is authorized to access this template
        if (!isTmd && !isAdmin && !string.IsNullOrEmpty(template.AuthorizedDepartmentIds))
        {
            if (!template.AuthorizedDepartmentIds.Contains(userOrgUnitId))
            {
                ErrorMessage = "You are not authorized to use this template. Contact your administrator.";
                return Page();
            }
        }

        Template = new TemplateInfo(
            template.Id,
            template.DocumentNumber,
            template.Title,
            template.Description,
            template.Content,
            template.DocumentType?.Name ?? "—",
            template.FilePath,
            template.OriginalFileName
        );

        TemplateId = template.Id;
        Title = template.Title;
        Description = template.Description;
        EditedContent = template.Content;

        // TMD/Deputy can share to departments, others only to users
        CanShareToDepartment = isTmd || isAdmin || isDeputy;

        // Load departments and users
        Departments = await _dbContext.OrganizationUnits
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.IsActive)
            .OrderBy(o => o.Name)
            .Select(o => new DeptOption(o.Id, o.Name))
            .ToListAsync();

        Users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && u.Id != userId.Value)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Include(u => u.OrganizationUnit)
            .Select(u => new UserOption(u.Id, $"{u.FirstName} {u.LastName}", u.OrganizationUnit != null ? u.OrganizationUnit.Name : "—"))
            .ToListAsync();

        _logger.LogInformation("User {UserId} ({Role}) opened template {TemplateId} for editing", userId, UserRoleContext, id);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null)
            return RedirectToPage("/Account/Login");

        if (string.IsNullOrWhiteSpace(Title))
        {
            ModelState.AddModelError(nameof(Title), "Title is required.");
        }

        if (!ModelState.IsValid)
        {
            await ReloadLookupsAsync();
            return Page();
        }

        // Load original template
        var template = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == TemplateId && d.TenantId == tenantId);

        if (template == null)
        {
            ErrorMessage = "Template not found.";
            await ReloadLookupsAsync();
            return Page();
        }

        // Generate document number
        var count = await _dbContext.Documents.CountAsync(d => d.TenantId == tenantId && !d.IsTemplate);
        var documentNumber = $"DOC-{DateTime.UtcNow.Year}-{(count + 1):D4}";

        // Create new document from template
        var newDocument = Document.Create(
            tenantId,
            Title,
            documentNumber,
            userId.Value,
            Description,
            template.DocumentTypeId,
            template.CategoryId
        );

        newDocument.Content = EditedContent;
        newDocument.SourceTemplateId = template.Id;
        newDocument.TargetDepartmentId = TargetDepartmentId;
        newDocument.TargetUserId = TargetUserId;
        newDocument.Status = DocumentStatus.Draft;

        // Copy file if template has one
        if (!string.IsNullOrEmpty(template.FilePath))
        {
            newDocument.FilePath = template.FilePath;
            newDocument.OriginalFileName = template.OriginalFileName;
        }

        _dbContext.Documents.Add(newDocument);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} created document {DocumentId} from template {TemplateId}", 
            userId, newDocument.Id, template.Id);

        return RedirectToPage("./Details", new { id = newDocument.Id, message = "Document created from template successfully!", success = true });
    }

    public async Task<IActionResult> OnPostExportAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null)
            return Unauthorized();

        var template = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == TemplateId && d.TenantId == tenantId);

        if (template == null)
            return NotFound();

        // Use edited content if available, otherwise original template content
        var content = EditedContent ?? template.Content ?? "";
        var title = string.IsNullOrWhiteSpace(Title) ? template.Title : Title;

        // Export as HTML file that can be opened locally
        var htmlContent = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>{System.Net.WebUtility.HtmlEncode(title)}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; line-height: 1.6; }}
        h1 {{ color: #333; border-bottom: 2px solid #059669; padding-bottom: 10px; }}
        .content {{ margin-top: 20px; }}
        .footer {{ margin-top: 40px; padding-top: 20px; border-top: 1px solid #ccc; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <h1>{System.Net.WebUtility.HtmlEncode(title)}</h1>
    <div class='content'>
        {content}
    </div>
    <div class='footer'>
        <p>Exported from KASAH QMS on {DateTime.Now:MMMM dd, yyyy}</p>
        <p>Template: {System.Net.WebUtility.HtmlEncode(template.DocumentNumber)}</p>
    </div>
</body>
</html>";

        var fileName = $"{title.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.html";
        var bytes = System.Text.Encoding.UTF8.GetBytes(htmlContent);

        return File(bytes, "text/html", fileName);
    }

    private async Task ReloadLookupsAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        var template = await _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.Id == TemplateId && d.TenantId == tenantId);

        if (template != null)
        {
            Template = new TemplateInfo(
                template.Id,
                template.DocumentNumber,
                template.Title,
                template.Description,
                template.Content,
                template.DocumentType?.Name ?? "—",
                template.FilePath,
                template.OriginalFileName
            );
        }

        var user = await _dbContext.Users.AsNoTracking().Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == userId);
        var roles = user?.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        bool isTmd = roles.Any(r => r == "TMD" || r == "TopManagingDirector" || r == "Country Manager");
        bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin" or "TenantAdmin");
        bool isDeputy = roles.Any(r => r.Contains("Deputy", StringComparison.OrdinalIgnoreCase));
        CanShareToDepartment = isTmd || isAdmin || isDeputy;

        Departments = await _dbContext.OrganizationUnits
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.IsActive)
            .OrderBy(o => o.Name)
            .Select(o => new DeptOption(o.Id, o.Name))
            .ToListAsync();

        Users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && u.Id != userId)
            .OrderBy(u => u.FirstName)
            .Include(u => u.OrganizationUnit)
            .Select(u => new UserOption(u.Id, $"{u.FirstName} {u.LastName}", u.OrganizationUnit != null ? u.OrganizationUnit.Name : "—"))
            .ToListAsync();
    }

    public record TemplateInfo(Guid Id, string DocumentNumber, string Title, string? Description, string? Content, string DocumentType, string? FilePath, string? OriginalFileName);
    public record DeptOption(Guid Id, string Name);
    public record UserOption(Guid Id, string Name, string Department);
}
