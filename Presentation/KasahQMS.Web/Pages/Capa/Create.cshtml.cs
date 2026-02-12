using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Features.Capa.Commands;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Capa;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IMediator mediator,
        IAuditLogService auditLogService,
        ILogger<CreateModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _mediator = mediator;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string CapaType { get; set; } = "Corrective";
    [BindProperty] public string Priority { get; set; } = "Medium";
    [BindProperty] public Guid? OwnerId { get; set; }
    [BindProperty] public DateTime? TargetCompletionDate { get; set; }
    [BindProperty] public string? ImmediateActions { get; set; }
    [BindProperty] public Guid? LinkedAuditId { get; set; }

    public List<UserOption> Users { get; set; } = new();
    public List<AuditOption> Audits { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public bool CanCreate { get; set; }
    public string UserRoleContext { get; set; } = string.Empty;

    /// <summary>
    /// Checks if user has permission to create CAPAs.
    /// Allowed: TMD, Deputy, Department Managers, System Admin
    /// Not allowed: Junior Staff, Auditors
    /// </summary>
    private (bool CanCreate, string Message) CheckCreatePermission(List<string> roles)
    {
        // System Admin can create CAPAs
        if (roles.Any(r => r is "System Admin" or "SystemAdmin" or "Admin" or "TenantAdmin"))
        {
            return (true, "System Admin");
        }

        // TMD can create CAPAs
        if (roles.Any(r => r is "TMD" or "TopManagingDirector" or "Country Manager"))
        {
            return (true, "TMD");
        }

        // Deputy can create CAPAs
        if (roles.Any(r => r is "Deputy" or "DeputyDirector" or "Deputy Country Manager"))
        {
            return (true, "Deputy");
        }

        // Department Managers can create CAPAs (within their department scope)
        if (roles.Any(r => r.Contains("Manager")))
        {
            return (true, "Department Manager");
        }

        // Auditors cannot create CAPAs
        if (roles.Any(r => r is "Auditor" or "Internal Auditor"))
        {
            return (false, "Auditors cannot create CAPAs. This is a read-only role.");
        }

        // Junior staff cannot create CAPAs
        return (false, "You do not have permission to create CAPAs. Only TMD, Deputies, Department Managers, and System Admins can create CAPAs.");
    }

    public async Task OnGetAsync()
    {
        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

        if (currentUser == null)
        {
            CanCreate = false;
            ErrorMessage = "User not authenticated.";
            return;
        }

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var (canCreate, message) = CheckCreatePermission(roles);
        
        CanCreate = canCreate;
        if (!canCreate)
        {
            ErrorMessage = message;
            return;
        }
        
        UserRoleContext = message;
        await LoadLookupsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);

        if (currentUser == null)
            return RedirectToPage("/Account/Login");

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var (canCreate, message) = CheckCreatePermission(roles);
        
        if (!canCreate)
        {
            ErrorMessage = message;
            CanCreate = false;
            await LoadLookupsAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Title))
            ModelState.AddModelError(nameof(Title), "Title is required.");

        if (!ModelState.IsValid)
        {
            CanCreate = true;
            await LoadLookupsAsync();
            return Page();
        }

        if (!Enum.TryParse<CapaType>(CapaType, out var capaType))
            capaType = Domain.Enums.CapaType.Corrective;

        if (!Enum.TryParse<CapaPriority>(Priority, out var priority))
            priority = CapaPriority.Medium;

        try
        {
            var cmd = new CreateCapaCommand(
                Title,
                Description,
                capaType,
                priority,
                OwnerId,
                LinkedAuditId,
                null, // LinkedAuditFindingId
                TargetCompletionDate,
                ImmediateActions);

            var result = await _mediator.Send(cmd);
            
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to create CAPA.";
                CanCreate = true;
                await LoadLookupsAsync();
                return Page();
            }

            // Audit log
            await _auditLogService.LogAsync(
                "CAPA_CREATED",
                "Capa",
                result.Value,
                $"CAPA created: {Title}",
                CancellationToken.None);

            _logger.LogInformation("CAPA created: {CapaId} by user {UserId} ({Role})", result.Value, currentUser.Id, message);
            return RedirectToPage("./Details", new { id = result.Value });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to create CAPA");
            ErrorMessage = "You don't have permission to create CAPAs. Please contact your administrator.";
            CanCreate = true;
            await LoadLookupsAsync();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating CAPA");
            ErrorMessage = "An error occurred while creating the CAPA. Please try again.";
            CanCreate = true;
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

        Audits = await _dbContext.Audits
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new AuditOption(a.Id, a.AuditNumber, a.Title))
            .ToListAsync();
    }

    public record UserOption(Guid Id, string Name, string Department);
    public record AuditOption(Guid Id, string Number, string Title);
}
