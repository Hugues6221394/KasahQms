using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Application.Features.PermissionDelegation.Commands;
using KasahQMS.Application.Features.PermissionDelegation.Queries;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Infrastructure.Persistence.Data;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using DelegationDto = KasahQMS.Application.Common.Interfaces.Services.DelegationDto;

namespace KasahQMS.Web.Pages.PermissionDelegation;

[Microsoft.AspNetCore.Authorization.Authorize]
public class IndexModel : PageModel
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHierarchyService _hierarchyService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IMediator mediator,
        ICurrentUserService currentUserService,
        IHierarchyService hierarchyService,
        IAuthorizationService authorizationService,
        ApplicationDbContext dbContext,
        ILogger<IndexModel> logger)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
        _authorizationService = authorizationService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public List<DelegationDto> MyDelegations { get; set; } = new();
    public List<DelegationDto> ReceivedDelegations { get; set; } = new();
    public List<SubordinateItem> Subordinates { get; set; } = new();
    public List<PermissionItem> AvailablePermissions { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty]
    public Guid? SelectedSubordinateId { get; set; }

    [BindProperty]
    public string? SelectedPermission { get; set; }

    [BindProperty]
    public int? ExpiresAfterDays { get; set; }

    public async Task OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
        {
            ErrorMessage = "User not authenticated.";
            return;
        }

        // Check if user can delegate (has ViewAll permission OR is a senior role)
        var canDelegate = await _authorizationService.HasPermissionAsync(
            "Users.ViewAll", 
            CancellationToken.None);

        if (!canDelegate)
        {
            // Also allow TMD, Deputy, Department Manager by checking their role claim
            var userRoles = User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
            
            canDelegate = userRoles.Any(r => 
                r.Contains("TMD", StringComparison.OrdinalIgnoreCase) || 
                r.Contains("Deputy", StringComparison.OrdinalIgnoreCase) || 
                r.Contains("Country Manager", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("Manager", StringComparison.OrdinalIgnoreCase) || 
                r.Contains("System Admin", StringComparison.OrdinalIgnoreCase));
        }

        if (canDelegate)
        {
            // Get my delegations
            var myDelegationsQuery = new GetMyDelegationsQuery();
            var myDelegationsResult = await _mediator.Send(myDelegationsQuery);
            if (myDelegationsResult.IsSuccess)
            {
                MyDelegations = myDelegationsResult.Value.ToList();
            }

            // Get subordinates / delegateable targets
            // Department managers see staff from their own department only
            // TMD/Deputy/SysAdmin see all subordinates via hierarchy
            var userRolesForSubordCheck = User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value).ToList();

            var isSeniorRole = userRolesForSubordCheck.Any(r =>
                r.Contains("TMD", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("Deputy", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("Country Manager", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("System Admin", StringComparison.OrdinalIgnoreCase));

            var isDeptManager = !isSeniorRole && userRolesForSubordCheck.Any(r =>
                r.Contains("Manager", StringComparison.OrdinalIgnoreCase));

            List<SubordinateItem> subordinates;
            if (isDeptManager)
            {
                // Load staff from the same department (OrganizationUnitId)
                var managerOrgUnitId = await _dbContext.Users
                    .Where(u => u.Id == userId.Value)
                    .Select(u => u.OrganizationUnitId)
                    .FirstOrDefaultAsync();

                if (managerOrgUnitId.HasValue)
                {
                    subordinates = await _dbContext.Users
                        .Where(u => u.OrganizationUnitId == managerOrgUnitId && u.Id != userId.Value && u.IsActive)
                        .Select(u => new SubordinateItem(u.Id, u.FullName, u.Email ?? "N/A"))
                        .ToListAsync();
                }
                else
                {
                    // Fallback to ManagerId chain
                    var subordinateIds = await _hierarchyService.GetSubordinateUserIdsAsync(userId.Value, recursive: true);
                    subordinates = await _dbContext.Users
                        .Where(u => subordinateIds.Contains(u.Id))
                        .Select(u => new SubordinateItem(u.Id, u.FullName, u.Email ?? "N/A"))
                        .ToListAsync();
                }
            }
            else
            {
                // TMD/Deputy/SysAdmin: use hierarchy subordinate chain
                var subordinateIds = await _hierarchyService.GetSubordinateUserIdsAsync(userId.Value, recursive: true);
                subordinates = await _dbContext.Users
                    .Where(u => subordinateIds.Contains(u.Id))
                    .Select(u => new SubordinateItem(u.Id, u.FullName, u.Email ?? "N/A"))
                    .ToListAsync();

                // System Admin may have no formal hierarchy subordinates — show all active users
                if (!subordinates.Any())
                {
                    var tenantId = _currentUserService.TenantId;
                    subordinates = await _dbContext.Users
                        .Where(u => u.TenantId == tenantId && u.IsActive && u.Id != userId.Value)
                        .OrderBy(u => u.FirstName)
                        .Select(u => new SubordinateItem(u.Id, u.FullName, u.Email ?? "N/A"))
                        .ToListAsync();
                }
            }

            Subordinates = subordinates;

            // Get available permissions (permissions the user has)
            var userPermissions = await _authorizationService.GetUserPermissionsAsync();
            var isSysAdminForPerms = User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value)
                .Any(r =>
                    r.Contains("System Admin", StringComparison.OrdinalIgnoreCase) ||
                    r.Contains("SystemAdmin", StringComparison.OrdinalIgnoreCase) ||
                    r.Contains("Tenant Admin", StringComparison.OrdinalIgnoreCase) ||
                    r.Contains("TenantAdmin", StringComparison.OrdinalIgnoreCase));

            var permissionsForDelegation = isSysAdminForPerms
                ? GetAllPermissionConstants()
                : userPermissions;

            AvailablePermissions = permissionsForDelegation
                .Select(p => new PermissionItem(p, p))
                .OrderBy(p => p.Value)
                .ToList();
        }

        // Get received delegations (all users can see what they received)
        var receivedQuery = new GetReceivedDelegationsQuery();
        var receivedResult = await _mediator.Send(receivedQuery);
        if (receivedResult.IsSuccess)
        {
            ReceivedDelegations = receivedResult.Value.ToList();
        }
    }

    public async Task<IActionResult> OnPostDelegateAsync()
    {
        if (!SelectedSubordinateId.HasValue || string.IsNullOrEmpty(SelectedPermission))
        {
            ErrorMessage = "Please select a subordinate and permission.";
            await OnGetAsync();
            return Page();
        }

        try
        {
            var command = new DelegatePermissionCommand(
                SelectedSubordinateId.Value,
                SelectedPermission,
                ExpiresAfterDays);

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
            {
                SuccessMessage = $"Permission '{SelectedPermission}' successfully delegated.";
                SelectedSubordinateId = null;
                SelectedPermission = null;
                ExpiresAfterDays = null;
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delegating permission");
            ErrorMessage = "An error occurred while delegating the permission.";
        }

        await OnGetAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAsync(Guid delegationId)
    {
        try
        {
            var command = new RevokeDelegationCommand(delegationId);
            var result = await _mediator.Send(command);

            if (result.IsSuccess)
            {
                SuccessMessage = "Delegation revoked successfully.";
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking delegation {DelegationId}", delegationId);
            ErrorMessage = "An error occurred while revoking the delegation.";
        }

        await OnGetAsync();
        return Page();
    }

    public record SubordinateItem(Guid Id, string Name, string Email);
    public record PermissionItem(string Label, string Value);

    private static IEnumerable<string> GetAllPermissionConstants()
    {
        return typeof(Permissions)
            .GetNestedTypes(BindingFlags.Public)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            .Where(f => f.FieldType == typeof(string) && f.IsLiteral && !f.IsInitOnly)
            .Select(f => f.GetRawConstantValue()?.ToString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}

