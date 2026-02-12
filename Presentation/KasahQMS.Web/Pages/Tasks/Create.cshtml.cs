using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Features.Documents.Commands;
using KasahQMS.Application.Features.Tasks.Commands;
using KasahQMS.Domain.Entities.Tasks;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Tasks;

public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateModel> _logger;
    private readonly IWebHostEnvironment _environment;

    public CreateModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IMediator mediator,
        ILogger<CreateModel> logger,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _mediator = mediator;
        _logger = logger;
        _environment = environment;
    }

    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public Guid? AssignedToId { get; set; }
    [BindProperty] public DateTime? DueDate { get; set; }
    [BindProperty] public string Priority { get; set; } = "Medium";
    [BindProperty] public Guid? LinkedDocumentId { get; set; }
    [BindProperty] public Guid? TemplateId { get; set; }
    [BindProperty] public List<IFormFile>? Attachments { get; set; }
    [BindProperty] public List<Guid>? AssignedToUserIds { get; set; }
    [BindProperty] public Guid? AssignedToOrgUnitId { get; set; }

    public List<UserOption> Users { get; set; } = new();
    public List<DocumentOption> Documents { get; set; } = new();
    public List<TemplateOption> Templates { get; set; } = new();
    public List<OrgUnitOption> Departments { get; set; } = new();
    public bool CanAssignToDepartment { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        // Authorization check: Only managers can create tasks
        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

        if (currentUser == null)
        {
            // Not authenticated - will be redirected by [Authorize]
            return;
        }

        // Check if user is auditor
        var isAuditor = currentUser.Roles?.Any(r => r.Name == "Auditor") == true;
        if (isAuditor)
        {
            ErrorMessage = "Auditors cannot create tasks. Only managers can assign work.";
            return;
        }

        // Check if user is manager
        var isManager = currentUser.Roles?.Any(r => r.Name is "TMD" or "Deputy Country Manager" 
            or "Department Manager" or "Manager" or "System Admin" or "Admin") == true;
        if (!isManager)
        {
            ErrorMessage = "Only managers can create tasks. Contact your manager to assign work.";
            return;
        }

        await LoadLookupsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Authorization check: Only managers can create tasks
        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

        if (currentUser == null)
            return RedirectToPage("/Account/Login");

        // Auditors cannot create tasks
        if (currentUser.Roles?.Any(r => r.Name == "Auditor") == true)
        {
            ModelState.AddModelError("", "Auditors cannot create tasks. This is a read-only role.");
            await LoadLookupsAsync();
            return Page();
        }

        // Only managers can create tasks
        var isManager = currentUser.Roles?.Any(r => r.Name is "TMD" or "Deputy Country Manager" 
            or "Department Manager" or "Manager" or "System Admin" or "Admin") == true;
        if (!isManager)
        {
            ModelState.AddModelError("", "Only managers can create tasks.");
            await LoadLookupsAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Title))
            ModelState.AddModelError(nameof(Title), "Title is required.");

        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return Page();
        }

        var priority = TaskPriority.Medium;
        if (!string.IsNullOrEmpty(Priority) && Enum.TryParse<TaskPriority>(Priority, true, out var p))
            priority = p;

        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        if (userId == null || tenantId == Guid.Empty)
        {
            ErrorMessage = "User or tenant not found.";
            await LoadLookupsAsync();
            return Page();
        }

        Guid? linkedDocId = LinkedDocumentId;

        if (TemplateId.HasValue)
        {
            var template = await _dbContext.Documents.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == TemplateId.Value && d.TenantId == tenantId);
            if (template != null)
            {
                var createDoc = new CreateDocumentCommand(
                    $"{template.Title} — Task",
                    template.Description,
                    template.Content,
                    template.DocumentTypeId,
                    template.CategoryId,
                    null,
                    null,
                    null);
                var docResult = await _mediator.Send(createDoc);
                if (docResult.IsSuccess)
                    linkedDocId = docResult.Value;
            }
        }

        var cmd = new CreateTaskCommand(
            Title,
            Description,
            AssignedToId,
            DueDate,
            priority,
            linkedDocId,
            null,
            null,
            AssignedToUserIds ?? new List<Guid>(),
            AssignedToOrgUnitId);

        var result = await _mediator.Send(cmd);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to create task.";
            await LoadLookupsAsync();
            return Page();
        }

        var taskId = result.Value;

        var files = Attachments ?? new List<IFormFile>();
        if (files.Count == 0 && Request.Form.Files.Count > 0)
        {
            for (var i = 0; i < Request.Form.Files.Count; i++)
                files.Add(Request.Form.Files[i]);
        }

        if (files.Count > 0)
        {
            var root = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var year = DateTime.UtcNow.Year.ToString();
            var taskDir = Path.Combine(root, "uploads", "tasks", year);
            if (!Directory.Exists(taskDir)) Directory.CreateDirectory(taskDir);

            foreach (var file in files)
            {
                if (file == null || file.Length == 0) continue;
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext)) ext = ".bin";
                var safeName = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(taskDir, safeName);
                await using (var stream = new FileStream(fullPath, FileMode.Create))
                    await file.CopyToAsync(stream);
                var storagePath = $"/uploads/tasks/{year}/{safeName}";
                var att = TaskAttachment.Create(taskId, file.FileName, storagePath, userId.Value, file.ContentType, file.Length);
                _dbContext.TaskAttachments.Add(att);
            }

            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage("./Details", new { id = taskId });
    }

    private async Task LoadLookupsAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        if (tenantId == Guid.Empty) return;

        var user = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);
        
        var roles = user?.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var isTmdOrDeputy = roles.Any(r => r == "TMD" || r == "Deputy Country Manager" || r == "TopManagingDirector" || r == "Country Manager");
        var isSystemAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin");
        var isManager = roles.Any(r => r.Contains("Manager", StringComparison.OrdinalIgnoreCase));

        CanAssignToDepartment = isTmdOrDeputy || isSystemAdmin;

        // TMD, Deputy, or System Admin can see ALL users and departments
        // Department Managers can only see users in THEIR department
        Guid? filterOrgUnitId = null;
        if (!isTmdOrDeputy && !isSystemAdmin && isManager)
        {
            filterOrgUnitId = user?.OrganizationUnitId;
        }

        // Build user query based on role
        var userList = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Where(u => !filterOrgUnitId.HasValue || u.OrganizationUnitId == filterOrgUnitId)
            .Include(u => u.OrganizationUnit)
            .Include(u => u.Roles)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        Users = userList
            .Where(u => u.Id != _currentUserService.UserId) // Exclude self
            .Select(u => new UserOption(
                u.Id,
                $"{u.FirstName} {u.LastName}",
                u.OrganizationUnit?.Name ?? "—",
                u.Roles?.Any() == true ? string.Join(", ", u.Roles.Select(r => r.Name)) : "—")).ToList();

        Documents = await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(100)
            .Select(d => new DocumentOption(d.Id, d.DocumentNumber, d.Title))
            .ToListAsync();

        // Load templates (IsTemplate flag)
        Templates = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.IsTemplate)
            .OrderBy(d => d.Title)
            .Select(d => new TemplateOption(d.Id, d.Title))
            .ToListAsync();

        // TMD/Deputy can assign to any department
        // Managers can only see their own department (for department-level assignment)
        if (isTmdOrDeputy || isSystemAdmin)
        {
            Departments = await _dbContext.OrganizationUnits.AsNoTracking()
                .Where(o => o.TenantId == tenantId && o.IsActive)
                .OrderBy(o => o.Name)
                .Select(o => new OrgUnitOption(o.Id, o.Name))
                .ToListAsync();
        }
        else if (user?.OrganizationUnitId.HasValue == true)
        {
            // Department manager can only assign to their own department
            var dept = await _dbContext.OrganizationUnits.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == user.OrganizationUnitId.Value);
            if (dept != null)
            {
                Departments = new List<OrgUnitOption> { new OrgUnitOption(dept.Id, dept.Name) };
            }
        }
    }

    public record UserOption(Guid Id, string Name, string Department, string Roles);
    public record DocumentOption(Guid Id, string Number, string Title);
    public record TemplateOption(Guid Id, string Title);
    public record OrgUnitOption(Guid Id, string Name);
}
