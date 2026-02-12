using System.Security.Claims;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatApiController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ICurrentUserService _currentUser;
    private readonly IWebHostEnvironment _environment;
    private readonly ApplicationDbContext _db;

    public ChatApiController(IChatService chatService, ICurrentUserService currentUser, IWebHostEnvironment environment, ApplicationDbContext db)
    {
        _chatService = chatService;
        _currentUser = currentUser;
        _environment = environment;
        _db = db;
    }

    private (Guid? userId, Guid? tenantId) GetUserContext()
    {
        var sid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = _currentUser.UserId ?? (Guid.TryParse(sid, out var u) ? u : (Guid?)null);
        return (userId, _currentUser.TenantId);
    }

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages([FromQuery] Guid threadId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var (userId, _) = GetUserContext();
        if (userId == null) return Unauthorized();

        var list = await _chatService.GetMessagesAsync(threadId, skip, Math.Min(take, 100));
        return Ok(list);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        var (userId, _) = GetUserContext();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Message content is required");

        var msg = await _chatService.SendMessageAsync(request.ThreadId, userId.Value, request.Content);
        if (msg == null) return BadRequest("Failed to send message");
        
        return Ok(msg);
    }

    [HttpPost("messages/with-file")]
    public async Task<IActionResult> SendMessageWithFile([FromForm] Guid threadId, [FromForm] string? content, IFormFile? file)
    {
        var (userId, _) = GetUserContext();
        if (userId == null) return Unauthorized();

        string? attachmentPath = null;
        string? attachmentName = null;

        if (file != null && file.Length > 0)
        {
            var root = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var year = DateTime.UtcNow.Year.ToString();
            var chatDir = Path.Combine(root, "uploads", "chat", year);
            if (!Directory.Exists(chatDir)) Directory.CreateDirectory(chatDir);

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(ext)) ext = ".bin";
            var safeName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(chatDir, safeName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(stream);

            attachmentPath = $"/uploads/chat/{year}/{safeName}";
            attachmentName = file.FileName;
        }

        var msg = await _chatService.SendMessageAsync(threadId, userId.Value, content ?? "", attachmentPath, attachmentName);
        if (msg == null) return BadRequest("Failed to send message");

        return Ok(msg);
    }

    [HttpPut("messages/{id}")]
    public async Task<IActionResult> EditMessage(Guid id, [FromBody] EditMessageRequest request)
    {
        var (userId, _) = GetUserContext();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Message content is required");

        var msg = await _chatService.EditMessageAsync(id, userId.Value, request.Content);
        if (msg == null) return NotFound("Message not found or you cannot edit it");

        return Ok(msg);
    }

    [HttpDelete("messages/{id}")]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        var (userId, _) = GetUserContext();
        if (userId == null) return Unauthorized();

        var success = await _chatService.DeleteMessageAsync(id, userId.Value);
        if (!success) return NotFound("Message not found or you cannot delete it");

        return Ok(new { success = true });
    }

    [HttpGet("thread")]
    public async Task<IActionResult> GetThread([FromQuery] Guid id)
    {
        var (userId, _) = GetUserContext();
        if (userId == null) return Unauthorized();

        var t = await _chatService.GetThreadAsync(id);
        if (t == null) return NotFound();
        return Ok(t);
    }

    [HttpGet("department-thread")]
    public async Task<IActionResult> GetDepartmentThread([FromQuery] Guid orgUnitId)
    {
        var (userId, tenantId) = GetUserContext();
        if (userId == null || tenantId == null) return Unauthorized();

        var t = await _chatService.GetOrCreateDepartmentThreadAsync(tenantId.Value, orgUnitId, userId.Value);
        if (t == null) return NotFound();
        return Ok(t);
    }

    [HttpGet("direct-thread")]
    public async Task<IActionResult> GetOrCreateDirectThread([FromQuery] Guid otherUserId, [FromQuery] Guid? taskId = null)
    {
        var (userId, tenantId) = GetUserContext();
        if (userId == null || tenantId == null) return Unauthorized();

        var t = await _chatService.GetOrCreateDirectThreadAsync(tenantId.Value, userId.Value, otherUserId, taskId);
        if (t == null) return BadRequest();
        return Ok(t);
    }

    [HttpGet("crossdept-thread")]
    public async Task<IActionResult> GetOrCreateCrossDeptThread([FromQuery] Guid dept1Id, [FromQuery] Guid dept2Id)
    {
        var (userId, tenantId) = GetUserContext();
        if (userId == null || tenantId == null) return Unauthorized();

        var t = await _chatService.GetOrCreateCrossDeptThreadAsync(tenantId.Value, dept1Id, dept2Id, userId.Value);
        if (t == null) return BadRequest();
        return Ok(t);
    }

    /// <summary>
    /// Get all conversations (inbox) for the current user, sorted by most recent message.
    /// Like Instagram's DM inbox.
    /// </summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var (userId, tenantId) = GetUserContext();
        if (userId == null) return Unauthorized();
        
        tenantId ??= await _db.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        // Get user's organization unit
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value);
        var orgUnitId = user?.OrganizationUnitId;

        // Get all thread IDs where user is a participant
        var participantThreadIds = await _db.ChatThreadParticipants
            .AsNoTracking()
            .Where(p => p.UserId == userId.Value)
            .Select(p => p.ThreadId)
            .ToListAsync();

        // Get department thread IDs
        var deptThreadIds = orgUnitId.HasValue
            ? await _db.ChatThreads
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId && 
                           t.Type == KasahQMS.Domain.Entities.Chat.ChatThreadType.Department && 
                           t.OrganizationUnitId == orgUnitId)
                .Select(t => t.Id)
                .ToListAsync()
            : new List<Guid>();

        var allThreadIds = participantThreadIds.Concat(deptThreadIds).Distinct().ToList();

        if (!allThreadIds.Any())
            return Ok(new List<ConversationDto>());

        // Get threads with their latest message
        var threads = await _db.ChatThreads
            .AsNoTracking()
            .Where(t => allThreadIds.Contains(t.Id))
            .Include(t => t.Participants!)
                .ThenInclude(p => p.User)
            .Include(t => t.OrganizationUnit)
            .ToListAsync();

        var conversationList = new List<ConversationDto>();

        foreach (var thread in threads)
        {
            // Get the latest message in this thread
            var latestMessage = await _db.ChatMessages
                .AsNoTracking()
                .Where(m => m.ThreadId == thread.Id && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Include(m => m.Sender)
                .FirstOrDefaultAsync();

            if (latestMessage == null) continue;

            // Get unread count
            var unreadCount = await _db.ChatMessages
                .CountAsync(m => m.ThreadId == thread.Id && 
                                !m.IsDeleted && 
                                m.SenderId != userId.Value);

            // Determine conversation info
            string name;
            string avatarInitials;
            Guid? otherUserId = null;
            string type = thread.Type.ToString();

            if (thread.Type == KasahQMS.Domain.Entities.Chat.ChatThreadType.Department)
            {
                name = thread.OrganizationUnit?.Name ?? "Department";
                avatarInitials = name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
            }
            else if (thread.Type == KasahQMS.Domain.Entities.Chat.ChatThreadType.Direct && thread.Participants != null)
            {
                var otherParticipant = thread.Participants.FirstOrDefault(p => p.UserId != userId.Value);
                if (otherParticipant?.User != null)
                {
                    name = otherParticipant.User.FullName;
                    otherUserId = otherParticipant.UserId;
                    var fn = otherParticipant.User.FirstName ?? "";
                    var ln = otherParticipant.User.LastName ?? "";
                    avatarInitials = ((fn.Length > 0 ? fn[0].ToString() : "") + (ln.Length > 0 ? ln[0].ToString() : "")).ToUpper();
                }
                else
                {
                    name = "Unknown";
                    avatarInitials = "??";
                }
            }
            else
            {
                name = "Conversation";
                avatarInitials = "CH";
            }

            var lastMessagePreview = latestMessage.Content ?? "";
            if (lastMessagePreview.Length > 40)
                lastMessagePreview = lastMessagePreview.Substring(0, 37) + "...";
            
            var senderPrefix = latestMessage.SenderId == userId.Value ? "You: " : "";
            if (thread.Type == KasahQMS.Domain.Entities.Chat.ChatThreadType.Department && latestMessage.SenderId != userId.Value)
                senderPrefix = (latestMessage.Sender?.FirstName ?? "Someone") + ": ";

            conversationList.Add(new ConversationDto(
                thread.Id,
                otherUserId,
                name,
                avatarInitials,
                type,
                senderPrefix + lastMessagePreview,
                latestMessage.CreatedAt,
                unreadCount > 0,
                latestMessage.AttachmentName != null
            ));
        }

        // Sort by latest message (newest first)
        return Ok(conversationList.OrderByDescending(c => c.LastMessageTime).ToList());
    }

    [HttpGet("threads/{id}/messages")]
    public async Task<IActionResult> GetThreadMessages(Guid id, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var (userId, _) = GetUserContext();
        if (userId == null) return Unauthorized();

        var list = await _chatService.GetMessagesAsync(id, skip, Math.Min(take, 100));
        return Ok(list);
    }
}

public record SendMessageRequest(Guid ThreadId, string Content);
public record EditMessageRequest(string Content);
public record ConversationDto(
    Guid ThreadId, 
    Guid? OtherUserId, 
    string Name, 
    string AvatarInitials, 
    string Type,
    string LastMessagePreview, 
    DateTime LastMessageTime, 
    bool HasUnread,
    bool HasAttachment);
