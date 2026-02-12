using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Documents;

/// <summary>
/// Document access control entity.
/// </summary>
public class DocumentAccess : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? RoleId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public DocumentAccessType AccessType { get; set; }
    public DateTime GrantedAt { get; set; }
    public Guid GrantedById { get; set; }
    public DateTime? ExpiresAt { get; set; }
    
    // Navigation properties
    public virtual Document? Document { get; set; }
    public virtual User? User { get; set; }
    public virtual Role? Role { get; set; }
    public virtual OrganizationUnit? OrganizationUnit { get; set; }
    public virtual User? GrantedBy { get; set; }
    
    public DocumentAccess() { }
    
    public static DocumentAccess GrantToUser(
        Guid documentId,
        Guid userId,
        DocumentAccessType accessType,
        Guid grantedById)
    {
        return new DocumentAccess
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            UserId = userId,
            AccessType = accessType,
            GrantedAt = DateTime.UtcNow,
            GrantedById = grantedById
        };
    }
    
    public static DocumentAccess GrantToRole(
        Guid documentId,
        Guid roleId,
        DocumentAccessType accessType,
        Guid grantedById)
    {
        return new DocumentAccess
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            RoleId = roleId,
            AccessType = accessType,
            GrantedAt = DateTime.UtcNow,
            GrantedById = grantedById
        };
    }
    
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt < DateTime.UtcNow;
}
