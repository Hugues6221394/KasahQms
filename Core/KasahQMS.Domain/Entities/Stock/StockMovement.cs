using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Stock;

/// <summary>
/// Represents a stock movement (the ONLY way to change stock levels).
/// CRITICAL: Stock balance is DERIVED from movements - never directly edited.
/// All movements are immutable once approved - history cannot be changed.
/// </summary>
public class StockMovement : AuditableEntity
{
    /// <summary>Unique movement reference number</summary>
    public string MovementNumber { get; private set; } = string.Empty;
    
    /// <summary>Type of movement (In, Out, Transfer, Adjustment)</summary>
    public StockMovementType MovementType { get; private set; }
    
    /// <summary>Status of the movement</summary>
    public StockMovementStatus Status { get; private set; } = StockMovementStatus.Pending;
    
    /// <summary>The stock item being moved</summary>
    public Guid StockItemId { get; private set; }
    
    /// <summary>Quantity being moved (always positive)</summary>
    public decimal Quantity { get; private set; }
    
    /// <summary>Source location (for Out, Transfer)</summary>
    public Guid? FromLocationId { get; private set; }
    
    /// <summary>Destination location (for In, Transfer)</summary>
    public Guid? ToLocationId { get; private set; }
    
    /// <summary>Reason for the movement</summary>
    public string Reason { get; private set; } = string.Empty;
    
    /// <summary>Additional notes</summary>
    public string? Notes { get; private set; }
    
    /// <summary>Reference to related tender (if applicable)</summary>
    public Guid? RelatedTenderId { get; private set; }
    
    /// <summary>Reference to related task (if applicable)</summary>
    public Guid? RelatedTaskId { get; private set; }
    
    /// <summary>Reference to related document (if applicable)</summary>
    public Guid? RelatedDocumentId { get; private set; }
    
    /// <summary>Reference to reservation being fulfilled (if applicable)</summary>
    public Guid? ReservationId { get; private set; }
    
    /// <summary>User who initiated the movement</summary>
    public Guid InitiatedById { get; private set; }
    
    /// <summary>User who approved the movement (null if auto-approved or pending)</summary>
    public Guid? ApprovedById { get; private set; }
    
    /// <summary>When the movement was approved</summary>
    public DateTime? ApprovedAt { get; private set; }
    
    /// <summary>User who rejected the movement (if rejected)</summary>
    public Guid? RejectedById { get; private set; }
    
    /// <summary>When the movement was rejected</summary>
    public DateTime? RejectedAt { get; private set; }
    
    /// <summary>Rejection reason</summary>
    public string? RejectionReason { get; private set; }
    
    /// <summary>Unit cost at time of movement (for valuation)</summary>
    public decimal UnitCostAtMovement { get; private set; }
    
    /// <summary>Total value of the movement</summary>
    public decimal TotalValue => Quantity * UnitCostAtMovement;
    
    /// <summary>Whether this movement requires approval</summary>
    public bool RequiresApproval { get; private set; }
    
    /// <summary>External reference (e.g., PO number, invoice number)</summary>
    public string? ExternalReference { get; private set; }
    
    // Navigation properties
    public virtual StockItem? StockItem { get; set; }
    public virtual StockLocation? FromLocation { get; set; }
    public virtual StockLocation? ToLocation { get; set; }
    public virtual User? InitiatedBy { get; set; }
    public virtual User? ApprovedBy { get; set; }
    public virtual StockReservation? Reservation { get; set; }
    
    private StockMovement() { }
    
