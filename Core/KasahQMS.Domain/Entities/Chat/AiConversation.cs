using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Chat;

public class AiConversation : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string ConversationKey { get; set; } = string.Empty;
    public string Title { get; set; } = "New chat";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Tenant? Tenant { get; set; }
    public virtual User? User { get; set; }
    public virtual ICollection<AiConversationMessage>? Messages { get; set; }
}

public class AiConversationMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public virtual AiConversation? Conversation { get; set; }
}
