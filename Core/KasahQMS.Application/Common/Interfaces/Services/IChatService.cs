using KasahQMS.Domain.Entities.Chat;

namespace KasahQMS.Application.Common.Interfaces.Services;

public record ChatMessageDto(
    Guid Id, 
    Guid ThreadId, 
    Guid SenderId, 
    string SenderName, 
    string Content, 
    DateTime CreatedAt, 
    DateTime? EditedAt = null,
    string? AttachmentPath = null,
    string? AttachmentName = null);
public record ChatThreadDto(Guid Id, ChatThreadType Type, string? Name, Guid? OrganizationUnitId, Guid? TaskId);

/// <summary>
/// Service for chat threads and messages.
/// </summary>
public interface IChatService
{
    Task<ChatThreadDto?> GetOrCreateDepartmentThreadAsync(Guid tenantId, Guid orgUnitId, Guid createdById, CancellationToken cancellationToken = default);
    Task<ChatThreadDto?> GetOrCreateDirectThreadAsync(Guid tenantId, Guid user1Id, Guid user2Id, Guid? taskId, CancellationToken cancellationToken = default);
    Task<ChatThreadDto?> GetOrCreateCrossDeptThreadAsync(Guid tenantId, Guid dept1Id, Guid dept2Id, Guid createdById, CancellationToken cancellationToken = default);
    Task<ChatMessageDto?> SendMessageAsync(Guid threadId, Guid senderId, string content, string? attachmentPath = null, string? attachmentName = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid threadId, int skip, int take, CancellationToken cancellationToken = default);
    Task<ChatThreadDto?> GetThreadAsync(Guid threadId, CancellationToken cancellationToken = default);
    Task<ChatMessageDto?> EditMessageAsync(Guid messageId, Guid userId, string newContent, CancellationToken cancellationToken = default);
    Task<bool> DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);
}
