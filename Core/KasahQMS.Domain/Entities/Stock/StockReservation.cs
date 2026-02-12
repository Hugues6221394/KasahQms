using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Stock;

/// <summary>
/// Represents a stock reservation, typically for tenders.
/// Reserved stock is held and not available for other use until:
/// - Issued (tender executed)
/// - Released (tender cancelled or modified)
/// - Expired (reservation timeout)
/// </summary>
public class StockReservation : AuditableEntity
{
    /// <summary>Unique reservation reference number</summary>
    public string ReservationNumber { get; private set; } = string.Empty;
    
    /// <summary>The stock item being reserved</summary>
    public Guid StockItemId { get; private set; }
    
    /// <summary>Location where stock is reserved</summary>
    public Guid LocationId { get; private set; }
    
    /// <summary>Quantity reserved</summary>
    public decimal QuantityReserved { get; private set; }
    
    /// <summary>Quantity already issued from this reservation</summary>
    public decimal QuantityIssued { get; private set; }
    
    /// <summary>Remaining quantity (Reserved - Issued)</summary>
    public decimal QuantityRemaining => QuantityReserved - QuantityIssued;
    
    /// <summary>Status of the reservation</summary>
    public StockReservationStatus Status { get; private set; } = StockReservationStatus.Reserved;
    
    /// <summary>Related tender ID</summary>
    public Guid? TenderId { get; private set; }
    
    /// <summary>External reference (e.g., tender number, order number)</summary>
    public string? ExternalReference { get; private set; }
    
    /// <summary>Purpose/reason for the reservation</summary>
    public string Purpose { get; private set; } = string.Empty;
    
    /// <summary>When the reservation expires (if applicable)</summary>
    public DateTime? ExpiresAt { get; private set; }
    
    /// <summary>User who created the reservation</summary>
    public Guid ReservedById { get; private set; }
    
    /// <summary>User who issued stock from this reservation</summary>
    public Guid? IssuedById { get; private set; }
    
    /// <summary>When stock was issued</summary>
    public DateTime? IssuedAt { get; private set; }
    
    /// <summary>User who released the reservation</summary>
    public Guid? ReleasedById { get; private set; }
    
    /// <summary>When the reservation was released</summary>
    public DateTime? ReleasedAt { get; private set; }
    
    /// <summary>Reason for release (if cancelled)</summary>
    public string? ReleaseReason { get; private set; }
    
    /// <summary>Notes</summary>
    public string? Notes { get; private set; }
    
    // Navigation properties
    public virtual StockItem? StockItem { get; set; }
    public virtual StockLocation? Location { get; set; }
    public virtual User? ReservedBy { get; set; }
    public virtual ICollection<StockMovement>? Movements { get; set; }
    
    private StockReservation() { }
    
    /// <summary>
    /// Create a new stock reservation.
    /// </summary>
    public static StockReservation Create(
        Guid tenantId,
        string reservationNumber,
        Guid stockItemId,
        Guid locationId,
        decimal quantity,
        string purpose,
        Guid reservedById,
        Guid? tenderId = null,
        string? externalReference = null,
        DateTime? expiresAt = null,
        string? notes = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));
            
        return new StockReservation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReservationNumber = reservationNumber,
            StockItemId = stockItemId,
            LocationId = locationId,
            QuantityReserved = quantity,
            QuantityIssued = 0,
            Status = StockReservationStatus.Reserved,
            TenderId = tenderId,
            ExternalReference = externalReference,
            Purpose = purpose,
            ExpiresAt = expiresAt,
            ReservedById = reservedById,
            Notes = notes,
            CreatedById = reservedById,
            CreatedAt = DateTime.UtcNow,
            Movements = new List<StockMovement>()
        };
    }
    
    /// <summary>
    /// Issue stock from this reservation (partial or full).
    /// </summary>
    public void Issue(decimal quantity, Guid issuedById)
    {
        if (Status != StockReservationStatus.Reserved)
            throw new InvalidOperationException($"Cannot issue from reservation in status {Status}");
            
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));
            
        if (quantity > QuantityRemaining)
            throw new InvalidOperationException($"Cannot issue {quantity}. Only {QuantityRemaining} remaining.");
            
        QuantityIssued += quantity;
        
        // If fully issued, mark as Issued
        if (QuantityRemaining <= 0)
        {
            Status = StockReservationStatus.Issued;
            IssuedAt = DateTime.UtcNow;
        }
        
        IssuedById = issuedById;
        LastModifiedById = issuedById;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Release the reservation (cancel and return stock to available).
    /// </summary>
    public void Release(Guid releasedById, string? reason = null)
    {
        if (Status != StockReservationStatus.Reserved)
            throw new InvalidOperationException($"Cannot release reservation in status {Status}");
            
        Status = StockReservationStatus.Released;
        ReleasedById = releasedById;
        ReleasedAt = DateTime.UtcNow;
        ReleaseReason = reason;
        LastModifiedById = releasedById;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Mark the reservation as expired.
    /// </summary>
    public void Expire()
    {
        if (Status != StockReservationStatus.Reserved)
            throw new InvalidOperationException($"Cannot expire reservation in status {Status}");
            
        Status = StockReservationStatus.Expired;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Check if the reservation is expired.
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow && Status == StockReservationStatus.Reserved;
    
    /// <summary>
    /// Check if stock can still be issued from this reservation.
    /// </summary>
    public bool CanIssue => Status == StockReservationStatus.Reserved && QuantityRemaining > 0 && !IsExpired;
}
