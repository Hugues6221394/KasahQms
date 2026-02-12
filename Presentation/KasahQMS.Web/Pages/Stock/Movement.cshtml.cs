using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace KasahQMS.Web.Pages.Stock;

/// <summary>
/// Record a new stock movement page.
/// Only TMD, Deputy, and Tender roles can record movements.
/// </summary>
[Authorize]
public class MovementModel : PageModel
{
    private readonly IStockService _stockService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MovementModel> _logger;

    public MovementModel(
        IStockService stockService,
        ICurrentUserService currentUserService,
        ILogger<MovementModel> logger)
    {
        _stockService = stockService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; } // Pre-selected item ID

    [BindProperty]
    [Required(ErrorMessage = "Stock item is required")]
    public Guid StockItemId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Movement type is required")]
    public StockMovementType MovementType { get; set; } = StockMovementType.In;

    [BindProperty]
    [Required(ErrorMessage = "Quantity is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    public decimal Quantity { get; set; }

    [BindProperty]
    public Guid? FromLocationId { get; set; }

    [BindProperty]
    public Guid? ToLocationId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Reason is required")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string Reason { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    public string? Notes { get; set; }

    [BindProperty]
    [StringLength(100, ErrorMessage = "Reference cannot exceed 100 characters")]
    public string? ExternalReference { get; set; }

    [BindProperty]
    public bool IsPositiveAdjustment { get; set; } = true;

    public bool CanManageStock { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public List<SelectListItem> StockItems { get; set; } = new();
    public List<SelectListItem> Locations { get; set; } = new();
    public string? SelectedItemName { get; set; }
    public decimal AvailableStock { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Unauthorized();

        CanManageStock = await _stockService.CanManageStockAsync(userId.Value);

        if (!CanManageStock)
        {
            ErrorMessage = "You do not have permission to record stock movements. Only TMD, Deputy, and Tender roles can manage stock.";
            return Page();
        }

        await LoadSelectListsAsync();

        // If item pre-selected, get its details
        if (Id.HasValue)
        {
            StockItemId = Id.Value;
            var item = await _stockService.GetStockItemAsync(Id.Value);
            if (item != null)
            {
                SelectedItemName = $"{item.SKU} - {item.Name}";
                AvailableStock = await _stockService.GetAvailableStockAsync(Id.Value);
            }
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
            ErrorMessage = "You do not have permission to record stock movements.";
            await LoadSelectListsAsync();
            return Page();
        }

        // Validate location requirements based on movement type
        if (MovementType == StockMovementType.In && !ToLocationId.HasValue)
        {
            ModelState.AddModelError(nameof(ToLocationId), "Destination location is required for Stock In");
        }
        if (MovementType == StockMovementType.Out && !FromLocationId.HasValue)
        {
            ModelState.AddModelError(nameof(FromLocationId), "Source location is required for Stock Out");
        }
        if (MovementType == StockMovementType.Transfer)
        {
            if (!FromLocationId.HasValue)
                ModelState.AddModelError(nameof(FromLocationId), "Source location is required for Transfer");
            if (!ToLocationId.HasValue)
                ModelState.AddModelError(nameof(ToLocationId), "Destination location is required for Transfer");
        }
        if (MovementType == StockMovementType.Adjustment)
        {
            if (!FromLocationId.HasValue && !ToLocationId.HasValue)
                ModelState.AddModelError("", "Location is required for Adjustment");
        }

        if (!ModelState.IsValid)
        {
            await LoadSelectListsAsync();
            return Page();
        }

        try
        {
            switch (MovementType)
            {
                case StockMovementType.In:
                    await _stockService.CreateInMovementAsync(
                        StockItemId,
                        Quantity,
                        ToLocationId!.Value,
                        Reason,
                        false, // Auto-approve for now
                        Notes,
                        ExternalReference);
                    break;

                case StockMovementType.Out:
                    await _stockService.CreateOutMovementAsync(
                        StockItemId,
                        Quantity,
                        FromLocationId!.Value,
                        Reason,
                        false, // Auto-approve for now
                        Notes);
                    break;

                case StockMovementType.Transfer:
                    await _stockService.CreateTransferMovementAsync(
                        StockItemId,
                        Quantity,
                        FromLocationId!.Value,
                        ToLocationId!.Value,
                        Reason,
                        false, // Auto-approve for now
                        Notes);
                    break;

                case StockMovementType.Adjustment:
                    var locationId = IsPositiveAdjustment ? ToLocationId!.Value : FromLocationId!.Value;
                    await _stockService.CreateAdjustmentAsync(
                        StockItemId,
                        Quantity,
                        locationId,
                        IsPositiveAdjustment,
                        Reason,
                        Notes);
                    break;
            }

            _logger.LogInformation("Stock movement {Type} recorded for item {ItemId} by user {UserId}",
                MovementType, StockItemId, userId);

            return RedirectToPage("./Details", new { id = StockItemId, message = "Stock movement recorded successfully." });
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            await LoadSelectListsAsync();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording stock movement");
            ErrorMessage = "An error occurred while recording the movement. Please try again.";
            await LoadSelectListsAsync();
            return Page();
        }
    }

    private async Task LoadSelectListsAsync()
    {
        var items = await _stockService.GetStockItemsAsync(StockItemStatus.Active);
        StockItems = items.Select(i => new SelectListItem($"{i.SKU} - {i.Name}", i.Id.ToString())).ToList();

        var locations = await _stockService.GetLocationsAsync(activeOnly: true);
        Locations = locations.Select(l => new SelectListItem(l.Name, l.Id.ToString())).ToList();
    }
}
