using KasahQMS.Domain.Enums;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using KasahQMS.Application.Common.Interfaces;

namespace KasahQMS.Web.Pages.Documents;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHierarchyService _hierarchyService;

    public IndexModel(
        ILogger<IndexModel> logger, 
        ApplicationDbContext dbContext, 
        ICurrentUserService currentUserService,
        IHierarchyService hierarchyService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
    }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Type { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 5;
    public int TotalItems { get; set; } = 45;
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Indicates if current user can create documents (auditors cannot)
    /// </summary>
    public bool CanCreateDocument { get; set; } = true;

    /// <summary>
    /// The user's current role context for display purposes
    /// </summary>
    public string UserRoleContext { get; set; } = "Staff";

    public List<DocumentRow> Documents { get; set; } = new();
    public List<LookupItem> DocumentTypes { get; set; } = new();
    public List<LookupItem> Categories { get; set; } = new();

    public async Task OnGetAsync()
    {
        CurrentPage = PageNumber < 1 ? 1 : PageNumber;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var currentUserId = _currentUserService.UserId;

        if (currentUserId == null)
        {
            _logger.LogWarning("Documents page accessed without valid user context");
            return;
        }

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

        var query = _dbContext.Documents.AsNoTracking()
            .Include(d => d.DocumentType)
            .Include(d => d.Category)
            .Where(d => d.TenantId == tenantId);

        // Get current user with roles
        var currentUser = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (currentUser == null)
        {
            _logger.LogWarning("Current user not found: {UserId}", currentUserId);
            return;
        }

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();

        // Determine user's role context and permissions
        bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin" or "TenantAdmin");
        bool isTmdOrDeputy = roles.Any(r => r is "TMD" or "TopManagingDirector" or "Country Manager" or "Deputy" or "DeputyDirector" or "Deputy Country Manager");
        bool isManager = roles.Any(r => r.Contains("Manager"));
        bool isAuditor = roles.Any(r => r == "Auditor");

        // Set role context for UI
        if (isAdmin) UserRoleContext = "Admin";
        else if (isTmdOrDeputy) UserRoleContext = "Executive";
        else if (isManager) UserRoleContext = "Manager";
        else if (isAuditor) UserRoleContext = "Auditor";
        else UserRoleContext = "Staff";

        // Auditors cannot create documents
        CanCreateDocument = !isAuditor;

        // Apply role-based filtering
        // CRITICAL: This enforces hierarchical visibility
        if (isAdmin || isTmdOrDeputy)
        {
            // Admin/TMD/Deputy can see all documents
            _logger.LogInformation("User {UserId} has full document visibility (Admin/Executive)", currentUserId);
        }
        else if (isAuditor)
        {
            // Auditors can view all documents for audit purposes (read-only)
            _logger.LogInformation("User {UserId} has auditor document visibility (read-only)", currentUserId);
        }
        else if (isManager)
        {
            // Managers can see:
            // 1. Documents they created
            // 2. Documents where they are the current approver
            // 3. Documents created by their subordinates (recursively)
            // 4. Documents targeted to their department or no specific department
            var subordinateIds = await _hierarchyService.GetSubordinateUserIdsAsync(currentUserId.Value);
            var visibleCreatorIds = new List<Guid> { currentUserId.Value };
            visibleCreatorIds.AddRange(subordinateIds);

            query = query.Where(d => 
                visibleCreatorIds.Contains(d.CreatedById) ||  // Created by visible users
                d.CurrentApproverId == currentUserId.Value ||  // Awaiting their approval
                d.TargetDepartmentId == null ||  // No specific department
                d.TargetDepartmentId == currentUser.OrganizationUnitId);  // Targeted to their department

            _logger.LogInformation("Manager {UserId} can see documents for {Count} users (including subordinates)", 
                currentUserId, visibleCreatorIds.Count);
        }
        else
        {
            // Staff can only see:
            // 1. Documents they created
            // 2. Documents where they are the current approver (if any)
            // 3. Documents targeted to their department or no specific department
            query = query.Where(d => 
                d.CreatedById == currentUserId.Value ||
                d.CurrentApproverId == currentUserId.Value ||
                d.TargetDepartmentId == null ||
                d.TargetDepartmentId == currentUser.OrganizationUnitId);

            _logger.LogInformation("Staff user {UserId} can see their own documents and department documents", currentUserId);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            query = query.Where(d => d.Title.Contains(SearchTerm) || d.DocumentNumber.Contains(SearchTerm));
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(Status) && Enum.TryParse<DocumentStatus>(Status, out var status))
        {
            query = query.Where(d => d.Status == status);
        }

        // Apply type filter
        if (!string.IsNullOrWhiteSpace(Type))
        {
            query = query.Where(d => d.DocumentType != null && d.DocumentType.Name == Type);
        }

        TotalItems = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalItems / (double)PageSize);

        Documents = await query
            .OrderByDescending(d => d.LastModifiedAt ?? d.CreatedAt)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .Select(d => new DocumentRow(
                d.Id,
                d.Title,
                d.DocumentNumber,
                d.DocumentType != null ? d.DocumentType.Name : "Unassigned",
                d.Status.ToString(),
                GetStatusClass(d.Status),
                $"v{d.CurrentVersion}",
                (d.LastModifiedAt ?? d.CreatedAt).ToString("MMM dd, yyyy")))
            .ToListAsync();

        _logger.LogInformation("Documents page accessed by {UserId} ({RoleContext}) with filters: Search={Search}, Status={Status}, Type={Type}. Showing {Count} documents.", 
            currentUserId, UserRoleContext, SearchTerm, Status, Type, Documents.Count);
    }

    private static string GetStatusClass(DocumentStatus status)
    {
        return status switch
        {
            DocumentStatus.Approved => "bg-emerald-100 text-emerald-700",
            DocumentStatus.Submitted => "bg-amber-100 text-amber-800",
            DocumentStatus.InReview => "bg-indigo-100 text-indigo-700",
            DocumentStatus.Rejected => "bg-rose-100 text-rose-700",
            DocumentStatus.Archived => "bg-slate-100 text-slate-500",
            _ => "bg-slate-100 text-slate-600"
        };
    }

    public record DocumentRow(
        Guid Id,
        string Title,
        string Number,
        string Type,
        string Status,
        string StatusClass,
        string Version,
        string ModifiedAt);

    public record LookupItem(Guid Id, string Name);
}

