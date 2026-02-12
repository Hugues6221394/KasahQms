using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Features.Documents.Commands;
using KasahQMS.Domain.Entities.Documents;
using KasahQMS.Infrastructure.Persistence.Data;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Documents;

public class CreateModel : PageModel
{
    private readonly ILogger<CreateModel> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWebHostEnvironment _environment;
    private readonly IMediator _mediator;

    public CreateModel(
        ILogger<CreateModel> logger,
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IWebHostEnvironment environment,
        IMediator mediator)
    {
        _logger = logger;
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _environment = environment;
        _mediator = mediator;
    }

    [BindProperty]
    public string Title { get; set; } = string.Empty;

    [BindProperty]
    public string? DocumentNumber { get; set; }

    [BindProperty]
    public Guid? DocumentTypeId { get; set; }

    [BindProperty]
    public Guid? CategoryId { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public new string? Content { get; set; }


    public string? ErrorMessage { get; set; }

    public List<LookupItem> DocumentTypes { get; set; } = new();
    public List<LookupItem> Categories { get; set; } = new();
    public List<LookupItem> Departments { get; set; } = new();

    [BindProperty]
    public Guid? TargetDepartmentId { get; set; }

    [BindProperty]
    public Guid? TargetUserId { get; set; }

    [BindProperty]
    public Guid? ApproverId { get; set; }

    [BindProperty]
    public Guid? ApproverDepartmentId { get; set; }

    [BindProperty]
    public Guid? AttachFromDocumentId { get; set; }

    [BindProperty]
    public Guid? AttachFromTemplateId { get; set; }

    [BindProperty]
    public List<IFormFile>? Attachments { get; set; }

    public bool IsTmdOrDeputy { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? TemplateId { get; set; }

    public List<LookupItem> Templates { get; set; } = new();
    public List<LookupItem> ExistingDocuments { get; set; } = new();
    public List<ApproverUserOption> ApproverUsers { get; set; } = new();

    public async Task OnGetAsync()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return;

        // Authorization check: Auditors cannot create documents
        var isAuditor = currentUser.Roles?.Any(r => r.Name == "Auditor") == true;
        if (isAuditor)
        {
            TempData["ErrorMessage"] = "Auditors cannot create documents. This is a read-only role.";
            RedirectToPage("/Dashboard/Index");
            return;
        }

        IsTmdOrDeputy = currentUser.Roles != null && 
            currentUser.Roles.Any(r => r.Name == "TMD" || r.Name == "Deputy Country Manager" || r.Name == "System Admin" || r.Name == "Admin");

        await LoadLookupsAsync();

        if (TemplateId.HasValue)
        {
            var template = await _dbContext.Documents.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == TemplateId.Value);

            if (template != null)
            {
                Title = $"{template.Title} - Copy";
                Description = template.Description;
                Content = template.Content;
                DocumentTypeId = template.DocumentTypeId;
                CategoryId = template.CategoryId;
            }
        }
    }

    public async Task<IActionResult> OnPostAsync(string action)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return RedirectToPage("/Account/Login");

        // Authorization check: Auditors cannot create documents
        var isAuditor = currentUser.Roles?.Any(r => r.Name == "Auditor") == true;
        if (isAuditor)
        {
            ModelState.AddModelError("", "Auditors cannot create documents. This is a read-only role.");
            await LoadLookupsAsync();
            return Page();
        }

        IsTmdOrDeputy = currentUser.Roles != null &&
            currentUser.Roles.Any(r => r.Name == "TMD" || r.Name == "Deputy Country Manager" || r.Name == "System Admin" || r.Name == "Admin");

