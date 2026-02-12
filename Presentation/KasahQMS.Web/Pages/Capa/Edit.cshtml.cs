using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Capa;

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

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string CapaType { get; set; } = "Corrective";
    [BindProperty] public string Priority { get; set; } = "Medium";
    [BindProperty] public Guid? OwnerId { get; set; }
    [BindProperty] public DateTime? TargetCompletionDate { get; set; }
    [BindProperty] public string? ImmediateActions { get; set; }
    [BindProperty] public string? RootCauseAnalysis { get; set; }
    [BindProperty] public string? CorrectiveActions { get; set; }
    [BindProperty] public string? PreventiveActions { get; set; }
    [BindProperty] public string? ImplementationNotes { get; set; }

    public string CapaNumber { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public string? NextStatus { get; set; }
    public bool CanAdvanceStatus { get; set; }
    public bool CanDelete { get; set; }
    public List<UserOption> Users { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public bool CanEdit { get; set; }
    public string UserEditPermission { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return RedirectToPage("/Account/Login");

        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (currentUser == null)
            return RedirectToPage("/Account/Login");

        var capa = await _dbContext.Capas
            .Include(c => c.Owner)
            .FirstOrDefaultAsync(c => c.Id == Id);

        if (capa == null)
        {
            return RedirectToPage("./Index");
        }

        // Determine edit permissions based on roles
        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var (canEdit, permission) = CheckEditPermission(roles, capa.CreatedById, userId.Value);
        
        CanEdit = canEdit;
        UserEditPermission = permission;

        if (!CanEdit)
        {
            ErrorMessage = permission;
            return Page();
        }

        // Load CAPA data
        CapaNumber = capa.CapaNumber;
        Title = capa.Title;
        Description = capa.Description;
        CapaType = capa.CapaType.ToString();
        Priority = capa.Priority.ToString();
        CurrentStatus = capa.Status.ToString();
        OwnerId = capa.OwnerId;
        TargetCompletionDate = capa.TargetCompletionDate;
        ImmediateActions = capa.ImmediateActions;
        RootCauseAnalysis = capa.RootCauseAnalysis;
        CorrectiveActions = capa.CorrectiveActions;
        PreventiveActions = capa.PreventiveActions;
        ImplementationNotes = capa.ImplementationNotes;

        // Check status advancement
        var nextStatus = capa.GetNextStatus();
        NextStatus = nextStatus?.ToString();
        CanAdvanceStatus = nextStatus != null;
        
        // Check delete permission
        CanDelete = capa.CanBeDeleted && CheckDeletePermission(roles, capa.CreatedById, userId.Value);

        await LoadLookupsAsync();
        return Page();
    }

    private (bool CanEdit, string Message) CheckEditPermission(List<string> roles, Guid capaCreatedById, Guid currentUserId)
    {
        // System Admin can edit any CAPA
        if (roles.Any(r => r is "System Admin" or "SystemAdmin" or "Admin" or "TenantAdmin"))
        {
            return (true, "System Admin - can edit any CAPA");
        }

        // TMD can edit any CAPA
        if (roles.Any(r => r is "TMD" or "TopManagingDirector" or "Country Manager"))
        {
            return (true, "TMD - can edit any CAPA");
        }

        // Deputy can edit CAPAs within operational scope (for now, treat as any)
        if (roles.Any(r => r is "Deputy" or "DeputyDirector" or "Deputy Country Manager"))
        {
            return (true, "Deputy - can edit CAPAs in operational scope");
        }

        // Department Manager can only edit their own CAPAs
        if (roles.Any(r => r.Contains("Manager")))
        {
            if (capaCreatedById == currentUserId)
            {
                return (true, "Department Manager - can edit own CAPA");
            }
            return (false, "Department Managers can only edit CAPAs they created.");
        }

        // Junior Staff and Auditors cannot edit
        if (roles.Any(r => r is "Auditor" or "Internal Auditor"))
        {
            return (false, "Auditors cannot edit CAPAs. This is a read-only role.");
        }

        return (false, "You do not have permission to edit CAPAs.");
    }

    private bool CheckDeletePermission(List<string> roles, Guid capaCreatedById, Guid currentUserId)
    {
        // System Admin can delete any CAPA (if not verified/closed)
        if (roles.Any(r => r is "System Admin" or "SystemAdmin" or "Admin" or "TenantAdmin"))
            return true;

        // TMD can delete any CAPA (if not verified/closed)
        if (roles.Any(r => r is "TMD" or "TopManagingDirector" or "Country Manager"))
            return true;

        // Department Manager can only delete their own CAPAs
        if (roles.Any(r => r.Contains("Manager")))
            return capaCreatedById == currentUserId;

        return false;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return RedirectToPage("/Account/Login");

        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (currentUser == null)
            return RedirectToPage("/Account/Login");

        var capa = await _dbContext.Capas.FirstOrDefaultAsync(c => c.Id == Id);
        if (capa == null)
        {
            return RedirectToPage("./Index");
        }

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var (canEdit, permission) = CheckEditPermission(roles, capa.CreatedById, userId.Value);
        
        if (!canEdit)
        {
            ErrorMessage = permission;
            CanEdit = false;
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            ModelState.AddModelError(nameof(Title), "Title is required.");
        }

        CapaNumber = capa.CapaNumber;
        CurrentStatus = capa.Status.ToString();

        if (!ModelState.IsValid)
        {
            CanEdit = true;
            await LoadLookupsAsync();
            return Page();
        }

        try
        {
            var changes = new List<string>();
            
            // Track changes for audit log
            if (capa.Title != Title) changes.Add($"Title: '{capa.Title}' → '{Title}'");
            if (capa.Description != Description) changes.Add("Description updated");
            if (capa.ImmediateActions != ImmediateActions) changes.Add("Immediate Actions updated");
            if (capa.RootCauseAnalysis != RootCauseAnalysis) changes.Add("Root Cause Analysis updated");
            if (capa.CorrectiveActions != CorrectiveActions) changes.Add("Corrective Actions updated");
            if (capa.PreventiveActions != PreventiveActions) changes.Add("Preventive Actions updated");
            if (capa.OwnerId != OwnerId) changes.Add($"Owner changed");

            // Update CAPA
            capa.Title = Title;
            capa.Description = Description;
            capa.ImmediateActions = ImmediateActions;
            capa.RootCauseAnalysis = RootCauseAnalysis;
            capa.CorrectiveActions = CorrectiveActions;
            capa.PreventiveActions = PreventiveActions;
            capa.ImplementationNotes = ImplementationNotes;
            capa.OwnerId = OwnerId;
            capa.LastModifiedById = userId.Value;
            capa.LastModifiedAt = DateTime.UtcNow;

            if (Enum.TryParse<Domain.Enums.CapaType>(CapaType, out var capaType))
                capa.CapaType = capaType;

            if (Enum.TryParse<CapaPriority>(Priority, out var priority))
                capa.Priority = priority;

            if (TargetCompletionDate.HasValue)
            {
                capa.SetTargetCompletionDate(TargetCompletionDate.Value);
            }

            await _dbContext.SaveChangesAsync();

            // Audit log
            await _auditLogService.LogAsync(
                "CAPA_UPDATED",
                "Capa",
                Id,
                $"CAPA {capa.CapaNumber} updated. Changes: {string.Join("; ", changes)}",
                CancellationToken.None);

            _logger.LogInformation("CAPA {CapaId} updated by user {UserId}. Changes: {Changes}", Id, userId, string.Join("; ", changes));
            SuccessMessage = "CAPA updated successfully.";
            CanEdit = true;
            
            // Refresh status info
            var nextStatus = capa.GetNextStatus();
            NextStatus = nextStatus?.ToString();
            CanAdvanceStatus = nextStatus != null;
            CanDelete = capa.CanBeDeleted && CheckDeletePermission(roles, capa.CreatedById, userId.Value);
            
            await LoadLookupsAsync();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating CAPA {CapaId}", Id);
            ErrorMessage = "An error occurred while updating the CAPA. Please try again.";
            CanEdit = true;
            await LoadLookupsAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAdvanceStatusAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return RedirectToPage("/Account/Login");

        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (currentUser == null)
            return RedirectToPage("/Account/Login");

        var capa = await _dbContext.Capas.FirstOrDefaultAsync(c => c.Id == Id);
        if (capa == null)
        {
            return RedirectToPage("./Index");
        }

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var (canEdit, _) = CheckEditPermission(roles, capa.CreatedById, userId.Value);
        
        if (!canEdit)
        {
            return RedirectToPage(new { Id, message = "You do not have permission to advance this CAPA.", success = false });
        }

        var previousStatus = capa.Status;
        var nextStatus = capa.GetNextStatus();
        
        if (nextStatus == null)
        {
            return RedirectToPage(new { Id, message = "CAPA cannot be advanced further.", success = false });
        }

        // Special handling for effectiveness verification
        if (nextStatus == CapaStatus.EffectivenessVerified)
        {
            if (capa.CreatedById == userId)
            {
                return RedirectToPage(new { Id, message = "You cannot verify effectiveness of a CAPA you created.", success = false });
            }
        }

        if (!capa.AdvanceStatus())
        {
            return RedirectToPage(new { Id, message = "Failed to advance CAPA status.", success = false });
        }

        capa.LastModifiedById = userId.Value;
        await _dbContext.SaveChangesAsync();

        // Audit log
        await _auditLogService.LogAsync(
            "CAPA_STATUS_ADVANCED",
            "Capa",
            Id,
            $"CAPA {capa.CapaNumber} status advanced from {previousStatus} to {capa.Status}",
            CancellationToken.None);

        _logger.LogInformation("CAPA {CapaId} status advanced from {PreviousStatus} to {NewStatus} by user {UserId}", 
            Id, previousStatus, capa.Status, userId);

        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return RedirectToPage("/Account/Login");

        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (currentUser == null)
            return RedirectToPage("/Account/Login");

        var capa = await _dbContext.Capas
            .Include(c => c.Actions)
            .FirstOrDefaultAsync(c => c.Id == Id);

        if (capa == null)
        {
            return RedirectToPage("./Index");
        }

        // Check if CAPA can be deleted (not verified or closed)
        if (!capa.CanBeDeleted)
        {
            ErrorMessage = "Cannot delete CAPA that has been verified or closed.";
            CanEdit = true;
            await LoadLookupsAsync();
            return Page();
        }

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        if (!CheckDeletePermission(roles, capa.CreatedById, userId.Value))
        {
            ErrorMessage = "You do not have permission to delete this CAPA.";
            CanEdit = true;
            await LoadLookupsAsync();
            return Page();
        }

        try
        {
            var capaNumber = capa.CapaNumber;
            
            // Delete related actions first
            if (capa.Actions?.Any() == true)
            {
                _dbContext.RemoveRange(capa.Actions);
            }

            _dbContext.Capas.Remove(capa);
            await _dbContext.SaveChangesAsync();

            // Audit log
            await _auditLogService.LogAsync(
                "CAPA_DELETED",
                "Capa",
                Id,
                $"CAPA {capaNumber} deleted",
                CancellationToken.None);

            _logger.LogInformation("CAPA {CapaId} ({CapaNumber}) deleted by user {UserId}", Id, capaNumber, userId);
            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting CAPA {CapaId}", Id);
            ErrorMessage = "An error occurred while deleting the CAPA. Please try again.";
            CanEdit = true;
            await LoadLookupsAsync();
            return Page();
        }
    }

    private async Task LoadLookupsAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        if (tenantId == Guid.Empty) return;

        Users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Include(u => u.OrganizationUnit)
            .Select(u => new UserOption(u.Id, $"{u.FirstName} {u.LastName}", u.OrganizationUnit != null ? u.OrganizationUnit.Name : "—"))
            .ToListAsync();
    }

    public record UserOption(Guid Id, string Name, string Department);
}
