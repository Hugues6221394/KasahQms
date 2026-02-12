using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Chat;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Infrastructure.Persistence.Services;

public class ChatService : IChatService
{
    private readonly ApplicationDbContext _db;

    public ChatService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ChatThreadDto?> GetOrCreateDepartmentThreadAsync(Guid tenantId, Guid orgUnitId, Guid createdById, CancellationToken cancellationToken = default)
    {
        var t = await _db.ChatThreads
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Type == ChatThreadType.Department && x.OrganizationUnitId == orgUnitId, cancellationToken);
        if (t != null) return ToDto(t);

        t = new ChatThread
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Type = ChatThreadType.Department,
            OrganizationUnitId = orgUnitId,
            CreatedAt = DateTime.UtcNow,
            CreatedById = createdById
        };
        _db.ChatThreads.Add(t);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(t);
    }

    public async Task<ChatThreadDto?> GetOrCreateDirectThreadAsync(Guid tenantId, Guid user1Id, Guid user2Id, Guid? taskId, CancellationToken cancellationToken = default)
    {
        var u1 = user1Id.CompareTo(user2Id) < 0 ? user1Id : user2Id;
        var u2 = user1Id.CompareTo(user2Id) < 0 ? user2Id : user1Id;

        var t = await _db.ChatThreads
            .Include(x => x.Participants)
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Type == ChatThreadType.Direct &&
                x.TaskId == taskId &&
                x.Participants!.Any(p => p.UserId == u1) &&
                x.Participants!.Any(p => p.UserId == u2), cancellationToken);

        if (t != null) return ToDto(t);

        t = new ChatThread
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Type = ChatThreadType.Direct,
            TaskId = taskId,
            CreatedAt = DateTime.UtcNow,
            CreatedById = user1Id
        };
        _db.ChatThreads.Add(t);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var uid in new[] { user1Id, user2Id }.Distinct())
        {
            _db.ChatThreadParticipants.Add(new ChatThreadParticipant
            {
                Id = Guid.NewGuid(),
                ThreadId = t.Id,
                UserId = uid,
                JoinedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(t);
    }

    public async Task<ChatThreadDto?> GetOrCreateCrossDeptThreadAsync(Guid tenantId, Guid dept1Id, Guid dept2Id, Guid createdById, CancellationToken cancellationToken = default)
    {
        var d1 = dept1Id.CompareTo(dept2Id) < 0 ? dept1Id : dept2Id;
        var d2 = dept1Id.CompareTo(dept2Id) < 0 ? dept2Id : dept1Id;

        var t = await _db.ChatThreads
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Type == ChatThreadType.CrossDept &&
                x.OrganizationUnitId == d1 &&
                x.SecondOrganizationUnitId == d2, cancellationToken);

        if (t != null) return ToDto(t);

        t = new ChatThread
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Type = ChatThreadType.CrossDept,
            OrganizationUnitId = d1,
            SecondOrganizationUnitId = d2,
            CreatedAt = DateTime.UtcNow,
            CreatedById = createdById
        };
        _db.ChatThreads.Add(t);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(t);
    }

    public async Task<ChatMessageDto?> SendMessageAsync(Guid threadId, Guid senderId, string content, string? attachmentPath = null, string? attachmentName = null, CancellationToken cancellationToken = default)
    {
        var thread = await _db.ChatThreads.FindAsync(new object[] { threadId }, cancellationToken);
        if (thread == null) return null;

        var sender = await _db.Users.FindAsync(new object[] { senderId }, cancellationToken);
        var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}" : "Unknown";

        var m = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = threadId,
            SenderId = senderId,
            Content = content.Trim().Length > 4000 ? content.Trim().Substring(0, 4000) : content.Trim(),
            CreatedAt = DateTime.UtcNow,
            AttachmentPath = attachmentPath,
            AttachmentName = attachmentName
        };
        _db.ChatMessages.Add(m);
        await _db.SaveChangesAsync(cancellationToken);

        return new ChatMessageDto(m.Id, m.ThreadId, m.SenderId, senderName, m.Content, m.CreatedAt, null, m.AttachmentPath, m.AttachmentName);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid threadId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var list = await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ThreadId == threadId && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Include(m => m.Sender)
            .ToListAsync(cancellationToken);

        return list
            .Select(m => new ChatMessageDto(
                m.Id,
                m.ThreadId,
                m.SenderId,
                m.Sender != null ? $"{m.Sender.FirstName} {m.Sender.LastName}" : "Unknown",
                m.Content,
                m.CreatedAt,
                m.EditedAt,
                m.AttachmentPath,
                m.AttachmentName))
            .Reverse()
            .ToList();
    }

    public async Task<ChatThreadDto?> GetThreadAsync(Guid threadId, CancellationToken cancellationToken = default)
    {
        var t = await _db.ChatThreads.FindAsync(new object[] { threadId }, cancellationToken);
        return t != null ? ToDto(t) : null;
    }

    public async Task<ChatMessageDto?> EditMessageAsync(Guid messageId, Guid userId, string newContent, CancellationToken cancellationToken = default)
    {
        var m = await _db.ChatMessages
            .Include(x => x.Sender)
            .FirstOrDefaultAsync(x => x.Id == messageId && x.SenderId == userId && !x.IsDeleted, cancellationToken);
        
        if (m == null) return null;

        m.Content = newContent.Trim().Length > 4000 ? newContent.Trim().Substring(0, 4000) : newContent.Trim();
        m.EditedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync(cancellationToken);

        var senderName = m.Sender != null ? $"{m.Sender.FirstName} {m.Sender.LastName}" : "Unknown";
        return new ChatMessageDto(m.Id, m.ThreadId, m.SenderId, senderName, m.Content, m.CreatedAt, m.EditedAt, m.AttachmentPath, m.AttachmentName);
    }

    public async Task<bool> DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default)
    {
        var m = await _db.ChatMessages
            .FirstOrDefaultAsync(x => x.Id == messageId && x.SenderId == userId && !x.IsDeleted, cancellationToken);
        
        if (m == null) return false;

        m.IsDeleted = true;
        m.Content = "[Message deleted]";
        
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ChatThreadDto ToDto(ChatThread t) =>
        new(t.Id, t.Type, t.Name, t.OrganizationUnitId, t.TaskId);
}
