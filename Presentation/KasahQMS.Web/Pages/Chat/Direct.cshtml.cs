using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Chat;

public class DirectModel : PageModel
{
    private readonly ICurrentUserService _currentUser;
    private readonly IChatService _chatService;
    private readonly ApplicationDbContext _db;

    public DirectModel(ICurrentUserService currentUser, IChatService chatService, ApplicationDbContext db)
    {
        _currentUser = currentUser;
        _chatService = chatService;
        _db = db;
    }

    public async Task<IActionResult> OnGetAsync(Guid otherUserId, Guid? taskId = null)
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
        
        if (otherUserId == userId.Value)
            return RedirectToPage("/Chat");

        var thread = await _chatService.GetOrCreateDirectThreadAsync(tenantId.Value, userId.Value, otherUserId, taskId);
        if (thread == null)
            return RedirectToPage("/Chat");

        return RedirectToPage("/Chat/Thread", new { id = thread.Id });
    }
}