        if (string.IsNullOrWhiteSpace(Title))
        {
            ModelState.AddModelError(nameof(Title), "Title is required.");
        }
        if (!DocumentTypeId.HasValue)
        {
            ModelState.AddModelError(nameof(DocumentTypeId), "Document type is required.");
        }
        if (!CategoryId.HasValue)
        {
            ModelState.AddModelError(nameof(CategoryId), "Category is required.");
        }
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return Page();
        }

        var root = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var year = DateTime.UtcNow.Year;
        var uploadsDir = Path.Combine(root, "uploads", "documents", year.ToString());
        if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

        string? filePath = null;
        string? originalFileName = null;
        var filesToAttach = new List<(string path, string name, string? contentType, Guid? sourceDocId)>();

        var uploads = Attachments ?? new List<IFormFile>();
        if (uploads.Count == 0 && Request.Form.Files.Count > 0)
            for (var i = 0; i < Request.Form.Files.Count; i++) uploads.Add(Request.Form.Files[i]);

        foreach (var file in uploads)
        {
            if (file == null || file.Length == 0) continue;
            var ext = Path.GetExtension(file.FileName); if (string.IsNullOrEmpty(ext)) ext = ".bin";
            var safeName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsDir, safeName);
            await using (var stream = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(stream);
            var relPath = $"/uploads/documents/{year}/{safeName}";
            if (filePath == null) { filePath = relPath; originalFileName = file.FileName; }
            filesToAttach.Add((relPath, file.FileName, file.ContentType, null));
        }

        var tenantId = await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        foreach (var srcId in new[] { AttachFromDocumentId, AttachFromTemplateId }.Where(x => x.HasValue).Select(x => x!.Value).Distinct())
        {
            var src = await _dbContext.Documents.AsNoTracking()
                .Where(d => d.Id == srcId && d.TenantId == tenantId)
                .OrderBy(d => d.Id)
                .FirstOrDefaultAsync();
            if (src == null || string.IsNullOrEmpty(src.FilePath)) continue;
            var physPath = Path.Combine(root, src.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(physPath)) continue;
            var ext = Path.GetExtension(src.OriginalFileName ?? ".bin"); if (string.IsNullOrEmpty(ext)) ext = ".bin";
            var safeName = $"{Guid.NewGuid():N}{ext}";
            var destPath = Path.Combine(uploadsDir, safeName);
            System.IO.File.Copy(physPath, destPath);
            var relPath = $"/uploads/documents/{year}/{safeName}";
            if (filePath == null) { filePath = relPath; originalFileName = src.OriginalFileName ?? "attachment"; }
            filesToAttach.Add((relPath, src.OriginalFileName ?? "attachment", null, src.Id));
        }

        var createCmd = new CreateDocumentCommand(
            Title,
            Description,
            Content,
            DocumentTypeId,
            CategoryId,
            filePath,
            originalFileName,
            IsTmdOrDeputy ? TargetDepartmentId : null);

        var createResult = await _mediator.Send(createCmd);
        if (!createResult.IsSuccess)
        {
            ErrorMessage = createResult.ErrorMessage ?? "Failed to create document.";
            await LoadLookupsAsync();
            return Page();
        }

        var docId = createResult.Value;

        for (var i = 1; i < filesToAttach.Count; i++)
        {
            var a = filesToAttach[i];
            _dbContext.DocumentAttachments.Add(new Domain.Entities.Documents.DocumentAttachment
            {
                Id = Guid.NewGuid(),
                DocumentId = docId,
                FilePath = a.path,
                OriginalFileName = a.name,
                ContentType = a.contentType,
                SourceDocumentId = a.sourceDocId
            });
        }
        if (filesToAttach.Count > 1)
            await _dbContext.SaveChangesAsync();

        if (string.Equals(action, "submit", StringComparison.OrdinalIgnoreCase))
        {
            var submitResult = await _mediator.Send(new SubmitDocumentCommand(docId, ApproverId, ApproverDepartmentId));
            if (!submitResult.IsSuccess)
            {
                _logger.LogWarning("Document {DocumentId} created but submit failed: {Error}", docId, submitResult.ErrorMessage);
                return RedirectToPage("./Details", new { id = docId });
            }
        }

        return RedirectToPage("./Details", new { id = docId });
    }

    private async Task LoadLookupsAsync()
    {
        var tenantId = await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
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

        var userList = await _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Include(u => u.OrganizationUnit).Include(u => u.Roles)
            .ToListAsync();
        ApproverUsers = userList.Select(u => new ApproverUserOption(
            u.Id,
            $"{u.FirstName} {u.LastName}",
            u.OrganizationUnit?.Name ?? "—",
            u.Roles?.Any() == true ? string.Join(", ", u.Roles.Select(r => r.Name)) : "—")).ToList();
        AllUsers = userList.Select(u => new ApproverUserOption(
            u.Id,
            $"{u.FirstName} {u.LastName}",
            u.OrganizationUnit?.Name ?? "—",
            u.Roles?.Any() == true ? string.Join(", ", u.Roles.Select(r => r.Name)) : "—")).ToList();

        ExistingDocuments = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(200)
            .Select(d => new LookupItem(d.Id, $"{d.DocumentNumber} — {d.Title}"))
            .ToListAsync();

        // Load Templates - documents marked as IsTemplate
        var currentUser = await GetCurrentUserAsync();
        var userOrgUnitId = currentUser?.OrganizationUnitId?.ToString() ?? "";
        var isTmdOrAdmin = currentUser?.Roles != null &&
            currentUser.Roles.Any(r => r.Name == "System Admin" || r.Name == "Admin" || r.Name == "TMD" || r.Name == "Country Manager");
        
        var templateQuery = _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.IsTemplate);

        // Filter by authorized departments if not Admin/TMD
        if (!isTmdOrAdmin && !string.IsNullOrEmpty(userOrgUnitId))
        {
            templateQuery = templateQuery.Where(d => 
                string.IsNullOrEmpty(d.AuthorizedDepartmentIds) || // No restriction - all depts can access
                d.AuthorizedDepartmentIds.Contains(userOrgUnitId)); // User's dept is authorized
        }

        Templates = await templateQuery
            .OrderBy(d => d.Title)
            .Select(d => new LookupItem(d.Id, d.Title))
            .ToListAsync();
    }
    
    public List<ApproverUserOption> AllUsers { get; set; } = new();

    private async Task<string> GenerateDocumentNumberAsync(Guid tenantId)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _dbContext.Documents.CountAsync(d => d.TenantId == tenantId && d.DocumentNumber.StartsWith($"DOC-{year}"));
        return $"DOC-{year}-{(count + 1).ToString().PadLeft(3, '0')}";
    }

    private async Task<KasahQMS.Domain.Entities.Identity.User?> GetCurrentUserAsync()
    {
        if (_currentUserService.UserId.HasValue)
        {
            return await _dbContext.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId.Value);
        }

        return await _dbContext.Users
            .Include(u => u.Roles)
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public record LookupItem(Guid Id, string Name);
    public record ApproverUserOption(Guid Id, string Name, string Department, string Roles);
}

