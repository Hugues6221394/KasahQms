namespace KasahQMS.Domain.Enums;

/// <summary>
/// Types of stock movements that modify inventory levels.
/// Stock balance is DERIVED from movements - never directly edited.
/// </summary>
public enum StockMovementType
{
    /// <summary>Stock coming in (purchase, return, production)</summary>
    In = 0,
    
    /// <summary>Stock going out (sale, consumption, tender execution)</summary>
    Out = 1,
    
    /// <summary>Stock transfer between locations</summary>
    Transfer = 2,
    
    /// <summary>Inventory adjustment (loss, damage, correction)</summary>
    Adjustment = 3
}

/// <summary>
/// Status of a stock movement through approval workflow.
/// </summary>
public enum StockMovementStatus
{
    /// <summary>Movement initiated, pending approval</summary>
    Pending = 0,
    
    /// <summary>Movement approved and executed</summary>
    Approved = 1,
    
    /// <summary>Movement rejected</summary>
    Rejected = 2,
    
    /// <summary>Movement cancelled before execution</summary>
    Cancelled = 3
}

/// <summary>
/// Status of stock reservations (typically for tenders).
/// </summary>
public enum StockReservationStatus
{
    /// <summary>Stock is reserved and held</summary>
    Reserved = 0,
    
    /// <summary>Reserved stock has been issued/consumed</summary>
    Issued = 1,
    
    /// <summary>Reservation released back to available stock</summary>
    Released = 2,
    
    /// <summary>Reservation expired</summary>
    Expired = 3
}

/// <summary>
/// Status of stock items in the catalog.
/// </summary>
public enum StockItemStatus
{
    /// <summary>Item is active and can be used</summary>
    Active = 0,
    
    /// <summary>Item is temporarily inactive</summary>
    Inactive = 1,
    
    /// <summary>Item is discontinued</summary>
    Discontinued = 2
}

/// <summary>
/// Category of stock item.
/// </summary>
public enum StockItemCategory
{
    /// <summary>Physical product</summary>
    Product = 0,
    
    /// <summary>Service offering</summary>
    Service = 1,
    
    /// <summary>Raw material</summary>
    RawMaterial = 2,
    
    /// <summary>Finished goods</summary>
    FinishedGoods = 3,
    
    /// <summary>Consumable supplies</summary>
    Consumable = 4,
    
    /// <summary>Equipment/Asset</summary>
    Equipment = 5
}
