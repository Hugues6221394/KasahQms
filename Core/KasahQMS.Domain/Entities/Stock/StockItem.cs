using KasahQMS.Domain.Common;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Stock;

/// <summary>
/// Represents a product or service that can be tracked in inventory.
/// The actual stock quantity is DERIVED from StockMovements - never stored directly.
/// </summary>
public class StockItem : AuditableEntity
{
    /// <summary>Stock Keeping Unit - unique identifier for the item</summary>
    public string SKU { get; private set; } = string.Empty;
    
    /// <summary>Item name</summary>
    public string Name { get; private set; } = string.Empty;
    
    /// <summary>Detailed description</summary>
    public string? Description { get; private set; }
    
    /// <summary>Category of the item</summary>
    public StockItemCategory Category { get; private set; }
    
    /// <summary>Unit of measure (e.g., PCS, KG, L, Hours)</summary>
    public string UnitOfMeasure { get; private set; } = "PCS";
    
    /// <summary>Status of the item</summary>
    public StockItemStatus Status { get; private set; } = StockItemStatus.Active;
    
    /// <summary>Minimum stock level for alerts</summary>
    public decimal MinimumStockLevel { get; private set; }
    
    /// <summary>Maximum stock level for alerts</summary>
    public decimal? MaximumStockLevel { get; private set; }
    
    /// <summary>Reorder point trigger</summary>
    public decimal ReorderPoint { get; private set; }
    
    /// <summary>Standard reorder quantity</summary>
    public decimal ReorderQuantity { get; private set; }
    
    /// <summary>Unit cost for valuation</summary>
    public decimal UnitCost { get; private set; }
    
    /// <summary>Unit price for sales/tenders</summary>
    public decimal UnitPrice { get; private set; }
    
    /// <summary>Currency code (e.g., USD, RWF)</summary>
    public string CurrencyCode { get; private set; } = "RWF";
    
    /// <summary>Whether this is a service (non-physical)</summary>
    public bool IsService { get; private set; }
    
    /// <summary>Whether stock tracking is enabled</summary>
    public bool TrackInventory { get; private set; } = true;
    
    /// <summary>Barcode if applicable</summary>
    public string? Barcode { get; private set; }
    
    /// <summary>Supplier/Vendor reference</summary>
    public string? SupplierReference { get; private set; }
    
    /// <summary>Notes or additional information</summary>
    public string? Notes { get; private set; }
    
    // Navigation properties
    public virtual ICollection<StockMovement>? Movements { get; set; }
    public virtual ICollection<StockReservation>? Reservations { get; set; }
    
    private StockItem() { }
    
    public static StockItem Create(
        Guid tenantId,
        string sku,
        string name,
        StockItemCategory category,
        string unitOfMeasure,
        decimal unitCost,
        decimal unitPrice,
        Guid createdById,
        string? description = null,
        bool isService = false,
        bool trackInventory = true)
    {
        return new StockItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SKU = sku,
            Name = name,
            Description = description,
            Category = category,
            UnitOfMeasure = unitOfMeasure,
            UnitCost = unitCost,
            UnitPrice = unitPrice,
            IsService = isService,
            TrackInventory = trackInventory,
            Status = StockItemStatus.Active,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow,
            Movements = new List<StockMovement>(),
            Reservations = new List<StockReservation>()
        };
    }
    
    public void Update(
        string name,
        string? description,
        StockItemCategory category,
        string unitOfMeasure,
        decimal unitCost,
        decimal unitPrice,
        Guid modifiedById)
    {
        Name = name;
        Description = description;
        Category = category;
        UnitOfMeasure = unitOfMeasure;
        UnitCost = unitCost;
        UnitPrice = unitPrice;
        LastModifiedById = modifiedById;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    public void SetStockLevels(decimal minimum, decimal? maximum, decimal reorderPoint, decimal reorderQuantity)
    {
        MinimumStockLevel = minimum;
        MaximumStockLevel = maximum;
        ReorderPoint = reorderPoint;
        ReorderQuantity = reorderQuantity;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    public void SetBarcode(string barcode)
    {
        Barcode = barcode;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    public void Discontinue()
    {
        Status = StockItemStatus.Discontinued;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    public void Deactivate()
    {
        Status = StockItemStatus.Inactive;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    public void Activate()
    {
        Status = StockItemStatus.Active;
        LastModifiedAt = DateTime.UtcNow;
    }
}
