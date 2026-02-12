using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Chat;

public class ChatThreadParticipant : BaseEntity
{
    public Guid ThreadId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; }

    public virtual ChatThread? Thread { get; set; }
    public virtual User? User { get; set; }
}
