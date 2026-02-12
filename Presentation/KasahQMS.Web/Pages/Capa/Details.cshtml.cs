using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Capa;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public DetailsModel(ApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string PriorityClass { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Owner { get; set; } = "Unassigned";
    public string Creator { get; set; } = "Unknown";
    public string DueDate { get; set; } = "No due date";
    public string LinkedAudit { get; set; } = "Not linked";
    public string LinkedFinding { get; set; } = "Not linked";
    public string? ImmediateActions { get; set; }
    public string? RootCauseAnalysis { get; set; }
    public string? CorrectiveActions { get; set; }
    public string? PreventiveActions { get; set; }
    public string? ImplementationNotes { get; set; }
    public string? VerificationNotes { get; set; }
    public bool? IsEffective { get; set; }
    public string? VerifiedBy { get; set; }
    public string? VerifiedAt { get; set; }
    
    // Permission flags
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public string UserPermissionContext { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        
        var capa = await _dbContext.Capas
            .Include(c => c.Owner)
            .Include(c => c.VerifiedBy)
            .FirstOrDefaultAsync(c => c.Id == Id);

        if (capa == null)
        {
            return RedirectToPage("/Capa/Index");
        }

        // Check user permissions
        if (userId.HasValue)
        {
            var currentUser = await _dbContext.Users
                .AsNoTracking()
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (currentUser != null)
            {
                var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
                var (canEdit, editContext) = CheckEditPermission(roles, capa.CreatedById, userId.Value);
                var canDelete = capa.CanBeDeleted && CheckDeletePermission(roles, capa.CreatedById, userId.Value);

                CanEdit = canEdit;
                CanDelete = canDelete;
                UserPermissionContext = editContext;
            }
        }

        // Get creator name
        var creatorName = await _dbContext.Users.AsNoTracking()
            .Where(u => u.Id == capa.CreatedById)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstOrDefaultAsync() ?? "Unknown";

        Number = capa.CapaNumber;
        Title = capa.Title;
        Description = capa.Description;
        Status = capa.Status.ToString();
        StatusLabel = GetStatusLabel(capa.Status);
        StatusClass = GetStatusClass(capa.Status);
        Priority = capa.Priority.ToString();
        PriorityClass = GetPriorityClass(capa.Priority);
        Type = capa.CapaType.ToString();
        Owner = capa.Owner != null ? capa.Owner.FullName : "Unassigned";
        Creator = creatorName;
        DueDate = capa.TargetCompletionDate.HasValue ? capa.TargetCompletionDate.Value.ToString("MMM dd, yyyy") : "No due date";
        LinkedAudit = capa.SourceAuditId.HasValue ? capa.SourceAuditId.Value.ToString() : "Not linked";
        LinkedFinding = capa.SourceAuditFindingId.HasValue ? capa.SourceAuditFindingId.Value.ToString() : "Not linked";
        ImmediateActions = capa.ImmediateActions;
        RootCauseAnalysis = capa.RootCauseAnalysis;
        CorrectiveActions = capa.CorrectiveActions;
        PreventiveActions = capa.PreventiveActions;
        ImplementationNotes = capa.ImplementationNotes;
        VerificationNotes = capa.VerificationNotes;
        IsEffective = capa.IsEffective;
        VerifiedBy = capa.VerifiedBy?.FullName;
        VerifiedAt = capa.VerifiedAt?.ToString("MMM dd, yyyy HH:mm");

        return Page();
    }

    private (bool CanEdit, string Message) CheckEditPermission(List<string> roles, Guid capaCreatedById, Guid currentUserId)
    {
        if (roles.Any(r => r is "System Admin" or "SystemAdmin" or "Admin" or "TenantAdmin"))
            return (true, "System Admin - can edit any CAPA");

        if (roles.Any(r => r is "TMD" or "TopManagingDirector" or "Country Manager"))
            return (true, "TMD - can edit any CAPA");

        if (roles.Any(r => r is "Deputy" or "DeputyDirector" or "Deputy Country Manager"))
            return (true, "Deputy - can edit CAPAs in scope");

        if (roles.Any(r => r.Contains("Manager")))
        {
            if (capaCreatedById == currentUserId)
                return (true, "Department Manager - can edit own CAPA");
            return (false, "Department Managers can only edit CAPAs they created.");
        }

        if (roles.Any(r => r is "Auditor" or "Internal Auditor"))
            return (false, "Auditors have read-only access.");

        return (false, "No edit permission");
    }

    private bool CheckDeletePermission(List<string> roles, Guid capaCreatedById, Guid currentUserId)
    {
        if (roles.Any(r => r is "System Admin" or "SystemAdmin" or "Admin" or "TenantAdmin"))
            return true;

        if (roles.Any(r => r is "TMD" or "TopManagingDirector" or "Country Manager"))
            return true;

        if (roles.Any(r => r.Contains("Manager")))
            return capaCreatedById == currentUserId;

        return false;
    }

    private static string GetStatusLabel(KasahQMS.Domain.Enums.CapaStatus status)
    {
        return status switch
        {
            Domain.Enums.CapaStatus.Draft => "Draft",
            Domain.Enums.CapaStatus.UnderInvestigation => "Under Investigation",
            Domain.Enums.CapaStatus.ActionsDefined => "Actions Defined",
            Domain.Enums.CapaStatus.ActionsImplemented => "Actions Implemented",
            Domain.Enums.CapaStatus.EffectivenessVerified => "Effectiveness Verified",
            Domain.Enums.CapaStatus.Closed => "Closed",
            _ => status.ToString()
        };
    }

    private static string GetStatusClass(KasahQMS.Domain.Enums.CapaStatus status)
    {
        return status switch
        {
            Domain.Enums.CapaStatus.Draft => "bg-slate-100 text-slate-700",
            Domain.Enums.CapaStatus.UnderInvestigation => "bg-amber-100 text-amber-700",
            Domain.Enums.CapaStatus.ActionsDefined => "bg-blue-100 text-blue-700",
            Domain.Enums.CapaStatus.ActionsImplemented => "bg-indigo-100 text-indigo-700",
            Domain.Enums.CapaStatus.EffectivenessVerified => "bg-emerald-100 text-emerald-700",
            Domain.Enums.CapaStatus.Closed => "bg-green-100 text-green-800",
            _ => "bg-slate-100 text-slate-600"
        };
    }

    private static string GetPriorityClass(KasahQMS.Domain.Enums.CapaPriority priority)
    {
        return priority switch
        {
            Domain.Enums.CapaPriority.Critical => "bg-rose-100 text-rose-700",
            Domain.Enums.CapaPriority.High => "bg-amber-100 text-amber-700",
            Domain.Enums.CapaPriority.Medium => "bg-brand-100 text-brand-700",
            Domain.Enums.CapaPriority.Low => "bg-emerald-100 text-emerald-700",
            _ => "bg-slate-100 text-slate-600"
        };
    }
}
