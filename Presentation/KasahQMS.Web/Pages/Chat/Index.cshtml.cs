using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Chat;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationDbContext _db;
    private readonly IHierarchyService _hierarchyService;

    public IndexModel(ICurrentUserService currentUser, ApplicationDbContext db, IHierarchyService hierarchyService)
    {
        _currentUser = currentUser;
        _db = db;
        _hierarchyService = hierarchyService;
    }

    public Guid? CurrentUserId { get; set; }
    public Guid? OrgUnitId { get; set; }
    public string? DeptName { get; set; }
    public bool CanChat { get; set; }
    public bool IsManager { get; set; }
    public bool IsAuditor { get; set; }
    public List<UserOption> AllUsers { get; set; } = new();
    public List<UserOption> SubordinateUsers { get; set; } = new();
    public List<DepartmentOption> Departments { get; set; } = new();
    public string CurrentUserName { get; set; } = "";
    
    // Conversations list (like Instagram inbox)
    public List<ConversationItem> Conversations { get; set; } = new();
    
    // For opening specific chat from notification
    public Guid? OpenThreadId { get; set; }
    public Guid? OpenUserId { get; set; }
    public string? OpenUserName { get; set; }
    public string? OpenThreadType { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid? thread = null, Guid? openUser = null)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return RedirectToPage("/Account/Login");

        CurrentUserId = userId;

        var tenantId = _currentUser.TenantId ?? await _db.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        var currentUserEntity = await _db.Users
            .AsNoTracking()
            .Include(u => u.OrganizationUnit)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (currentUserEntity == null)
            return RedirectToPage("/Account/Login");

        // Check if user is an auditor - redirect to audit page
        IsAuditor = currentUserEntity.Roles?.Any(r => 
            r.Name == "Auditor" || 
            r.Name == "Internal Auditor") == true;

        if (IsAuditor)
        {
            // Auditors can't send messages - redirect to read-only audit view
            return RedirectToPage("./Audit");
        }

        CurrentUserName = currentUserEntity.FullName;
        OrgUnitId = currentUserEntity.OrganizationUnitId;
        DeptName = currentUserEntity.OrganizationUnit?.Name ?? "No Department";
        CanChat = true;

        // Check if user is manager
        IsManager = currentUserEntity.Roles?.Any(r => 
            r.Name == "TMD" || 
            r.Name == "Deputy Country Manager" || 
            r.Name == "Department Manager" ||
            r.Name == "System Admin") == true;

        // Handle opening specific thread from notification
        if (thread.HasValue)
        {
            var chatThread = await _db.ChatThreads.AsNoTracking()
                .Include(t => t.Participants)
                .Include(t => t.OrganizationUnit)
                .FirstOrDefaultAsync(t => t.Id == thread.Value);
            
            if (chatThread != null)
            {
                OpenThreadId = chatThread.Id;
                OpenThreadType = chatThread.Type.ToString();
                
                if (chatThread.Type == KasahQMS.Domain.Entities.Chat.ChatThreadType.Department)
                {
                    OpenUserName = chatThread.OrganizationUnit?.Name ?? "Department";
                }
                else if (chatThread.Type == KasahQMS.Domain.Entities.Chat.ChatThreadType.Direct && chatThread.Participants != null)
                {
                    var otherParticipant = chatThread.Participants.FirstOrDefault(p => p.UserId != userId.Value);
                    if (otherParticipant != null)
                    {
                        var otherUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == otherParticipant.UserId);
                        OpenUserId = otherParticipant.UserId;
                        OpenUserName = otherUser?.FullName ?? "Unknown";
                    }
                }
            }
        }
        else if (openUser.HasValue)
        {
            var targetUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == openUser.Value);
            if (targetUser != null)
            {
                OpenUserId = targetUser.Id;
                OpenUserName = targetUser.FullName;
                OpenThreadType = "Direct";
            }
        }

        // Get all active users for direct messaging (everyone can message anyone)
        AllUsers = await _db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && u.Id != userId.Value)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Include(u => u.OrganizationUnit)
            .Select(u => new UserOption(u.Id, $"{u.FirstName} {u.LastName}", u.OrganizationUnit != null ? u.OrganizationUnit.Name : "—"))
            .ToListAsync();

        // Get subordinates if manager
        if (IsManager)
        {
            var subordinateIds = await _hierarchyService.GetSubordinateUserIdsAsync(userId.Value);
            SubordinateUsers = AllUsers.Where(u => subordinateIds.Contains(u.Id)).ToList();
        }

        // Get all departments for cross-department messaging
        Departments = await _db.OrganizationUnits
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.IsActive)
            .OrderBy(o => o.Name)
            .Select(o => new DepartmentOption(o.Id, o.Name))
            .ToListAsync();

        // Load conversations (threads user is part of) - like Instagram inbox
        await LoadConversationsAsync(userId.Value, tenantId);

        return Page();
    }

    private async Task LoadConversationsAsync(Guid userId, Guid tenantId)
    {
        // Get all thread IDs where user is a participant
        var participantThreadIds = await _db.ChatThreadParticipants
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ThreadId)
            .ToListAsync();

        // Get department thread IDs (if user belongs to a department)
        var deptThreadIds = OrgUnitId.HasValue
            ? await _db.ChatThreads
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId && 
                           t.Type == KasahQMS.Domain.Entities.Chat.ChatThreadType.Department && 
                           t.OrganizationUnitId == OrgUnitId)
                .Select(t => t.Id)
                .ToListAsync()
            : new List<Guid>();

        var allThreadIds = participantThreadIds.Concat(deptThreadIds).Distinct().ToList();

        if (!allThreadIds.Any())
        {
            Conversations = new List<ConversationItem>();
            return;
        }

        // Get threads with their latest message
        var threads = await _db.ChatThreads
            .AsNoTracking()
            .Where(t => allThreadIds.Contains(t.Id))
            .Include(t => t.Participants!)
                .ThenInclude(p => p.User)
            .Include(t => t.OrganizationUnit)
            .ToListAsync();

        var conversationList = new List<ConversationItem>();

        foreach (var thread in threads)
        {
            // Get the latest message in this thread
            var latestMessage = await _db.ChatMessages
                .AsNoTracking()
                .Where(m => m.ThreadId == thread.Id && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Include(m => m.Sender)
                .FirstOrDefaultAsync();

            if (latestMessage == null) continue; // Skip threads with no messages

            // Get unread count (messages from others)
            var unreadCount = await _db.ChatMessages
                .CountAsync(m => m.ThreadId == thread.Id && 
                                !m.IsDeleted && 
                                m.SenderId != userId);

            // Determine conversation name and other participant info
            string name;
            string? avatarInitials;
            Guid? otherUserId = null;
            string type = thread.Type.ToString();

            if (thread.Type == KasahQMS.Domain.Entities.Chat.ChatThreadType.Department)
            {
                name = thread.OrganizationUnit?.Name ?? "Department";
                avatarInitials = name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
            }
            else if (thread.Type == KasahQMS.Domain.Entities.Chat.ChatThreadType.Direct && thread.Participants != null)
            {
                var otherParticipant = thread.Participants.FirstOrDefault(p => p.UserId != userId);
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

            // Format last message preview
            var lastMessagePreview = latestMessage.Content ?? "";
            if (lastMessagePreview.Length > 40)
                lastMessagePreview = lastMessagePreview.Substring(0, 37) + "...";
            
            var senderPrefix = latestMessage.SenderId == userId ? "You: " : "";
            if (thread.Type == KasahQMS.Domain.Entities.Chat.ChatThreadType.Department && latestMessage.SenderId != userId)
            {
                senderPrefix = (latestMessage.Sender?.FirstName ?? "Someone") + ": ";
            }

            conversationList.Add(new ConversationItem(
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

        // Sort by latest message time (newest first) - like Instagram
        Conversations = conversationList.OrderByDescending(c => c.LastMessageTime).ToList();
    }

    public record UserOption(Guid Id, string Name, string Department);
    public record DepartmentOption(Guid Id, string Name);
    public record ConversationItem(
        Guid ThreadId, 
        Guid? OtherUserId, 
        string Name, 
        string AvatarInitials, 
        string Type,
        string LastMessagePreview, 
        DateTime LastMessageTime, 
        bool HasUnread,
        bool HasAttachment);
}