    /// <summary>
    /// Create a stock IN movement (receiving stock).
    /// </summary>
    public static StockMovement CreateIn(
        Guid tenantId,
        string movementNumber,
        Guid stockItemId,
        decimal quantity,
        Guid toLocationId,
        string reason,
        decimal unitCost,
        Guid initiatedById,
        bool requiresApproval = false,
        string? notes = null,
        string? externalReference = null,
        Guid? relatedDocumentId = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));
            
        return new StockMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MovementNumber = movementNumber,
            MovementType = StockMovementType.In,
            Status = requiresApproval ? StockMovementStatus.Pending : StockMovementStatus.Approved,
            StockItemId = stockItemId,
            Quantity = quantity,
            ToLocationId = toLocationId,
            Reason = reason,
            Notes = notes,
            UnitCostAtMovement = unitCost,
            InitiatedById = initiatedById,
            RequiresApproval = requiresApproval,
            ExternalReference = externalReference,
            RelatedDocumentId = relatedDocumentId,
            CreatedById = initiatedById,
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = requiresApproval ? null : DateTime.UtcNow,
            ApprovedById = requiresApproval ? null : initiatedById
        };
    }
    
    /// <summary>
    /// Create a stock OUT movement (issuing stock).
    /// </summary>
    public static StockMovement CreateOut(
        Guid tenantId,
        string movementNumber,
        Guid stockItemId,
        decimal quantity,
        Guid fromLocationId,
        string reason,
        decimal unitCost,
        Guid initiatedById,
        bool requiresApproval = false,
        string? notes = null,
        Guid? relatedTenderId = null,
        Guid? relatedTaskId = null,
        Guid? reservationId = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));
            
        return new StockMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MovementNumber = movementNumber,
            MovementType = StockMovementType.Out,
            Status = requiresApproval ? StockMovementStatus.Pending : StockMovementStatus.Approved,
            StockItemId = stockItemId,
            Quantity = quantity,
            FromLocationId = fromLocationId,
            Reason = reason,
            Notes = notes,
            UnitCostAtMovement = unitCost,
            InitiatedById = initiatedById,
            RequiresApproval = requiresApproval,
            RelatedTenderId = relatedTenderId,
            RelatedTaskId = relatedTaskId,
            ReservationId = reservationId,
            CreatedById = initiatedById,
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = requiresApproval ? null : DateTime.UtcNow,
            ApprovedById = requiresApproval ? null : initiatedById
        };
    }
    
    /// <summary>
    /// Create a stock TRANSFER movement (between locations).
    /// </summary>
    public static StockMovement CreateTransfer(
        Guid tenantId,
        string movementNumber,
        Guid stockItemId,
        decimal quantity,
        Guid fromLocationId,
        Guid toLocationId,
        string reason,
        decimal unitCost,
        Guid initiatedById,
        bool requiresApproval = true,
        string? notes = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));
        if (fromLocationId == toLocationId)
            throw new ArgumentException("From and To locations must be different", nameof(toLocationId));
            
        return new StockMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MovementNumber = movementNumber,
            MovementType = StockMovementType.Transfer,
            Status = requiresApproval ? StockMovementStatus.Pending : StockMovementStatus.Approved,
            StockItemId = stockItemId,
            Quantity = quantity,
            FromLocationId = fromLocationId,
            ToLocationId = toLocationId,
            Reason = reason,
            Notes = notes,
            UnitCostAtMovement = unitCost,
            InitiatedById = initiatedById,
            RequiresApproval = requiresApproval,
            CreatedById = initiatedById,
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = requiresApproval ? null : DateTime.UtcNow,
            ApprovedById = requiresApproval ? null : initiatedById
        };
    }
    
    /// <summary>
    /// Create a stock ADJUSTMENT movement (correction, loss, damage).
    /// Adjustments always require approval.
    /// </summary>
    public static StockMovement CreateAdjustment(
        Guid tenantId,
        string movementNumber,
        Guid stockItemId,
        decimal quantity,
        Guid locationId,
        bool isPositive,
        string reason,
        decimal unitCost,
        Guid initiatedById,
        string? notes = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));
            
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MovementNumber = movementNumber,
            MovementType = StockMovementType.Adjustment,
            Status = StockMovementStatus.Pending, // Adjustments always require approval
            StockItemId = stockItemId,
            Quantity = quantity,
            Reason = reason,
            Notes = notes,
            UnitCostAtMovement = unitCost,
            InitiatedById = initiatedById,
            RequiresApproval = true,
            CreatedById = initiatedById,
            CreatedAt = DateTime.UtcNow
        };
        
        // Positive adjustment = stock coming in to location
        // Negative adjustment = stock going out from location
        if (isPositive)
        {
            movement.ToLocationId = locationId;
        }
        else
        {
            movement.FromLocationId = locationId;
        }
        
        return movement;
    }
    
    /// <summary>
    /// Approve the movement. Once approved, the movement is immutable.
    /// </summary>
    public void Approve(Guid approvedById)
    {
        if (Status != StockMovementStatus.Pending)
            throw new InvalidOperationException($"Cannot approve movement in status {Status}");
            
        Status = StockMovementStatus.Approved;
        ApprovedById = approvedById;
        ApprovedAt = DateTime.UtcNow;
        LastModifiedById = approvedById;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Reject the movement.
    /// </summary>
    public void Reject(Guid rejectedById, string reason)
    {
        if (Status != StockMovementStatus.Pending)
            throw new InvalidOperationException($"Cannot reject movement in status {Status}");
            
        Status = StockMovementStatus.Rejected;
        RejectedById = rejectedById;
        RejectedAt = DateTime.UtcNow;
        RejectionReason = reason;
        LastModifiedById = rejectedById;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Cancel a pending movement.
    /// </summary>
    public void Cancel(Guid cancelledById, string reason)
    {
        if (Status != StockMovementStatus.Pending)
            throw new InvalidOperationException($"Cannot cancel movement in status {Status}");
            
        Status = StockMovementStatus.Cancelled;
        Notes = string.IsNullOrEmpty(Notes) ? $"Cancelled: {reason}" : $"{Notes}\n\nCancelled: {reason}";
        LastModifiedById = cancelledById;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Gets the effective quantity change for stock balance calculation.
    /// Positive = increases stock, Negative = decreases stock.
    /// </summary>
    public decimal GetEffectiveQuantity(Guid? atLocationId = null)
    {
        if (Status != StockMovementStatus.Approved)
            return 0;
            
        return MovementType switch
        {
            StockMovementType.In => Quantity,
            StockMovementType.Out => -Quantity,
            StockMovementType.Transfer when atLocationId == ToLocationId => Quantity,
            StockMovementType.Transfer when atLocationId == FromLocationId => -Quantity,
            StockMovementType.Adjustment when ToLocationId.HasValue => Quantity,
            StockMovementType.Adjustment when FromLocationId.HasValue => -Quantity,
            _ => 0
        };
    }
}
