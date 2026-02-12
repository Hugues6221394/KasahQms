using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Features.Tasks.Commands;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Tasks;

public class EditModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;
    private readonly ILogger<EditModel> _logger;
    private readonly IWebHostEnvironment _environment;

    public EditModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IMediator mediator,
        ILogger<EditModel> logger,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _mediator = mediator;
        _logger = logger;
        _environment = environment;
    }

    public TaskEditView? TaskItem { get; set; }
    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public Guid? AssignedToId { get; set; }
    [BindProperty] public DateTime? DueDate { get; set; }
    [BindProperty] public string Priority { get; set; } = "Medium";
    [BindProperty] public List<IFormFile>? Attachments { get; set; }
    public List<UserOption> Users { get; set; } = new();
    public List<EditAttachmentInfo> ExistingAttachments { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var task = await _dbContext.QmsTasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);
        if (task == null) return NotFound();
        if (task.CreatedById != _currentUserService.UserId)
            return Forbid();
        if (task.Status == QmsTaskStatus.Completed || task.Status == QmsTaskStatus.Cancelled)
            return RedirectToPage("./Details", new { id, message = "Cannot edit completed or cancelled task.", success = false });

        TaskItem = new TaskEditView(task.Id, task.TaskNumber, task.Title, task.Description, task.AssignedToId, task.DueDate, task.Priority.ToString());
        Title = task.Title;
        Description = task.Description;
        AssignedToId = task.AssignedToId;
        DueDate = task.DueDate;
        Priority = task.Priority.ToString();
        ExistingAttachments = await _dbContext.TaskAttachments.AsNoTracking()
            .Where(a => a.TaskId == id)
            .Select(a => new EditAttachmentInfo(a.Id, a.FileName, a.StoragePath))
            .ToListAsync();
        await LoadUsersAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ModelState.AddModelError(nameof(Title), "Title is required.");
        }

        if (!ModelState.IsValid)
        {
            TaskItem = new TaskEditView(id, "", Title, Description, AssignedToId, DueDate, Priority);
            ExistingAttachments = await _dbContext.TaskAttachments.AsNoTracking()
                .Where(a => a.TaskId == id).Select(a => new EditAttachmentInfo(a.Id, a.FileName, a.StoragePath)).ToListAsync();
            await LoadUsersAsync();
            return Page();
        }

        var priority = TaskPriority.Medium;
        if (!string.IsNullOrEmpty(Priority) && Enum.TryParse<TaskPriority>(Priority, true, out var p))
            priority = p;

        var cmd = new UpdateTaskCommand(id, Title, Description, AssignedToId, DueDate, priority);
        var result = await _mediator.Send(cmd);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to update task.";
            TaskItem = new TaskEditView(id, "", Title, Description, AssignedToId, DueDate, Priority);
            ExistingAttachments = await _dbContext.TaskAttachments.AsNoTracking()
                .Where(a => a.TaskId == id).Select(a => new EditAttachmentInfo(a.Id, a.FileName, a.StoragePath)).ToListAsync();
            await LoadUsersAsync();
            return Page();
        }

        var userId = _currentUserService.UserId;
        var files = Attachments ?? new List<IFormFile>();
        if (files.Count == 0 && Request.Form.Files.Count > 0)
            for (var i = 0; i < Request.Form.Files.Count; i++) files.Add(Request.Form.Files[i]);

        if (files.Count > 0 && userId.HasValue)
        {
            var root = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var year = DateTime.UtcNow.Year.ToString();
            var taskDir = Path.Combine(root, "uploads", "tasks", year);
            if (!Directory.Exists(taskDir)) Directory.CreateDirectory(taskDir);
            foreach (var file in files)
            {
                if (file == null || file.Length == 0) continue;
                var ext = Path.GetExtension(file.FileName); if (string.IsNullOrEmpty(ext)) ext = ".bin";
                var safeName = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(taskDir, safeName);
                await using (var stream = new FileStream(fullPath, FileMode.Create))
                    await file.CopyToAsync(stream);
                var storagePath = $"/uploads/tasks/{year}/{safeName}";
                var att = Domain.Entities.Tasks.TaskAttachment.Create(id, file.FileName, storagePath, userId.Value, file.ContentType, file.Length);
                _dbContext.TaskAttachments.Add(att);
            }
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage("./Details", new { id, message = "Task updated.", success = true });
    }

    private async Task LoadUsersAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        if (tenantId == Guid.Empty) return;
        var userList = await _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Include(u => u.OrganizationUnit).Include(u => u.Roles)
            .ToListAsync();
        Users = userList.Select(u => new UserOption(
            u.Id,
            $"{u.FirstName} {u.LastName}",
            u.OrganizationUnit?.Name ?? "—",
            u.Roles?.Any() == true ? string.Join(", ", u.Roles.Select(r => r.Name)) : "—")).ToList();
    }

    public record TaskEditView(Guid Id, string Number, string Title, string? Description, Guid? AssignedToId, DateTime? DueDate, string Priority);
    public record UserOption(Guid Id, string Name, string Department, string Roles);
    public record EditAttachmentInfo(Guid Id, string FileName, string StoragePath);
}
