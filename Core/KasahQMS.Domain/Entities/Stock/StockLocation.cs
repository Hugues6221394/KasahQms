using KasahQMS.Domain.Common;

namespace KasahQMS.Domain.Entities.Stock;

/// <summary>
/// Represents a physical or logical location where stock is stored.
/// Examples: Warehouse, Office, Virtual inventory for services.
/// </summary>
public class StockLocation : AuditableEntity
{
    /// <summary>Unique code for the location (e.g., WH-001)</summary>
    public string Code { get; private set; } = string.Empty;
    
    /// <summary>Human-readable name</summary>
    public string Name { get; private set; } = string.Empty;
    
    /// <summary>Optional description</summary>
    public string? Description { get; private set; }
    
    /// <summary>Physical address if applicable</summary>
    public string? Address { get; private set; }
    
    /// <summary>Whether this location is active</summary>
    public bool IsActive { get; private set; } = true;
    
    /// <summary>Whether this is a virtual location (for services)</summary>
    public bool IsVirtual { get; private set; }
    
    /// <summary>Sort order for display</summary>
    public int DisplayOrder { get; private set; }
    
    // Navigation properties
    public virtual ICollection<StockMovement>? IncomingMovements { get; set; }
    public virtual ICollection<StockMovement>? OutgoingMovements { get; set; }
    
    private StockLocation() { }
    
    public static StockLocation Create(
        Guid tenantId,
        string code,
        string name,
        Guid createdById,
        string? description = null,
        string? address = null,
        bool isVirtual = false)
    {
        return new StockLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            Description = description,
            Address = address,
            IsVirtual = isVirtual,
            IsActive = true,
            DisplayOrder = 0,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    public void Update(string name, string? description, string? address)
    {
        Name = name;
        Description = description;
        Address = address;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    public void Deactivate()
    {
        IsActive = false;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    public void Activate()
    {
        IsActive = true;
        LastModifiedAt = DateTime.UtcNow;
    }
}
