using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Chat;

public class ChatMessage : BaseEntity
{
    public Guid ThreadId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentName { get; set; }

    public virtual ChatThread? Thread { get; set; }
    public virtual User? Sender { get; set; }
}
