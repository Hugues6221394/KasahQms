using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Entities.Chat;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Chat;

/// <summary>
/// Read-only chat audit page for Internal Auditors.
/// Allows viewing all chat conversations in the system for compliance purposes.
/// </summary>
[Authorize(Roles = "Auditor,Internal Auditor,System Admin,Admin")]
public class AuditModel : PageModel
{
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AuditModel> _logger;

    public AuditModel(ICurrentUserService currentUser, ApplicationDbContext db, ILogger<AuditModel> logger)
    {
        _currentUser = currentUser;
        _db = db;
        _logger = logger;
    }

    public bool IsAuditor { get; set; }
    public bool HasAccess { get; set; }
    public string? ErrorMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DateTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? SelectedThreadId { get; set; }

    public List<ThreadSummary> Threads { get; set; } = new();
    public ThreadDetail? SelectedThread { get; set; }
    public int TotalThreads { get; set; }
    public int TotalMessages { get; set; }
    public int TotalUsers { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return RedirectToPage("/Account/Login");

        var currentUser = await _db.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (currentUser == null)
            return RedirectToPage("/Account/Login");

        // Check if user is an auditor or admin
        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        IsAuditor = roles.Any(r => r == "Auditor" || r == "Internal Auditor");
        var isAdmin = roles.Any(r => r == "System Admin" || r == "SystemAdmin" || r == "Admin");

        if (!IsAuditor && !isAdmin)
        {
            HasAccess = false;
            ErrorMessage = "Only Internal Auditors and System Admins can access the chat audit page.";
            return Page();
        }

        HasAccess = true;
        var tenantId = _currentUser.TenantId ?? await _db.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        _logger.LogInformation("Chat audit accessed by user {UserId} (Auditor: {IsAuditor})", userId, IsAuditor);

        // Get statistics
        TotalThreads = await _db.ChatThreads.CountAsync(t => t.TenantId == tenantId);
        TotalMessages = await _db.ChatMessages.CountAsync(m => m.Thread != null && m.Thread.TenantId == tenantId);
        TotalUsers = await _db.Users.CountAsync(u => u.TenantId == tenantId && u.IsActive);

        // Build query for threads
        var threadsQuery = _db.ChatThreads
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .Include(t => t.Participants!)
                .ThenInclude(p => p.User)
            .Include(t => t.OrganizationUnit)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            threadsQuery = threadsQuery.Where(t =>
                (t.OrganizationUnit != null && t.OrganizationUnit.Name.ToLower().Contains(searchLower)) ||
                (t.Participants != null && t.Participants.Any(p => 
                    p.User != null && (p.User.FirstName.ToLower().Contains(searchLower) || p.User.LastName.ToLower().Contains(searchLower)))));
        }

        var threads = await threadsQuery.ToListAsync();

        // Build thread summaries
        foreach (var thread in threads)
        {
            var latestMessage = await _db.ChatMessages
                .AsNoTracking()
                .Where(m => m.ThreadId == thread.Id && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Include(m => m.Sender)
                .FirstOrDefaultAsync();

            var messageCount = await _db.ChatMessages.CountAsync(m => m.ThreadId == thread.Id);

            if (messageCount == 0) continue;

            // Apply date filter on messages
            if (!string.IsNullOrEmpty(DateFrom) && DateTime.TryParse(DateFrom, out var fromDate))
            {
                var hasMessagesAfterDate = await _db.ChatMessages
                    .AnyAsync(m => m.ThreadId == thread.Id && m.CreatedAt >= fromDate);
                if (!hasMessagesAfterDate) continue;
            }

            if (!string.IsNullOrEmpty(DateTo) && DateTime.TryParse(DateTo, out var toDate))
            {
                var hasMessagesBeforeDate = await _db.ChatMessages
                    .AnyAsync(m => m.ThreadId == thread.Id && m.CreatedAt <= toDate.AddDays(1));
                if (!hasMessagesBeforeDate) continue;
            }

            string name;
            string participants;
            string type = thread.Type.ToString();

            if (thread.Type == ChatThreadType.Department)
            {
                name = thread.OrganizationUnit?.Name ?? "Department";
                var memberCount = await _db.Users.CountAsync(u => u.OrganizationUnitId == thread.OrganizationUnitId);
                participants = $"{memberCount} members";
            }
            else if (thread.Type == ChatThreadType.Direct && thread.Participants != null)
            {
                var participantNames = thread.Participants
                    .Where(p => p.User != null)
                    .Select(p => p.User!.FullName)
                    .Take(2)
                    .ToList();
                name = string.Join(" & ", participantNames);
                participants = $"{participantNames.Count} participants";
            }
            else
            {
                name = "Unknown Thread";
                participants = "Unknown";
            }

            Threads.Add(new ThreadSummary(
                thread.Id,
                name,
                type,
                participants,
                messageCount,
                latestMessage?.CreatedAt,
                latestMessage?.Content?.Length > 50 ? latestMessage.Content.Substring(0, 47) + "..." : latestMessage?.Content ?? ""
            ));
        }

        // Sort by latest message
        Threads = Threads.OrderByDescending(t => t.LastMessageTime).ToList();

        // Load selected thread details
        if (SelectedThreadId.HasValue)
        {
            await LoadThreadDetailsAsync(SelectedThreadId.Value, tenantId);
        }

        return Page();
    }

