using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KasahQMS.Web.Pages.Stock;

/// <summary>
/// Stock index page displaying all stock items with filtering.
/// All authenticated users can view stock.
/// </summary>
[Authorize]
public class IndexModel : PageModel
{
    private readonly IStockService _stockService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IStockService stockService,
        ICurrentUserService currentUserService,
        ILogger<IndexModel> logger)
    {
        _stockService = stockService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public StockItemStatus? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public StockItemCategory? Category { get; set; }

    public bool CanManageStock { get; set; }
    public List<StockItemRow> StockItems { get; set; } = new();
    public StockSummary Summary { get; set; } = new StockSummary(0, 0, 0, 0);

    public async Task OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return;

        CanManageStock = await _stockService.CanManageStockAsync(userId.Value);

        // Get stock items
        var items = await _stockService.GetStockItemsAsync(Status, Category, SearchTerm);
        
        // Get balance summaries for all items
        var balanceSummaries = await _stockService.GetStockBalanceSummaryAsync();

        int totalItems = items.Count;
        int belowMinimum = 0;
        int atReorderPoint = 0;
        decimal totalValue = 0;

        StockItems = new List<StockItemRow>();
        
        foreach (var item in items)
        {
            var summary = balanceSummaries.FirstOrDefault(s => s.ItemId == item.Id);
            
            var balance = summary?.TotalBalance ?? 0;
            var reserved = summary?.ReservedQuantity ?? 0;
            var available = summary?.AvailableQuantity ?? 0;
            var isBelowMin = summary?.IsBelowMinimum ?? (balance < item.MinimumStockLevel);
            var isAtReorder = summary?.IsAtReorderPoint ?? (balance <= item.ReorderPoint && item.ReorderPoint > 0);
            var itemValue = summary?.TotalValue ?? (balance * item.UnitPrice);

            if (isBelowMin) belowMinimum++;
            if (isAtReorder) atReorderPoint++;
            totalValue += itemValue;

            StockItems.Add(new StockItemRow(
                item.Id,
                item.SKU,
                item.Name,
                item.Category.ToString(),
                item.UnitOfMeasure,
                balance,
                reserved,
                available,
                item.UnitPrice,
                item.Status.ToString(),
                GetStatusClass(item.Status),
                isBelowMin,
                isAtReorder));
        }

        Summary = new StockSummary(totalItems, belowMinimum, atReorderPoint, totalValue);

        _logger.LogInformation("Stock index viewed by user {UserId}. Total items: {Count}", userId, StockItems.Count);
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

    public record StockItemRow(
        Guid Id,
        string SKU,
        string Name,
        string Category,
        string UnitOfMeasure,
        decimal Balance,
        decimal Reserved,
        decimal Available,
        decimal UnitPrice,
        string Status,
        string StatusClass,
        bool IsBelowMinimum,
        bool IsAtReorderPoint);

    public record StockSummary(
        int TotalItems,
        int BelowMinimumCount,
        int AtReorderPointCount,
        decimal TotalValue);
}
