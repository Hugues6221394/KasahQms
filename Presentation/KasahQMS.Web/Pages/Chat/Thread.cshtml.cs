using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Chat;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Chat;

public class ThreadModel : PageModel
{
    private readonly ICurrentUserService _currentUser;
    private readonly IChatService _chatService;
    private readonly ApplicationDbContext _db;

    public ThreadModel(ICurrentUserService currentUser, IChatService chatService, ApplicationDbContext db)
    {
        _currentUser = currentUser;
        _chatService = chatService;
        _db = db;
    }

    public Guid ThreadId { get; set; }
    public string? ThreadTitle { get; set; }
    public Guid? TaskId { get; set; }
    public bool CanAccess { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = _currentUser.UserId;
        var tenantId = _currentUser.TenantId;
        if (userId == null || tenantId == null)
            return RedirectToPage("/Account/Login");
        
        // Auditors cannot send messages - redirect to audit page
        var currentUser = await _db.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);
        
        var isAuditor = currentUser?.Roles?.Any(r => 
            r.Name == "Auditor" || 
            r.Name == "Internal Auditor") == true;
        
        if (isAuditor)
            return RedirectToPage("/Chat/Audit");

        var thread = await _db.ChatThreads
            .AsNoTracking()
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);
        if (thread == null)
        {
            CanAccess = false;
            return Page();
        }

        var allowed = false;
        if (thread.Type == ChatThreadType.Direct && thread.Participants != null)
            allowed = thread.Participants.Any(p => p.UserId == userId);
        else if (thread.Type == ChatThreadType.Department && thread.OrganizationUnitId.HasValue)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Value);
            allowed = u?.OrganizationUnitId == thread.OrganizationUnitId;
        }
        else if (thread.Type == ChatThreadType.CrossDept)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Value);
            allowed = u != null && (u.OrganizationUnitId == thread.OrganizationUnitId || u.OrganizationUnitId == thread.SecondOrganizationUnitId);
        }

        if (!allowed)
        {
            CanAccess = false;
            return Page();
        }

        ThreadId = thread.Id;
        TaskId = thread.TaskId;
        ThreadTitle = thread.Name ?? (thread.Type == ChatThreadType.Direct ? "Direct chat" : "Chat");
        CanAccess = true;
        return Page();
    }
}
