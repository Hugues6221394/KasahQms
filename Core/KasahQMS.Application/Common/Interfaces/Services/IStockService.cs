using KasahQMS.Domain.Entities.Stock;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for stock management operations.
/// CRITICAL: Stock balance is DERIVED from movements - never directly edited.
/// All stock changes MUST go through movement operations.
/// </summary>
public interface IStockService
{
    #region Stock Item Operations
    
    /// <summary>Create a new stock item.</summary>
    Task<StockItem> CreateStockItemAsync(
        string sku,
        string name,
        StockItemCategory category,
        string unitOfMeasure,
        decimal unitCost,
        decimal unitPrice,
        string? description = null,
        bool isService = false,
        bool trackInventory = true,
        CancellationToken cancellationToken = default);
    
    /// <summary>Update stock item details.</summary>
    Task<StockItem> UpdateStockItemAsync(
        Guid itemId,
        string name,
        string? description,
        StockItemCategory category,
        string unitOfMeasure,
        decimal unitCost,
        decimal unitPrice,
        CancellationToken cancellationToken = default);
    
    /// <summary>Get stock item by ID.</summary>
    Task<StockItem?> GetStockItemAsync(Guid itemId, CancellationToken cancellationToken = default);
    
    /// <summary>Get stock item by SKU.</summary>
    Task<StockItem?> GetStockItemBySkuAsync(string sku, CancellationToken cancellationToken = default);
    
    /// <summary>Get all stock items with optional filtering.</summary>
    Task<IReadOnlyList<StockItem>> GetStockItemsAsync(
        StockItemStatus? status = null,
        StockItemCategory? category = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Stock Location Operations
    
    /// <summary>Create a new stock location.</summary>
    Task<StockLocation> CreateLocationAsync(
        string code,
        string name,
        string? description = null,
        string? address = null,
        bool isVirtual = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>Get all locations.</summary>
    Task<IReadOnlyList<StockLocation>> GetLocationsAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default);
    
    /// <summary>Get location by ID.</summary>
    Task<StockLocation?> GetLocationAsync(Guid locationId, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Stock Balance Operations (READ-ONLY - Derived from Movements)
    
    /// <summary>
    /// Get current stock balance for an item across all locations.
    /// Balance is DERIVED from approved movements.
    /// </summary>
    Task<decimal> GetStockBalanceAsync(
        Guid itemId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get current stock balance for an item at a specific location.
    /// Balance is DERIVED from approved movements.
    /// </summary>
    Task<decimal> GetStockBalanceAtLocationAsync(
        Guid itemId,
        Guid locationId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get available stock (total - reserved) for an item.
    /// </summary>
    Task<decimal> GetAvailableStockAsync(
        Guid itemId,
        Guid? locationId = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get stock balance summary for all items.
    /// </summary>
    Task<IReadOnlyList<StockBalanceSummary>> GetStockBalanceSummaryAsync(
        Guid? locationId = null,
        CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Stock Movement Operations
    
    /// <summary>
    /// Create a stock IN movement (receiving stock).
    /// </summary>
    Task<StockMovement> CreateInMovementAsync(
        Guid itemId,
        decimal quantity,
        Guid toLocationId,
        string reason,
        bool requiresApproval = false,
        string? notes = null,
        string? externalReference = null,
        Guid? relatedDocumentId = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a stock OUT movement (issuing stock).
    /// Validates sufficient available stock before creating.
    /// </summary>
    Task<StockMovement> CreateOutMovementAsync(
        Guid itemId,
        decimal quantity,
        Guid fromLocationId,
        string reason,
        bool requiresApproval = false,
        string? notes = null,
        Guid? relatedTenderId = null,
        Guid? relatedTaskId = null,
        Guid? reservationId = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a stock TRANSFER movement (between locations).
    /// </summary>
    Task<StockMovement> CreateTransferMovementAsync(
        Guid itemId,
        decimal quantity,
        Guid fromLocationId,
        Guid toLocationId,
        string reason,
        bool requiresApproval = true,
        string? notes = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a stock ADJUSTMENT movement (correction).
    /// Adjustments always require approval.
    /// </summary>
    Task<StockMovement> CreateAdjustmentAsync(
        Guid itemId,
        decimal quantity,
        Guid locationId,
        bool isPositive,
        string reason,
        string? notes = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>Approve a pending movement.</summary>
    Task<StockMovement> ApproveMovementAsync(
        Guid movementId,
        CancellationToken cancellationToken = default);
    
    /// <summary>Reject a pending movement.</summary>
    Task<StockMovement> RejectMovementAsync(
        Guid movementId,
        string reason,
        CancellationToken cancellationToken = default);
    
    /// <summary>Get movement by ID.</summary>
    Task<StockMovement?> GetMovementAsync(
        Guid movementId,
        CancellationToken cancellationToken = default);
    
    /// <summary>Get movement history for an item.</summary>
    Task<IReadOnlyList<StockMovement>> GetMovementHistoryAsync(
        Guid? itemId = null,
        Guid? locationId = null,
        StockMovementType? type = null,
        StockMovementStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Stock Reservation Operations (for Tenders)
    
    /// <summary>
    /// Reserve stock for a tender.
    /// Validates sufficient available stock before reserving.
    /// </summary>
    Task<StockReservation> ReserveStockAsync(
        Guid itemId,
        decimal quantity,
        Guid locationId,
        string purpose,
        Guid? tenderId = null,
        string? externalReference = null,
        DateTime? expiresAt = null,
        string? notes = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Issue stock from a reservation (partial or full).
    /// Creates an OUT movement linked to the reservation.
    /// </summary>
    Task<StockMovement> IssueFromReservationAsync(
        Guid reservationId,
        decimal quantity,
        string reason,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Release a reservation (return reserved stock to available).
    /// </summary>
    Task<StockReservation> ReleaseReservationAsync(
        Guid reservationId,
        string? reason = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>Get reservation by ID.</summary>
    Task<StockReservation?> GetReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);
    
    /// <summary>Get reservations for a tender.</summary>
    Task<IReadOnlyList<StockReservation>> GetReservationsForTenderAsync(
        Guid tenderId,
        CancellationToken cancellationToken = default);
    
    /// <summary>Get active reservations for an item.</summary>
    Task<IReadOnlyList<StockReservation>> GetActiveReservationsAsync(
        Guid? itemId = null,
        Guid? locationId = null,
        CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Authorization Helpers
    
    /// <summary>
    /// Check if user can manage stock (create, issue, adjust).
    /// Only TMD, Deputy, and Tender roles can manage stock.
    /// </summary>
    Task<bool> CanManageStockAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if user can view stock (read-only access).
    /// All authorized users can view stock.
    /// </summary>
    Task<bool> CanViewStockAsync(Guid userId, CancellationToken cancellationToken = default);
    
    #endregion
}

/// <summary>
/// Stock balance summary for reporting.
/// </summary>
public record StockBalanceSummary(
    Guid ItemId,
    string SKU,
    string ItemName,
    string UnitOfMeasure,
    decimal TotalBalance,
    decimal ReservedQuantity,
    decimal AvailableQuantity,
    decimal MinimumLevel,
    decimal ReorderPoint,
    bool IsBelowMinimum,
    bool IsAtReorderPoint,
    decimal TotalValue);
