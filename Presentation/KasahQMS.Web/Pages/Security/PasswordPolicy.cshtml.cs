using System.Security.Claims;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KasahQMS.Web.Pages.Security;

[Authorize(Roles = "System Admin,SystemAdmin,Admin,TenantAdmin")]
public class PasswordPolicyModel : PageModel
{
    private readonly IPasswordPolicyService _policyService;
    private readonly ICurrentUserService _currentUserService;

    public PasswordPolicyModel(
        IPasswordPolicyService policyService,
        ICurrentUserService currentUserService)
    {
        _policyService = policyService;
        _currentUserService = currentUserService;
    }

    [BindProperty]
    public int MinLength { get; set; }

    [BindProperty]
    public int MaxLength { get; set; }

    [BindProperty]
    public bool RequireUppercase { get; set; }

    [BindProperty]
    public bool RequireLowercase { get; set; }

    [BindProperty]
    public bool RequireDigit { get; set; }

    [BindProperty]
    public bool RequireSpecialChar { get; set; }

    [BindProperty]
    public int PreventReuse { get; set; }

    [BindProperty]
    public int MaxAgeDays { get; set; }

    [BindProperty]
    public int LockoutThreshold { get; set; }

    [BindProperty]
    public int LockoutDurationMinutes { get; set; }

    public List<string> PolicyRules { get; set; } = new();
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadPolicyAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty)
        {
            ErrorMessage = "Unable to determine tenant.";
            BuildPolicyRules();
            return Page();
        }

        var policy = await _policyService.GetPolicyAsync(tenantId);
        policy.MinLength = MinLength;
        policy.MaxLength = MaxLength;
        policy.RequireUppercase = RequireUppercase;
        policy.RequireLowercase = RequireLowercase;
        policy.RequireDigit = RequireDigit;
        policy.RequireSpecialChar = RequireSpecialChar;
        policy.PreventReuse = PreventReuse;
        policy.MaxAgeDays = MaxAgeDays;
        policy.LockoutThreshold = LockoutThreshold;
        policy.LockoutDurationMinutes = LockoutDurationMinutes;

        StatusMessage = "Password policy updated successfully.";
        BuildPolicyRules();
        return Page();
    }

    private async Task LoadPolicyAsync()
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return;

        var policy = await _policyService.GetPolicyAsync(tenantId);
        MinLength = policy.MinLength;
        MaxLength = policy.MaxLength;
        RequireUppercase = policy.RequireUppercase;
        RequireLowercase = policy.RequireLowercase;
        RequireDigit = policy.RequireDigit;
        RequireSpecialChar = policy.RequireSpecialChar;
        PreventReuse = policy.PreventReuse;
        MaxAgeDays = policy.MaxAgeDays;
        LockoutThreshold = policy.LockoutThreshold;
        LockoutDurationMinutes = policy.LockoutDurationMinutes;

        BuildPolicyRules();
    }

    private void BuildPolicyRules()
    {
        PolicyRules = new List<string>();
        PolicyRules.Add($"Minimum {MinLength} characters, maximum {MaxLength} characters");
        if (RequireUppercase) PolicyRules.Add("At least one uppercase letter (A-Z)");
        if (RequireLowercase) PolicyRules.Add("At least one lowercase letter (a-z)");
        if (RequireDigit) PolicyRules.Add("At least one digit (0-9)");
        if (RequireSpecialChar) PolicyRules.Add("At least one special character (!@#$...)");
        if (PreventReuse > 0) PolicyRules.Add($"Cannot reuse last {PreventReuse} passwords");
        if (MaxAgeDays > 0) PolicyRules.Add($"Password expires every {MaxAgeDays} days");
        if (LockoutThreshold > 0) PolicyRules.Add($"Account locks after {LockoutThreshold} failed attempts for {LockoutDurationMinutes} minutes");
    }

    private Guid GetTenantId()
    {
        return _currentUserService.TenantId ?? Guid.Empty;
    }
}
