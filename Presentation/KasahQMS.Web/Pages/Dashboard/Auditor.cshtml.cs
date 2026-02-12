using System.Text.Json;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Dashboard;

/// <summary>
/// Auditor dashboard - read-only view with export capabilities.
/// </summary>
[Authorize(Roles = "Auditor")]
public class AuditorModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuditorModel> _logger;

    public AuditorModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<AuditorModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public string DisplayName { get; set; } = "Auditor";
    public List<StatCard> Stats { get; set; } = new();
    public List<DocumentItem> RecentDocuments { get; set; } = new();
    public List<CapaItem> RecentCapas { get; set; } = new();
    public List<ActivityItem> Activity { get; set; } = new();
    public string DocumentsTrendJson { get; set; } = "{}";
    public string ComplianceStatusJson { get; set; } = "{}";

    public async Task OnGetAsync()
    {
        var currentUser = await GetCurrentUserAsync();
        var tenantId = currentUser?.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (tenantId == Guid.Empty)
        {
            return;
        }

        DisplayName = currentUser?.FullName ?? "Auditor";

        // Read-only statistics (system-wide view)
        var totalDocuments = await _dbContext.Documents.CountAsync(d => d.TenantId == tenantId);
        var approvedDocuments = await _dbContext.Documents.CountAsync(d =>
            d.TenantId == tenantId && d.Status == DocumentStatus.Approved);
        var openCapas = await _dbContext.Capas.CountAsync(c =>
            c.TenantId == tenantId && c.Status != CapaStatus.Closed && c.Status != CapaStatus.EffectivenessVerified);
        var completedAudits = await _dbContext.Audits.CountAsync(a =>
            a.TenantId == tenantId && a.Status == AuditStatus.Closed);

        Stats = new List<StatCard>
        {
            new("Total documents", totalDocuments.ToString(), "System-wide"),
            new("Approved documents", approvedDocuments.ToString(), "Compliant"),
            new("Open CAPAs", openCapas.ToString(), "Requires attention"),
            new("Completed audits", completedAudits.ToString(), "All time")
        };

        // Recent documents (read-only)
        RecentDocuments = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(5)
            .Select(d => new DocumentItem(d.Title, d.DocumentNumber, d.Status.ToString(), d.CreatedAt.ToString("MMM dd, yyyy")))
            .ToListAsync();

        // Recent CAPAs
        RecentCapas = await _dbContext.Capas.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(5)
            .Select(c => new CapaItem(c.Title, c.CapaNumber, c.Status.ToString(), c.CreatedAt.ToString("MMM dd, yyyy")))
            .ToListAsync();

        // Recent activity (read-only)
        Activity = await _dbContext.AuditLogEntries.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Timestamp)
            .Take(6)
            .Select(a => new ActivityItem(
                a.Action.Replace("_", " "),
                a.Description ?? a.EntityType,
                a.Timestamp.ToString("MMM dd, HH:mm")))
            .ToListAsync();

        DocumentsTrendJson = await BuildDocumentTrendAsync(tenantId);
        ComplianceStatusJson = await BuildComplianceStatusAsync(tenantId);
    }

    private static string SerializeChart(IEnumerable<string> labels, IEnumerable<int> values)
    {
        return JsonSerializer.Serialize(new
        {
            labels,
            values
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public record StatCard(string Title, string Value, string Subtitle);
    public record DocumentItem(string Title, string Number, string Status, string Created);
    public record CapaItem(string Title, string Number, string Status, string Created);
    public record ActivityItem(string Title, string Description, string When);

    private async Task<User?> GetCurrentUserAsync()
    {
        if (_currentUserService.UserId.HasValue)
        {
            return await _dbContext.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId.Value);
        }

        return null;
    }

    private async Task<string> BuildDocumentTrendAsync(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        var start = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1).AddMonths(-5), DateTimeKind.Utc);
        var months = Enumerable.Range(0, 6)
            .Select(i => start.AddMonths(i))
            .ToList();

        var grouped = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CreatedAt >= start)
            .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        var labels = months.Select(m => m.ToString("MMM")).ToArray();
        var values = months
            .Select(m => grouped.FirstOrDefault(g => g.Year == m.Year && g.Month == m.Month)?.Count ?? 0)
            .ToArray();

        return SerializeChart(labels, values);
    }

    private async Task<string> BuildComplianceStatusAsync(Guid tenantId)
    {
        var total = await _dbContext.Documents.CountAsync(d => d.TenantId == tenantId);
        var approved = await _dbContext.Documents.CountAsync(d =>
            d.TenantId == tenantId && d.Status == DocumentStatus.Approved);
        var inReview = await _dbContext.Documents.CountAsync(d =>
            d.TenantId == tenantId && (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview));
        var draft = await _dbContext.Documents.CountAsync(d =>
            d.TenantId == tenantId && d.Status == DocumentStatus.Draft);

        var labels = new[] { "Approved", "In Review", "Draft", "Other" };
        var values = new[]
        {
            approved,
            inReview,
            draft,
            total - approved - inReview - draft
        };

        return SerializeChart(labels, values);
    }
}

