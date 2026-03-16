using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Suppliers;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<CreateModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public string Code { get; set; } = string.Empty;
    [BindProperty] public string? ContactName { get; set; }
    [BindProperty] public string? ContactEmail { get; set; }
    [BindProperty] public string? ContactPhone { get; set; }
    [BindProperty] public string? Address { get; set; }
    [BindProperty] public string Category { get; set; } = "Raw Material";

    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
            ModelState.AddModelError(nameof(Name), "Name is required.");

        if (string.IsNullOrWhiteSpace(Code))
            ModelState.AddModelError(nameof(Code), "Code is required.");

        if (!ModelState.IsValid)
            return Page();

        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        // Check for duplicate code
        var exists = await _dbContext.Suppliers.AsNoTracking()
            .AnyAsync(s => s.TenantId == tenantId && s.Code == Code);

        if (exists)
        {
            ErrorMessage = $"A supplier with code '{Code}' already exists.";
            return Page();
        }

        var supplier = new Domain.Entities.Supplier.Supplier
        {
            Id = Guid.NewGuid(),
            Name = Name,
            Code = Code,
            ContactName = ContactName,
            ContactEmail = ContactEmail,
            ContactPhone = ContactPhone,
            Address = Address,
            Category = Category,
            QualificationStatus = SupplierQualificationStatus.Pending,
            IsActive = true,
            TenantId = tenantId,
            CreatedById = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Suppliers.Add(supplier);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Supplier {Id} ({Code}) created by {UserId}", supplier.Id, supplier.Code, currentUserId);
        return RedirectToPage("./Index");
    }
}
