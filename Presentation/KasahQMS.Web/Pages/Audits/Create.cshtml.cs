using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Security;
using KasahQMS.Application.Features.Audits.Commands;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Audits;

[Microsoft.AspNetCore.Authorization.Authorize]
public class CreateModel : PageModel
{
    private readonly IMediator _mediator;
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuthorizationService _authorizationService;

    public CreateModel(
        IMediator mediator,
        ApplicationDbContext dbContext,
        IAuthorizationService authorizationService)
    {
        _mediator = mediator;
        _dbContext = dbContext;
        _authorizationService = authorizationService;
    }

    [BindProperty]
    public string Title { get; set; } = string.Empty;

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public AuditType AuditType { get; set; }

    [BindProperty]
    public DateTime ScheduledStartDate { get; set; } = DateTime.Today.AddDays(7);

    [BindProperty]
    public DateTime ScheduledEndDate { get; set; } = DateTime.Today.AddDays(8);

    [BindProperty]
    public Guid? LeadAuditorId { get; set; }

    [BindProperty]
    public string? Scope { get; set; }

    [BindProperty]
    public string? Objectives { get; set; }

    public string? ErrorMessage { get; set; }

    public List<AuditorItem> Auditors { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await _authorizationService.HasPermissionAsync(Permissions.Audits.Create))
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        await LoadAuditorsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await _authorizationService.HasPermissionAsync(Permissions.Audits.Create))
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        if (!ModelState.IsValid)
        {
            await LoadAuditorsAsync();
            return Page();
        }

        if (ScheduledEndDate < ScheduledStartDate)
        {
            ModelState.AddModelError(nameof(ScheduledEndDate), "End date cannot be before start date.");
            await LoadAuditorsAsync();
            return Page();
        }

        try
        {
            var command = new CreateAuditCommand(
                Title,
                Description,
                AuditType,
                ScheduledStartDate,
                ScheduledEndDate,
                LeadAuditorId,
                Scope,
                Objectives);

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
            {
                return RedirectToPage("./Index");
            }

            ErrorMessage = result.ErrorMessage;
        }
        catch (Exception)
        {
            ErrorMessage = "An unexpected error occurred while creating the audit.";
        }

        await LoadAuditorsAsync();
        return Page();
    }

    private async Task LoadAuditorsAsync()
    {
        var auditors = await _dbContext.Users
            .Where(u => u.Roles.Any(r => r.Name == "Auditor" || r.Name == "Internal Auditor"))
            .Select(u => new AuditorItem(u.Id, u.FullName))
            .ToListAsync();

        Auditors = auditors;
    }

    public record AuditorItem(Guid Id, string Name);
}
