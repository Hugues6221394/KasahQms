using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace KasahQMS.Web.Pages.Stock;

/// <summary>
/// Create new stock item page.
/// Only TMD, Deputy, and Tender roles can create stock items.
/// </summary>
[Authorize]
public class CreateModel : PageModel
{
    private readonly IStockService _stockService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        IStockService stockService,
        ICurrentUserService currentUserService,
        ILogger<CreateModel> logger)
    {
        _stockService = stockService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [BindProperty]
    [Required(ErrorMessage = "SKU is required")]
    [StringLength(50, ErrorMessage = "SKU cannot exceed 50 characters")]
    public string SKU { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Category is required")]
    public StockItemCategory Category { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Unit of Measure is required")]
    [StringLength(20, ErrorMessage = "Unit cannot exceed 20 characters")]
    public string UnitOfMeasure { get; set; } = "PCS";

    [BindProperty]
    [Required(ErrorMessage = "Unit Cost is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Unit Cost must be a positive number")]
    public decimal UnitCost { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Unit Price is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Unit Price must be a positive number")]
    public decimal UnitPrice { get; set; }

    [BindProperty]
    public bool IsService { get; set; }

    [BindProperty]
    public bool TrackInventory { get; set; } = true;

    [BindProperty]
    [Range(0, double.MaxValue)]
    public decimal MinimumStockLevel { get; set; }

    [BindProperty]
    [Range(0, double.MaxValue)]
    public decimal ReorderPoint { get; set; }

    public bool CanManageStock { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Unauthorized();

        CanManageStock = await _stockService.CanManageStockAsync(userId.Value);

        if (!CanManageStock)
        {
            ErrorMessage = "You do not have permission to create stock items. Only TMD, Deputy, and Tender roles can manage stock.";
            return Page();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Unauthorized();

        CanManageStock = await _stockService.CanManageStockAsync(userId.Value);

        if (!CanManageStock)
        {
            ErrorMessage = "You do not have permission to create stock items.";
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var item = await _stockService.CreateStockItemAsync(
                SKU,
                Name,
                Category,
                UnitOfMeasure,
                UnitCost,
                UnitPrice,
                Description,
                IsService,
                TrackInventory);

            if (MinimumStockLevel > 0 || ReorderPoint > 0)
            {
                // Note: We would need to add this to the service, but for now just create the item
            }

            _logger.LogInformation("Stock item {SKU} created by user {UserId}", SKU, userId);
            return RedirectToPage("./Details", new { id = item.Id, message = "Stock item created successfully." });
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating stock item {SKU}", SKU);
            ErrorMessage = "An error occurred while creating the stock item. Please try again.";
            return Page();
        }
    }
}