    private async Task LoadThreadDetailsAsync(Guid threadId, Guid tenantId)
    {
        var thread = await _db.ChatThreads
            .AsNoTracking()
            .Include(t => t.Participants!)
                .ThenInclude(p => p.User)
            .Include(t => t.OrganizationUnit)
            .FirstOrDefaultAsync(t => t.Id == threadId && t.TenantId == tenantId);

        if (thread == null) return;

        var messagesQuery = _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ThreadId == threadId)
            .Include(m => m.Sender)
            .OrderBy(m => m.CreatedAt);

        // Apply date filters
        IQueryable<ChatMessage> filteredQuery = messagesQuery;
        if (!string.IsNullOrEmpty(DateFrom) && DateTime.TryParse(DateFrom, out var fromDate))
        {
            filteredQuery = filteredQuery.Where(m => m.CreatedAt >= fromDate);
        }
        if (!string.IsNullOrEmpty(DateTo) && DateTime.TryParse(DateTo, out var toDate))
        {
            filteredQuery = filteredQuery.Where(m => m.CreatedAt <= toDate.AddDays(1));
        }

        var messages = await filteredQuery.ToListAsync();

        string name;
        var participantList = new List<string>();

        if (thread.Type == ChatThreadType.Department)
        {
            name = thread.OrganizationUnit?.Name ?? "Department";
            var members = await _db.Users
                .AsNoTracking()
                .Where(u => u.OrganizationUnitId == thread.OrganizationUnitId && u.IsActive)
                .Select(u => u.FullName)
                .ToListAsync();
            participantList = members;
        }
        else if (thread.Participants != null)
        {
            participantList = thread.Participants
                .Where(p => p.User != null)
                .Select(p => p.User!.FullName)
                .ToList();
            name = string.Join(" & ", participantList);
        }
        else
        {
            name = "Unknown";
        }

        SelectedThread = new ThreadDetail(
            thread.Id,
            name,
            thread.Type.ToString(),
            participantList,
            messages.Select(m => new MessageDetail(
                m.Id,
                m.Sender?.FullName ?? "Unknown",
                m.Content,
                m.CreatedAt,
                m.IsDeleted,
                m.EditedAt,
                m.AttachmentName
            )).ToList()
        );
    }

    public record ThreadSummary(
        Guid Id,
        string Name,
        string Type,
        string Participants,
        int MessageCount,
        DateTime? LastMessageTime,
        string LastMessagePreview);

    public record ThreadDetail(
        Guid Id,
        string Name,
        string Type,
        List<string> Participants,
        List<MessageDetail> Messages);

    public record MessageDetail(
        Guid Id,
        string SenderName,
        string Content,
        DateTime CreatedAt,
        bool IsDeleted,
        DateTime? EditedAt,
        string? AttachmentName);
}
