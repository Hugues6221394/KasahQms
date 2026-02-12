using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KasahQMS.Web.Pages.Stock;

/// <summary>
/// Stock movements history and filtering page.
/// All authenticated users can view movements.
/// </summary>
[Authorize]
public class MovementsModel : PageModel
{
    private readonly IStockService _stockService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MovementsModel> _logger;

    public MovementsModel(
        IStockService stockService,
        ICurrentUserService currentUserService,
        ILogger<MovementsModel> logger)
    {
        _stockService = stockService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public Guid? ItemId { get; set; }

    [BindProperty(SupportsGet = true)]
    public StockMovementType? Type { get; set; }

    [BindProperty(SupportsGet = true)]
    public StockMovementStatus? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    public bool CanManageStock { get; set; }
    public List<MovementRow> Movements { get; set; } = new();
    public MovementStats Stats { get; set; } = new MovementStats(0, 0, 0, 0, 0);

    public async Task OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return;

        CanManageStock = await _stockService.CanManageStockAsync(userId.Value);

        var movements = await _stockService.GetMovementHistoryAsync(
            itemId: ItemId,
            type: Type,
            status: Status,
            fromDate: FromDate,
            toDate: ToDate,
            limit: 100);

        Movements = movements.Select(m => new MovementRow(
            m.Id,
            m.MovementNumber,
            m.StockItem?.SKU ?? "Unknown",
            m.StockItem?.Name ?? "Unknown",
            m.MovementType.ToString(),
            GetTypeClass(m.MovementType),
            m.Quantity,
            m.StockItem?.UnitOfMeasure ?? "PCS",
            m.FromLocation?.Name,
            m.ToLocation?.Name,
            m.Reason,
            m.Status.ToString(),
            GetStatusClass(m.Status),
            m.InitiatedBy?.FullName ?? "System",
            m.ApprovedBy?.FullName,
            m.TotalValue,
            m.CreatedAt,
            m.ApprovedAt)).ToList();

        // Calculate stats
        var approved = movements.Where(m => m.Status == StockMovementStatus.Approved).ToList();
        Stats = new MovementStats(
            movements.Count,
            approved.Where(m => m.MovementType == StockMovementType.In).Sum(m => m.Quantity),
            approved.Where(m => m.MovementType == StockMovementType.Out).Sum(m => m.Quantity),
            movements.Count(m => m.Status == StockMovementStatus.Pending),
            approved.Sum(m => m.TotalValue));

        _logger.LogInformation("Stock movements viewed by user {UserId}. Count: {Count}", userId, Movements.Count);
    }

    private static string GetTypeClass(StockMovementType type)
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

    private static string GetStatusClass(StockMovementStatus status)
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

    public record MovementRow(
        Guid Id,
        string MovementNumber,
        string ItemSKU,
        string ItemName,
        string Type,
        string TypeClass,
        decimal Quantity,
        string Unit,
        string? FromLocation,
        string? ToLocation,
        string Reason,
        string Status,
        string StatusClass,
        string InitiatedBy,
        string? ApprovedBy,
        decimal TotalValue,
        DateTime CreatedAt,
        DateTime? ApprovedAt);

    public record MovementStats(
        int TotalMovements,
        decimal TotalIn,
        decimal TotalOut,
        int PendingCount,
        decimal TotalValue);
}
