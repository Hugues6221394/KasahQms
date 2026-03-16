using System.Security.Claims;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KasahQMS.Web.Pages.Privacy;

public class IndexModel : PageModel
{
    private readonly IDataPrivacyService _privacyService;
    private readonly ICurrentUserService _currentUserService;

    public IndexModel(
        IDataPrivacyService privacyService,
        ICurrentUserService currentUserService)
    {
        _privacyService = privacyService;
        _currentUserService = currentUserService;
    }

    public List<ConsentItem> Consents { get; set; } = new();
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadConsentsAsync();
    }

    public async Task<IActionResult> OnPostToggleConsentAsync(string consentType, bool isGranted)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        if (userId == null || tenantId == Guid.Empty)
        {
            ErrorMessage = "Unable to determine user identity.";
            await LoadConsentsAsync();
            return Page();
        }

        if (!Enum.TryParse<ConsentType>(consentType, out var type))
        {
            ErrorMessage = "Invalid consent type.";
            await LoadConsentsAsync();
            return Page();
        }

        try
        {
            if (isGranted)
            {
                await _privacyService.RecordConsentAsync(userId.Value, tenantId, type, true,
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    HttpContext.Request.Headers.UserAgent.ToString());
            }
            else
            {
                await _privacyService.RevokeConsentAsync(userId.Value, type);
            }
            StatusMessage = $"Consent for {consentType} has been updated.";
        }
        catch (Exception)
        {
            ErrorMessage = "Failed to update consent. Please try again.";
        }

        await LoadConsentsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostExportDataAsync()
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        if (userId == null || tenantId == Guid.Empty)
        {
            ErrorMessage = "Unable to determine user identity.";
            await LoadConsentsAsync();
            return Page();
        }

        try
        {
            await _privacyService.RequestDataExportAsync(userId.Value, tenantId);
            StatusMessage = "Data export request submitted. You can track its status on the Data Export page.";
        }
        catch (Exception)
        {
            ErrorMessage = "Failed to submit export request. Please try again.";
        }

        await LoadConsentsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAccountAsync()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            ErrorMessage = "Unable to determine user identity.";
            await LoadConsentsAsync();
            return Page();
        }

        try
        {
            await _privacyService.AnonymizeUserDataAsync(userId.Value);
            return RedirectToPage("/Account/Login");
        }
        catch (Exception)
        {
            ErrorMessage = "Failed to process account deletion. Please contact support.";
        }

        await LoadConsentsAsync();
        return Page();
    }

    private async Task LoadConsentsAsync()
    {
        var userId = GetUserId();
        if (userId == null) return;

        var records = await _privacyService.GetConsentsAsync(userId.Value);
        var allTypes = Enum.GetValues<ConsentType>();

        Consents = allTypes.Select(type =>
        {
            var record = records.FirstOrDefault(r => r.ConsentType == type);
            return new ConsentItem(
                type.ToString(),
                GetConsentDescription(type),
                record?.IsGranted ?? false
            );
        }).ToList();
    }

    private static string GetConsentDescription(ConsentType type) => type switch
    {
        ConsentType.DataProcessing => "Allow processing of your personal data for core service functionality.",
        ConsentType.Marketing => "Receive marketing communications and product updates.",
        ConsentType.Analytics => "Allow usage analytics to help improve the platform.",
        ConsentType.ThirdPartySharing => "Allow sharing data with approved third-party integrations.",
        _ => "Consent for data usage."
    };

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : null;
    }

    private Guid GetTenantId()
    {
        return _currentUserService.TenantId ?? Guid.Empty;
    }

    public record ConsentItem(string Type, string Description, bool IsGranted);
}

