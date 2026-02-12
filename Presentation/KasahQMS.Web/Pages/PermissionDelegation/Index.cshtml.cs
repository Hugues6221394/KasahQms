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

        // Check if user can delegate (has ViewAll permission)
        var canDelegate = await _authorizationService.HasPermissionAsync(
            "Users.ViewAll", 
            CancellationToken.None);

        if (canDelegate)
        {
            // Get my delegations
            var myDelegationsQuery = new GetMyDelegationsQuery();
            var myDelegationsResult = await _mediator.Send(myDelegationsQuery);
            if (myDelegationsResult.IsSuccess)
            {
                MyDelegations = myDelegationsResult.Value.ToList();
            }

            // Get subordinates
            var subordinateIds = await _hierarchyService.GetSubordinateIdsAsync(userId.Value, recursive: true);
            var subordinates = await _dbContext.Users
                .Where(u => subordinateIds.Contains(u.Id))
                .Select(u => new SubordinateItem(u.Id, u.FullName, u.Email ?? "N/A"))
                .ToListAsync();

            Subordinates = subordinates;

            // Get available permissions (permissions the user has)
            var userPermissions = await _authorizationService.GetUserPermissionsAsync();
            AvailablePermissions = userPermissions
                .Where(p => !p.EndsWith(".ViewAll")) // Exclude ViewAll as it's too broad
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
}

