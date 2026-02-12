using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Stock;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Services;

/// <summary>
/// Stock management service implementation.
/// CRITICAL: Stock balance is DERIVED from movements - never directly edited.
/// All stock changes MUST go through movement operations.
/// </summary>
public class StockService : IStockService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<StockService> _logger;

    public StockService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<StockService> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private Guid GetTenantId() => _currentUserService.TenantId 
        ?? _dbContext.Tenants.Select(t => t.Id).FirstOrDefault();

    private Guid GetUserId() => _currentUserService.UserId 
        ?? throw new UnauthorizedAccessException("User not authenticated");

    private async Task<string> GenerateMovementNumberAsync()
    {
        var date = DateTime.UtcNow;
        var prefix = $"MOV-{date:yyyyMMdd}-";
        var lastMovement = await _dbContext.StockMovements
            .Where(m => m.MovementNumber.StartsWith(prefix))
            .OrderByDescending(m => m.MovementNumber)
            .FirstOrDefaultAsync();
        
        var sequence = 1;
        if (lastMovement != null)
        {
            var lastSequence = lastMovement.MovementNumber.Replace(prefix, "");
            if (int.TryParse(lastSequence, out var lastSeq))
                sequence = lastSeq + 1;
        }
        
        return $"{prefix}{sequence:D4}";
    }

    private async Task<string> GenerateReservationNumberAsync()
    {
        var date = DateTime.UtcNow;
        var prefix = $"RES-{date:yyyyMMdd}-";
        var lastReservation = await _dbContext.StockReservations
            .Where(r => r.ReservationNumber.StartsWith(prefix))
            .OrderByDescending(r => r.ReservationNumber)
            .FirstOrDefaultAsync();
        
        var sequence = 1;
        if (lastReservation != null)
        {
            var lastSequence = lastReservation.ReservationNumber.Replace(prefix, "");
            if (int.TryParse(lastSequence, out var lastSeq))
                sequence = lastSeq + 1;
        }
        
        return $"{prefix}{sequence:D4}";
    }

    #region Stock Item Operations

    public async Task<StockItem> CreateStockItemAsync(
        string sku, string name, StockItemCategory category, string unitOfMeasure,
        decimal unitCost, decimal unitPrice, string? description = null,
        bool isService = false, bool trackInventory = true, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();

        var existing = await _dbContext.StockItems
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SKU == sku, cancellationToken);
        
        if (existing != null)
            throw new InvalidOperationException($"Stock item with SKU '{sku}' already exists.");

        var item = StockItem.Create(tenantId, sku, name, category, unitOfMeasure,
            unitCost, unitPrice, userId, description, isService, trackInventory);

        _dbContext.StockItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stock item {SKU} created by user {UserId}", sku, userId);
        return item;
    }

    public async Task<StockItem> UpdateStockItemAsync(
        Guid itemId, string name, string? description, StockItemCategory category,
        string unitOfMeasure, decimal unitCost, decimal unitPrice, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var item = await _dbContext.StockItems.FindAsync(new object[] { itemId }, cancellationToken)
            ?? throw new InvalidOperationException("Stock item not found.");

        item.Update(name, description, category, unitOfMeasure, unitCost, unitPrice, userId);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stock item {SKU} updated by user {UserId}", item.SKU, userId);
        return item;
    }

    public async Task<StockItem?> GetStockItemAsync(Guid itemId, CancellationToken cancellationToken = default) =>
        await _dbContext.StockItems.AsNoTracking().FirstOrDefaultAsync(s => s.Id == itemId, cancellationToken);

    public async Task<StockItem?> GetStockItemBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await _dbContext.StockItems.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SKU == sku, cancellationToken);
    }

    public async Task<IReadOnlyList<StockItem>> GetStockItemsAsync(
        StockItemStatus? status = null, StockItemCategory? category = null,
        string? searchTerm = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _dbContext.StockItems.AsNoTracking().Where(s => s.TenantId == tenantId);

        if (status.HasValue) query = query.Where(s => s.Status == status.Value);
        if (category.HasValue) query = query.Where(s => s.Category == category.Value);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(s => s.SKU.ToLower().Contains(term) || s.Name.ToLower().Contains(term));
        }

        return await query.OrderBy(s => s.Name).ToListAsync(cancellationToken);
    }

    #endregion

    #region Stock Location Operations

    public async Task<StockLocation> CreateLocationAsync(
        string code, string name, string? description = null, string? address = null,
        bool isVirtual = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();

        var existing = await _dbContext.StockLocations
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Code == code, cancellationToken);
        
        if (existing != null)
            throw new InvalidOperationException($"Location with code '{code}' already exists.");

        var location = StockLocation.Create(tenantId, code, name, userId, description, address, isVirtual);
        _dbContext.StockLocations.Add(location);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stock location {Code} created by user {UserId}", code, userId);
        return location;
    }

    public async Task<IReadOnlyList<StockLocation>> GetLocationsAsync(
        bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _dbContext.StockLocations.AsNoTracking().Where(l => l.TenantId == tenantId);
        if (activeOnly) query = query.Where(l => l.IsActive);
        return await query.OrderBy(l => l.DisplayOrder).ThenBy(l => l.Name).ToListAsync(cancellationToken);
    }

    public async Task<StockLocation?> GetLocationAsync(Guid locationId, CancellationToken cancellationToken = default) =>
        await _dbContext.StockLocations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken);

    #endregion

    #region Stock Balance Operations

    public async Task<decimal> GetStockBalanceAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var inQty = await _dbContext.StockMovements
            .Where(m => m.StockItemId == itemId && m.Status == StockMovementStatus.Approved)
            .Where(m => m.MovementType == StockMovementType.In || (m.MovementType == StockMovementType.Adjustment && m.ToLocationId != null))
            .SumAsync(m => m.Quantity, cancellationToken);

        var outQty = await _dbContext.StockMovements
            .Where(m => m.StockItemId == itemId && m.Status == StockMovementStatus.Approved)
            .Where(m => m.MovementType == StockMovementType.Out || (m.MovementType == StockMovementType.Adjustment && m.FromLocationId != null))
            .SumAsync(m => m.Quantity, cancellationToken);

        return inQty - outQty;
    }

    public async Task<decimal> GetStockBalanceAtLocationAsync(Guid itemId, Guid locationId, CancellationToken cancellationToken = default)
    {
        var inQty = await _dbContext.StockMovements
            .Where(m => m.StockItemId == itemId && m.Status == StockMovementStatus.Approved && m.ToLocationId == locationId)
            .SumAsync(m => m.Quantity, cancellationToken);

        var outQty = await _dbContext.StockMovements
            .Where(m => m.StockItemId == itemId && m.Status == StockMovementStatus.Approved && m.FromLocationId == locationId)
            .SumAsync(m => m.Quantity, cancellationToken);

        return inQty - outQty;
    }

    public async Task<decimal> GetAvailableStockAsync(Guid itemId, Guid? locationId = null, CancellationToken cancellationToken = default)
    {
        var balance = locationId.HasValue
            ? await GetStockBalanceAtLocationAsync(itemId, locationId.Value, cancellationToken)
            : await GetStockBalanceAsync(itemId, cancellationToken);

        var reservedQuery = _dbContext.StockReservations
            .Where(r => r.StockItemId == itemId && r.Status == StockReservationStatus.Reserved);
        if (locationId.HasValue) reservedQuery = reservedQuery.Where(r => r.LocationId == locationId.Value);

        var reserved = await reservedQuery.SumAsync(r => r.QuantityReserved - r.QuantityIssued, cancellationToken);
        return balance - reserved;
    }

    public async Task<IReadOnlyList<StockBalanceSummary>> GetStockBalanceSummaryAsync(
        Guid? locationId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var items = await _dbContext.StockItems.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.Status == StockItemStatus.Active).ToListAsync(cancellationToken);

        var summaries = new List<StockBalanceSummary>();
        foreach (var item in items)
        {
            var balance = locationId.HasValue
                ? await GetStockBalanceAtLocationAsync(item.Id, locationId.Value, cancellationToken)
                : await GetStockBalanceAsync(item.Id, cancellationToken);

            var reserved = await _dbContext.StockReservations
                .Where(r => r.StockItemId == item.Id && r.Status == StockReservationStatus.Reserved)
                .SumAsync(r => r.QuantityReserved - r.QuantityIssued, cancellationToken);

            summaries.Add(new StockBalanceSummary(item.Id, item.SKU, item.Name, item.UnitOfMeasure,
                balance, reserved, balance - reserved, item.MinimumStockLevel, item.ReorderPoint,
                balance < item.MinimumStockLevel, item.ReorderPoint > 0 && balance <= item.ReorderPoint,
                balance * item.UnitPrice));
        }
        return summaries;
    }

    #endregion

    #region Stock Movement Operations

    public async Task<StockMovement> CreateInMovementAsync(
        Guid itemId, decimal quantity, Guid toLocationId, string reason,
        bool requiresApproval = false, string? notes = null, string? externalReference = null,
        Guid? relatedDocumentId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var item = await _dbContext.StockItems.FindAsync(new object[] { itemId }, cancellationToken)
            ?? throw new InvalidOperationException("Stock item not found.");

        var movementNumber = await GenerateMovementNumberAsync();
        var movement = StockMovement.CreateIn(tenantId, movementNumber, itemId, quantity, toLocationId,
            reason, item.UnitCost, userId, requiresApproval, notes, externalReference, relatedDocumentId);

        _dbContext.StockMovements.Add(movement);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stock IN {Number} created for {SKU}", movementNumber, item.SKU);
        return movement;
    }

    public async Task<StockMovement> CreateOutMovementAsync(
        Guid itemId, decimal quantity, Guid fromLocationId, string reason,
        bool requiresApproval = false, string? notes = null, Guid? relatedTenderId = null,
        Guid? relatedTaskId = null, Guid? reservationId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var item = await _dbContext.StockItems.FindAsync(new object[] { itemId }, cancellationToken)
            ?? throw new InvalidOperationException("Stock item not found.");

        var available = await GetAvailableStockAsync(itemId, fromLocationId, cancellationToken);
        if (available < quantity)
            throw new InvalidOperationException($"Insufficient stock. Available: {available}, Requested: {quantity}");

        var movementNumber = await GenerateMovementNumberAsync();
        var movement = StockMovement.CreateOut(tenantId, movementNumber, itemId, quantity, fromLocationId,
            reason, item.UnitCost, userId, requiresApproval, notes, relatedTenderId, relatedTaskId, reservationId);

        _dbContext.StockMovements.Add(movement);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stock OUT {Number} created for {SKU}", movementNumber, item.SKU);
        return movement;
    }

    public async Task<StockMovement> CreateTransferMovementAsync(
        Guid itemId, decimal quantity, Guid fromLocationId, Guid toLocationId, string reason,
        bool requiresApproval = true, string? notes = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var item = await _dbContext.StockItems.FindAsync(new object[] { itemId }, cancellationToken)
            ?? throw new InvalidOperationException("Stock item not found.");

        var available = await GetAvailableStockAsync(itemId, fromLocationId, cancellationToken);
        if (available < quantity)
            throw new InvalidOperationException($"Insufficient stock at source. Available: {available}");

        var movementNumber = await GenerateMovementNumberAsync();
        var movement = StockMovement.CreateTransfer(tenantId, movementNumber, itemId, quantity,
            fromLocationId, toLocationId, reason, item.UnitCost, userId, requiresApproval, notes);

        _dbContext.StockMovements.Add(movement);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stock TRANSFER {Number} created for {SKU}", movementNumber, item.SKU);
        return movement;
    }

    public async Task<StockMovement> CreateAdjustmentAsync(
        Guid itemId, decimal quantity, Guid locationId, bool isPositive, string reason,
        string? notes = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var item = await _dbContext.StockItems.FindAsync(new object[] { itemId }, cancellationToken)
            ?? throw new InvalidOperationException("Stock item not found.");

        if (!isPositive)
        {
            var balance = await GetStockBalanceAtLocationAsync(itemId, locationId, cancellationToken);
            if (balance < quantity)
                throw new InvalidOperationException($"Cannot adjust. Current balance: {balance}");
        }

        var movementNumber = await GenerateMovementNumberAsync();
        var movement = StockMovement.CreateAdjustment(tenantId, movementNumber, itemId, quantity,
            locationId, isPositive, reason, item.UnitCost, userId, notes);

        _dbContext.StockMovements.Add(movement);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stock ADJUSTMENT {Number} created for {SKU}", movementNumber, item.SKU);
        return movement;
    }

    public async Task<StockMovement> ApproveMovementAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var movement = await _dbContext.StockMovements.FindAsync(new object[] { movementId }, cancellationToken)
            ?? throw new InvalidOperationException("Movement not found.");

        movement.Approve(userId);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Movement {Number} approved", movement.MovementNumber);
        return movement;
    }

    public async Task<StockMovement> RejectMovementAsync(Guid movementId, string reason, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var movement = await _dbContext.StockMovements.FindAsync(new object[] { movementId }, cancellationToken)
            ?? throw new InvalidOperationException("Movement not found.");

        movement.Reject(userId, reason);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Movement {Number} rejected: {Reason}", movement.MovementNumber, reason);
        return movement;
    }

    public async Task<StockMovement?> GetMovementAsync(Guid movementId, CancellationToken cancellationToken = default) =>
        await _dbContext.StockMovements.AsNoTracking()
            .Include(m => m.StockItem).Include(m => m.FromLocation).Include(m => m.ToLocation)
            .Include(m => m.InitiatedBy).Include(m => m.ApprovedBy)
            .FirstOrDefaultAsync(m => m.Id == movementId, cancellationToken);

    public async Task<IReadOnlyList<StockMovement>> GetMovementHistoryAsync(
        Guid? itemId = null, Guid? locationId = null, StockMovementType? type = null,
        StockMovementStatus? status = null, DateTime? fromDate = null, DateTime? toDate = null,
        int limit = 100, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _dbContext.StockMovements.AsNoTracking()
            .Include(m => m.StockItem).Include(m => m.FromLocation).Include(m => m.ToLocation)
            .Include(m => m.InitiatedBy).Include(m => m.ApprovedBy)
            .Where(m => m.TenantId == tenantId);

        if (itemId.HasValue) query = query.Where(m => m.StockItemId == itemId.Value);
        if (locationId.HasValue) query = query.Where(m => m.FromLocationId == locationId.Value || m.ToLocationId == locationId.Value);
        if (type.HasValue) query = query.Where(m => m.MovementType == type.Value);
        if (status.HasValue) query = query.Where(m => m.Status == status.Value);
        if (fromDate.HasValue) query = query.Where(m => m.CreatedAt >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(m => m.CreatedAt <= toDate.Value);

        return await query.OrderByDescending(m => m.CreatedAt).Take(limit).ToListAsync(cancellationToken);
    }

    #endregion

    #region Stock Reservation Operations

    public async Task<StockReservation> ReserveStockAsync(
        Guid itemId, decimal quantity, Guid locationId, string purpose,
        Guid? tenderId = null, string? externalReference = null, DateTime? expiresAt = null,
        string? notes = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();

        var available = await GetAvailableStockAsync(itemId, locationId, cancellationToken);
        if (available < quantity)
            throw new InvalidOperationException($"Insufficient stock. Available: {available}");

        var reservationNumber = await GenerateReservationNumberAsync();
        var reservation = StockReservation.Create(tenantId, reservationNumber, itemId, locationId,
            quantity, purpose, userId, tenderId, externalReference, expiresAt, notes);

        _dbContext.StockReservations.Add(reservation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Reservation {Number} created for {Qty} units", reservationNumber, quantity);
        return reservation;
    }

    public async Task<StockMovement> IssueFromReservationAsync(
        Guid reservationId, decimal quantity, string reason, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        
        var reservation = await _dbContext.StockReservations.Include(r => r.StockItem)
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            ?? throw new InvalidOperationException("Reservation not found.");

        if (!reservation.CanIssue)
            throw new InvalidOperationException("Cannot issue from this reservation.");
        if (quantity > reservation.QuantityRemaining)
            throw new InvalidOperationException($"Cannot issue {quantity}. Only {reservation.QuantityRemaining} remaining.");

        var movementNumber = await GenerateMovementNumberAsync();
        var movement = StockMovement.CreateOut(tenantId, movementNumber, reservation.StockItemId,
            quantity, reservation.LocationId, reason, reservation.StockItem?.UnitCost ?? 0,
            userId, false, $"From reservation {reservation.ReservationNumber}",
            reservation.TenderId, null, reservationId);

        reservation.Issue(quantity, userId);
        _dbContext.StockMovements.Add(movement);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Issued {Qty} from reservation {Number}", quantity, reservation.ReservationNumber);
        return movement;
    }

    public async Task<StockReservation> ReleaseReservationAsync(
        Guid reservationId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var reservation = await _dbContext.StockReservations.FindAsync(new object[] { reservationId }, cancellationToken)
            ?? throw new InvalidOperationException("Reservation not found.");

        reservation.Release(userId, reason);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Reservation {Number} released", reservation.ReservationNumber);
        return reservation;
    }

    public async Task<StockReservation?> GetReservationAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
        await _dbContext.StockReservations.AsNoTracking()
            .Include(r => r.StockItem).Include(r => r.Location)
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

    public async Task<IReadOnlyList<StockReservation>> GetReservationsForTenderAsync(
        Guid tenderId, CancellationToken cancellationToken = default) =>
        await _dbContext.StockReservations.AsNoTracking()
            .Include(r => r.StockItem).Include(r => r.Location)
            .Where(r => r.TenderId == tenderId)
            .OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StockReservation>> GetActiveReservationsAsync(
        Guid? itemId = null, Guid? locationId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _dbContext.StockReservations.AsNoTracking()
            .Include(r => r.StockItem).Include(r => r.Location)
            .Where(r => r.TenantId == tenantId && r.Status == StockReservationStatus.Reserved);

        if (itemId.HasValue) query = query.Where(r => r.StockItemId == itemId.Value);
        if (locationId.HasValue) query = query.Where(r => r.LocationId == locationId.Value);

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
    }

    #endregion

    #region Authorization Helpers

    public async Task<bool> CanManageStockAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.AsNoTracking().Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null) return false;

        var allowedRoles = new[] { "TMD", "Deputy", "Tender", "Admin", "Administrator" };
        return user.Roles?.Any(r => allowedRoles.Contains(r.Name, StringComparer.OrdinalIgnoreCase)) ?? false;
    }

    public async Task<bool> CanViewStockAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        return user != null && user.IsActive;
    }

    #endregion
}

