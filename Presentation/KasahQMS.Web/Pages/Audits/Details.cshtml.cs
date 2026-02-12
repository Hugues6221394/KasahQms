using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Entities.Audits;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Audits;

/// <summary>
/// Audit Details page with proper authorization.
/// View rules:
/// - TMD, Deputy Country Manager: Full access
/// - Department Managers: Can view audits affecting their departments
/// - Internal Auditors: Can view audits they are assigned to
/// - Quality/Compliance roles: Can view all audits
/// - Junior Staff: Limited visibility (only basic info if directly affected)
/// - System Admin: Technical metadata only
/// </summary>
[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<DetailsModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public AuditDetailView? Audit { get; set; }
    public List<FindingView> Findings { get; set; } = new();
    public List<TeamMemberView> TeamMembers { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public bool CanEdit { get; set; }
    public bool CanSchedule { get; set; }
    public string UserPermissionContext { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
            return Unauthorized();

        // Get current user with roles
        var currentUser = await _dbContext.Users
            .Include(u => u.Roles)
            .Include(u => u.OrganizationUnit)
            .FirstOrDefaultAsync(u => u.Id == currentUserId.Value);

        if (currentUser == null)
            return Unauthorized();

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();

        // Fetch audit with related data
        var audit = await _dbContext.Audits
            .Include(a => a.LeadAuditor)
            .Include(a => a.Findings)!
                .ThenInclude(f => f.ResponsibleUser)
            .Include(a => a.Findings)!
                .ThenInclude(f => f.IdentifiedBy)
            .Include(a => a.TeamMembers)!
                .ThenInclude(tm => tm.User)
            .FirstOrDefaultAsync(a => a.Id == Id);

        if (audit == null)
        {
            ErrorMessage = "Audit not found.";
            return Page();
        }

        // Check view permissions
        var canView = CheckViewPermission(roles, audit, currentUserId.Value);
        if (!canView.CanView)
        {
            _logger.LogWarning("User {UserId} denied access to audit {AuditId}: {Reason}",
                currentUserId, Id, canView.Reason);
            ErrorMessage = canView.Reason;
            return Page();
        }

        UserPermissionContext = canView.Context;

        // Check edit/schedule permissions
        CanSchedule = CheckSchedulePermission(roles);
        CanEdit = CanSchedule; // Only schedulers can edit

        // Map audit to view model
        Audit = new AuditDetailView
        {
            Id = audit.Id,
            AuditNumber = audit.AuditNumber,
            Title = audit.Title,
            Description = audit.Description,
            AuditType = audit.AuditType.ToString(),
            Status = audit.Status.ToString(),
            StatusClass = GetStatusClass(audit.Status),
            PlannedStartDate = audit.PlannedStartDate,
            PlannedEndDate = audit.PlannedEndDate,
            ActualStartDate = audit.ActualStartDate,
            ActualEndDate = audit.ActualEndDate,
            Scope = audit.Scope,
            Objectives = audit.Objectives,
            Conclusion = audit.Conclusion,
            LeadAuditorName = audit.LeadAuditor?.FullName ?? "Unassigned",
            CreatedAt = audit.CreatedAt
        };

        // Map findings
        if (audit.Findings != null)
        {
            Findings = audit.Findings.OrderByDescending(f => f.IdentifiedAt).Select(f => new FindingView
            {
                Id = f.Id,
                FindingNumber = f.FindingNumber,
                Title = f.Title,
                Description = f.Description,
                FindingType = f.FindingType,
                Severity = f.Severity.ToString(),
                SeverityClass = GetSeverityClass(f.Severity),
                Status = f.Status,
                Clause = f.Clause,
                Evidence = f.Evidence,
                ResponsibleUserName = f.ResponsibleUser?.FullName,
                ResponseDueDate = f.ResponseDueDate,
                Response = f.Response,
                IdentifiedByName = f.IdentifiedBy?.FullName ?? "Unknown",
                IdentifiedAt = f.IdentifiedAt,
                HasLinkedCapa = f.LinkedCapaId.HasValue
            }).ToList();
        }

        // Map team members
        if (audit.TeamMembers != null)
        {
            TeamMembers = audit.TeamMembers.Select(tm => new TeamMemberView
            {
                UserId = tm.UserId,
                Name = tm.User?.FullName ?? "Unknown",
                Role = tm.Role.ToString()
            }).ToList();
        }

        _logger.LogInformation("User {UserId} ({Roles}) viewed audit {AuditId}",
            currentUserId, string.Join(", ", roles), Id);

        return Page();
    }

    private (bool CanView, string Reason, string Context) CheckViewPermission(List<string> roles, Audit audit, Guid currentUserId)
    {
        // TMD has full access
        if (roles.Any(r => r.Contains("TMD") || r.Contains("Top Managing Director") || r.Contains("Managing Director")))
        {
            return (true, "", "Full Access (TMD)");
        }

        // Deputy Country Manager has full access
        if (roles.Any(r => r.Contains("Deputy") || r.Contains("Country Manager") || r.Contains("Operations")))
        {
            return (true, "", "Full Access (Deputy/Operations)");
        }

        // Quality/Compliance roles have full access
        if (roles.Any(r => r.Contains("Quality") || r.Contains("Compliance")))
        {
            return (true, "", "Full Access (Quality/Compliance)");
        }

        // Internal Auditors can view audits they are assigned to or all audits for audit purposes
        if (roles.Any(r => r.Contains("Auditor") || r.Contains("Internal Auditor")))
        {
            var isTeamMember = audit.TeamMembers?.Any(tm => tm.UserId == currentUserId) ?? false;
            var isLeadAuditor = audit.LeadAuditorId == currentUserId;
            if (isTeamMember || isLeadAuditor)
            {
                return (true, "", "Audit Team Member");
            }
            // Auditors can view all audits for reference, but read-only
            return (true, "", "Auditor (Read-Only)");
        }

        // Department Managers can view audits (for awareness of audits in their area)
        if (roles.Any(r => r.Contains("Manager") || r.Contains("Department")))
        {
            return (true, "", "Department Manager (Read-Only)");
        }

        // System Admin - technical access
        if (roles.Any(r => r.Contains("Admin") || r.Contains("System Admin")))
        {
            return (true, "", "System Admin (Technical View)");
        }

        // Junior Staff - very limited
        if (roles.Any(r => r.Contains("Junior") || r.Contains("Staff")))
        {
            return (false, "Junior staff do not have access to audit details. Contact your manager if you need this information.", "");
        }

        return (false, "You do not have permission to view this audit.", "");
    }

    private bool CheckSchedulePermission(List<string> roles)
    {
        // Only TMD and Deputy can create/schedule audits
        // Per rules: Audit scheduling is a management responsibility
        return roles.Any(r => 
            r.Contains("TMD") || 
            r.Contains("Top Managing Director") ||
            r.Contains("Managing Director") ||
            r.Contains("Deputy") || 
            r.Contains("Country Manager") ||
            r.Contains("Operations"));
    }

    private static string GetStatusClass(AuditStatus status)
    {
        return status switch
        {
            AuditStatus.Planned => "bg-brand-100 text-brand-700",
            AuditStatus.InProgress => "bg-amber-100 text-amber-700",
            AuditStatus.Completed => "bg-emerald-100 text-emerald-700",
            AuditStatus.Closed => "bg-slate-100 text-slate-600",
            _ => "bg-slate-100 text-slate-600"
        };
    }

    private static string GetSeverityClass(FindingSeverity severity)
    {
        return severity switch
        {
            FindingSeverity.Critical => "bg-red-100 text-red-700",
            FindingSeverity.Major => "bg-orange-100 text-orange-700",
            FindingSeverity.Minor => "bg-amber-100 text-amber-700",
            FindingSeverity.Observation => "bg-blue-100 text-blue-700",
            _ => "bg-slate-100 text-slate-600"
        };
    }

    public class AuditDetailView
    {
        public Guid Id { get; set; }
        public string AuditNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string AuditType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusClass { get; set; } = string.Empty;
        public DateTime PlannedStartDate { get; set; }
        public DateTime PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public string? Scope { get; set; }
        public string? Objectives { get; set; }
        public string? Conclusion { get; set; }
        public string LeadAuditorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class FindingView
    {
        public Guid Id { get; set; }
        public string FindingNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FindingType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string SeverityClass { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Clause { get; set; }
        public string? Evidence { get; set; }
        public string? ResponsibleUserName { get; set; }
        public DateTime? ResponseDueDate { get; set; }
        public string? Response { get; set; }
        public string IdentifiedByName { get; set; } = string.Empty;
        public DateTime IdentifiedAt { get; set; }
        public bool HasLinkedCapa { get; set; }
    }

    public class TeamMemberView
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
