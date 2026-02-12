using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Chat;

public class ChatThread : BaseEntity
{
    public Guid TenantId { get; set; }
    public ChatThreadType Type { get; set; }
    public string? Name { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? SecondOrganizationUnitId { get; set; }
    public Guid? TaskId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedById { get; set; }

    public virtual Tenant? Tenant { get; set; }
    public virtual OrganizationUnit? OrganizationUnit { get; set; }
    public virtual OrganizationUnit? SecondOrganizationUnit { get; set; }
    public virtual User? CreatedBy { get; set; }
    public virtual ICollection<ChatMessage>? Messages { get; set; }
    public virtual ICollection<ChatThreadParticipant>? Participants { get; set; }
}
