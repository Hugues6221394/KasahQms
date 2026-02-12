using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Documents;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Templates;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IWebHostEnvironment environment,
        ILogger<CreateModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _environment = environment;
        _logger = logger;
    }

    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public new string? Content { get; set; }
    [BindProperty] public Guid? DocumentTypeId { get; set; }
    [BindProperty] public Guid? CategoryId { get; set; }
    [BindProperty] public List<Guid> AuthorizedDepartments { get; set; } = new();
    [BindProperty] public IFormFile? TemplateFile { get; set; }

    public List<DepartmentOption> Departments { get; set; } = new();
    public List<DocumentTypeOption> DocumentTypes { get; set; } = new();
    public List<CategoryOption> Categories { get; set; } = new();
    public bool CanCreate { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _currentUserService.UserId;
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

        // Only TMD/Admin can create templates
        if (!isTmd && !isAdmin)
        {
            CanCreate = false;
            ErrorMessage = "Only TMD or System Admin can create document templates.";
            return Page();
        }

        CanCreate = true;
        await LoadLookupsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null || tenantId == Guid.Empty)
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
        {
            CanCreate = false;
            ErrorMessage = "Only TMD or System Admin can create document templates.";
            await LoadLookupsAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            ModelState.AddModelError(nameof(Title), "Title is required.");
            CanCreate = true;
            await LoadLookupsAsync();
            return Page();
        }

        try
        {
            // Generate document number
            var year = DateTime.UtcNow.Year;
            var count = await _dbContext.Documents
                .Where(d => d.TenantId == tenantId && d.DocumentNumber.StartsWith($"TPL-{year}"))
                .CountAsync();
            var documentNumber = $"TPL-{year}-{(count + 1):D4}";

            // Create template document
            var document = Document.Create(
                tenantId,
                Title,
                documentNumber,
                userId.Value,
                Description,
                DocumentTypeId,
                CategoryId
            );

            document.IsTemplate = true;
            document.Content = Content;
            document.Status = DocumentStatus.Approved; // Templates are pre-approved by TMD

            // Set authorized departments
            if (AuthorizedDepartments.Any())
            {
                document.AuthorizedDepartmentIds = string.Join(",", AuthorizedDepartments);
            }

            // Handle file upload
            if (TemplateFile != null && TemplateFile.Length > 0)
            {
                var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "templates");
                Directory.CreateDirectory(uploadsDir);
                
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(TemplateFile.FileName)}";
                var filePath = Path.Combine(uploadsDir, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await TemplateFile.CopyToAsync(stream);
                }

                document.FilePath = $"/uploads/templates/{fileName}";
                document.OriginalFileName = TemplateFile.FileName;
            }

            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "TEMPLATE_CREATED",
                "Templates",
                document.Id,
                $"Template '{Title}' created by TMD",
                CancellationToken.None);

            _logger.LogInformation("Template {DocumentId} created by user {UserId}", document.Id, userId);

            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template");
            ErrorMessage = "Failed to create template. Please try again.";
            CanCreate = true;
            await LoadLookupsAsync();
            return Page();
        }
    }

    private async Task LoadLookupsAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        Departments = await _dbContext.OrganizationUnits
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.IsActive)
            .OrderBy(o => o.Name)
            .Select(o => new DepartmentOption(o.Id, o.Name))
            .ToListAsync();

        DocumentTypes = await _dbContext.DocumentTypes
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new DocumentTypeOption(t.Id, t.Name))
            .ToListAsync();

        Categories = await _dbContext.DocumentCategories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryOption(c.Id, c.Name))
            .ToListAsync();
    }

    public record DepartmentOption(Guid Id, string Name);
    public record DocumentTypeOption(Guid Id, string Name);
    public record CategoryOption(Guid Id, string Name);
}
