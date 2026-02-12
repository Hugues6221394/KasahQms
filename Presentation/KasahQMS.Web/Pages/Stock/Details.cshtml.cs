using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Stock;
using KasahQMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KasahQMS.Web.Pages.Stock;

/// <summary>
/// Stock item details page.
/// All authenticated users can view stock details.
/// </summary>
[Authorize]
public class DetailsModel : PageModel
{
    private readonly IStockService _stockService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        IStockService stockService,
        ICurrentUserService currentUserService,
        ILogger<DetailsModel> logger)
    {
        _stockService = stockService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Message { get; set; }

    public StockItemDetail? StockItem { get; set; }
    public List<MovementRow> RecentMovements { get; set; } = new();
    public List<ReservationRow> ActiveReservations { get; set; } = new();
    public bool CanManageStock { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Unauthorized();

        CanManageStock = await _stockService.CanManageStockAsync(userId.Value);

        var item = await _stockService.GetStockItemAsync(Id);
        if (item == null)
        {
            ErrorMessage = "Stock item not found.";
            return Page();
        }

        // Get balance
        var balance = await _stockService.GetStockBalanceAsync(Id);
        var available = await _stockService.GetAvailableStockAsync(Id);
        var reserved = balance - available;

        StockItem = new StockItemDetail(
            item.Id,
            item.SKU,
            item.Name,
            item.Description,
            item.Category.ToString(),
            item.UnitOfMeasure,
            balance,
            reserved,
            available,
            item.UnitCost,
            item.UnitPrice,
            item.Status.ToString(),
            GetStatusClass(item.Status),
            item.MinimumStockLevel,
            item.ReorderPoint,
            item.IsService,
            item.TrackInventory,
            item.CreatedAt,
            balance < item.MinimumStockLevel,
            balance <= item.ReorderPoint && item.ReorderPoint > 0);

        // Get recent movements
        var movements = await _stockService.GetMovementHistoryAsync(itemId: Id, limit: 10);
        RecentMovements = movements.Select(m => new MovementRow(
            m.Id,
            m.MovementNumber,
            m.MovementType.ToString(),
            GetMovementTypeClass(m.MovementType),
            m.Quantity,
            m.FromLocation?.Name,
            m.ToLocation?.Name,
            m.Reason,
            m.Status.ToString(),
            GetMovementStatusClass(m.Status),
            m.InitiatedBy?.FullName ?? "System",
            m.CreatedAt)).ToList();

        // Get active reservations
        var reservations = await _stockService.GetActiveReservationsAsync(itemId: Id);
        ActiveReservations = reservations.Select(r => new ReservationRow(
            r.Id,
            r.ReservationNumber,
            r.QuantityReserved,
            r.QuantityIssued,
            r.QuantityRemaining,
            r.Purpose,
            r.ExternalReference,
            r.Status.ToString(),
            r.ExpiresAt,
            r.CreatedAt)).ToList();

        _logger.LogInformation("Stock item {SKU} details viewed by user {UserId}", item.SKU, userId);
        return Page();
    }

    private static string GetStatusClass(StockItemStatus status)
    {
        return status switch
        {
            StockItemStatus.Active => "bg-emerald-100 text-emerald-700",
            StockItemStatus.Inactive => "bg-amber-100 text-amber-700",
            StockItemStatus.Discontinued => "bg-slate-100 text-slate-600",
            _ => "bg-slate-100 text-slate-600"
        };
    }

    private static string GetMovementTypeClass(StockMovementType type)
    {
        return type switch
        {
            StockMovementType.In => "bg-emerald-100 text-emerald-700",
            StockMovementType.Out => "bg-red-100 text-red-700",
            StockMovementType.Transfer => "bg-blue-100 text-blue-700",
            StockMovementType.Adjustment => "bg-amber-100 text-amber-700",
            _ => "bg-slate-100 text-slate-600"
        };
    }

    private static string GetMovementStatusClass(StockMovementStatus status)
    {
        return status switch
        {
            StockMovementStatus.Approved => "bg-emerald-100 text-emerald-700",
            StockMovementStatus.Pending => "bg-amber-100 text-amber-700",
            StockMovementStatus.Rejected => "bg-red-100 text-red-700",
            StockMovementStatus.Cancelled => "bg-slate-100 text-slate-600",
            _ => "bg-slate-100 text-slate-600"
        };
    }

    public record StockItemDetail(
        Guid Id,
        string SKU,
        string Name,
        string? Description,
        string Category,
        string UnitOfMeasure,
        decimal Balance,
        decimal Reserved,
        decimal Available,
        decimal UnitCost,
        decimal UnitPrice,
        string Status,
        string StatusClass,
        decimal MinimumLevel,
        decimal ReorderPoint,
        bool IsService,
        bool TrackInventory,
        DateTime CreatedAt,
        bool IsBelowMinimum,
        bool IsAtReorderPoint);

    public record MovementRow(
        Guid Id,
        string MovementNumber,
        string Type,
        string TypeClass,
        decimal Quantity,
        string? FromLocation,
        string? ToLocation,
        string Reason,
        string Status,
        string StatusClass,
        string InitiatedBy,
        DateTime CreatedAt);

    public record ReservationRow(
        Guid Id,
        string ReservationNumber,
        decimal Quantity,
        decimal Issued,
        decimal Remaining,
        string Purpose,
        string? ExternalReference,
        string Status,
        DateTime? ExpiresAt,
        DateTime CreatedAt);
}
