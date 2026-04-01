using System.Security.Claims;
using System.IO.Compression;
using System.Text.Json;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Privacy;

public class DataExportModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public DataExportModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public List<ExportRow> Exports { get; set; } = new();
    public ExportPreview Preview { get; set; } = ExportPreview.Empty;

    public async Task OnGetAsync()
    {
        var userId = GetUserId();
        var tenantId = _currentUserService.TenantId;
        if (userId == null || tenantId == null) return;

        var requests = await _dbContext.DataExportRequests.AsNoTracking()
            .Where(r => r.UserId == userId.Value)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        Preview = await BuildExportPreviewAsync(userId.Value, tenantId.Value);

        Exports = requests.Select(r => new ExportRow(
            r.Id,
            r.RequestedAt,
            r.Status.ToString(),
            r.CompletedAt,
            r.DownloadUrl,
            r.ExpiresAt,
            Preview.TotalRecords
        )).ToList();
    }

    public async Task<IActionResult> OnGetDownloadAsync(Guid id)
    {
        var userId = GetUserId();
        var tenantId = _currentUserService.TenantId;
        if (userId == null || tenantId == null) return Unauthorized();

        var request = await _dbContext.DataExportRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId.Value);
        if (request == null) return NotFound();
        if (request.Status != Domain.Enums.DataExportStatus.Completed) return BadRequest("Export is not ready yet.");
        if (request.ExpiresAt != null && request.ExpiresAt <= DateTime.UtcNow) return BadRequest("Export link has expired.");

        var payload = await BuildExportPayloadAsync(userId.Value, tenantId.Value);
        var zipBytes = BuildCsvZip(payload);
        var fileName = $"kasah-qms-data-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        return File(zipBytes, "application/zip", fileName);
    }

    private async Task<Dictionary<string, object?>> BuildExportPayloadAsync(Guid userId, Guid tenantId)
    {
        var user = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        var roleNames = await _dbContext.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Join(_dbContext.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
            .Distinct()
            .ToListAsync();

        var organizationUnitName = await _dbContext.OrganizationUnits.AsNoTracking()
            .Where(ou => user != null && ou.Id == user.OrganizationUnitId)
            .Select(ou => ou.Name)
            .FirstOrDefaultAsync();

        var documents = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CreatedById == userId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id,
                d.DocumentNumber,
                d.Title,
                Status = d.Status.ToString(),
                d.CreatedAt,
                d.SubmittedAt,
                d.ApprovedAt,
                d.CurrentVersion
            })
            .ToListAsync();

        var tasks = await _dbContext.QmsTasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId && (t.CreatedById == userId || t.AssignedToId == userId))
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.TaskNumber,
                t.Title,
                Status = t.Status.ToString(),
                Priority = t.Priority.ToString(),
                t.CreatedAt,
                t.DueDate,
                t.CompletedAt
            })
            .ToListAsync();

        var audits = await _dbContext.Audits.AsNoTracking()
            .Where(a => a.TenantId == tenantId && (a.CreatedById == userId || a.LeadAuditorId == userId))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.AuditNumber,
                a.Title,
                Type = a.AuditType.ToString(),
                Status = a.Status.ToString(),
                a.PlannedStartDate,
                a.PlannedEndDate,
                a.ActualStartDate,
                a.ActualEndDate
            })
            .ToListAsync();

        var capas = await _dbContext.Capas.AsNoTracking()
            .Where(c => c.TenantId == tenantId && (c.CreatedById == userId || c.OwnerId == userId))
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.CapaNumber,
                c.Title,
                Status = c.Status.ToString(),
                Priority = c.Priority.ToString(),
                c.CreatedAt,
                c.TargetCompletionDate,
                c.ActualCompletionDate
            })
            .ToListAsync();

        var chatMessages = await _dbContext.ChatMessages.AsNoTracking()
            .Where(m => m.SenderId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(500)
            .Select(m => new
            {
                m.Id,
                m.ThreadId,
                m.Content,
                m.CreatedAt,
                m.EditedAt,
                m.IsDeleted
            })
            .ToListAsync();

        var trainings = await _dbContext.TrainingRecords.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.UserId == userId)
            .OrderByDescending(t => t.ScheduledDate)
            .Select(t => new
            {
                t.Id,
                t.Title,
                Type = t.TrainingType.ToString(),
                Status = t.Status.ToString(),
                t.ScheduledDate,
                t.CompletedDate,
                t.ExpiryDate,
                t.Score
            })
            .ToListAsync();

        var notifications = await _dbContext.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(1000)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                Type = n.Type.ToString(),
                n.CreatedAt,
                n.IsRead,
                n.ReadAt,
                n.RelatedEntityId,
                n.RelatedEntityType
            })
            .ToListAsync();

        var sessions = await _dbContext.UserSessions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.CreatedAt,
                s.ExpiresAt,
                s.LastActivityAt,
                s.IsRevoked,
                s.RevokedAt,
                s.IpAddress,
                s.DeviceInfo,
                s.Browser,
                s.OperatingSystem
            })
            .ToListAsync();

        var consents = await _dbContext.ConsentRecords.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.GrantedAt ?? c.RevokedAt)
            .Select(c => new
            {
                Type = c.ConsentType.ToString(),
                c.IsGranted,
                c.GrantedAt,
                c.RevokedAt,
                c.IpAddress
            })
            .ToListAsync();

        var auditLogs = await _dbContext.AuditLogEntries.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .Take(2000)
            .Select(a => new
            {
                a.Id,
                a.Timestamp,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.Description,
                a.IsSuccessful
            })
            .ToListAsync();

        var exportRequests = await _dbContext.DataExportRequests.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new
            {
                r.Id,
                r.RequestedAt,
                Status = r.Status.ToString(),
                r.CompletedAt,
                r.ExpiresAt
            })
            .ToListAsync();

        return new Dictionary<string, object?>
        {
            ["meta"] = new
            {
                generatedAtUtc = DateTime.UtcNow,
                tenantId,
                exportScope = "User comprehensive export",
                totalRecords = documents.Count + tasks.Count + audits.Count + capas.Count + chatMessages.Count +
                               trainings.Count + notifications.Count + sessions.Count + consents.Count + auditLogs.Count + exportRequests.Count
            },
            ["profile"] = new
            {
                user?.Id,
                user?.Email,
                user?.FirstName,
                user?.LastName,
                user?.FullName,
                user?.PhoneNumber,
                user?.JobTitle,
                user?.IsActive,
                user?.CreatedAt,
                user?.LastLoginAt,
                organizationUnitName,
                roleNames
            },
            ["documents"] = documents,
            ["tasks"] = tasks,
            ["audits"] = audits,
            ["capas"] = capas,
            ["trainings"] = trainings,
            ["notifications"] = notifications,
            ["chatMessages"] = chatMessages,
            ["activeSessions"] = sessions,
            ["consents"] = consents,
            ["auditLogs"] = auditLogs,
            ["exportHistory"] = exportRequests
        };
    }

    private async Task<ExportPreview> BuildExportPreviewAsync(Guid userId, Guid tenantId)
    {
        var documents = await _dbContext.Documents.CountAsync(d => d.TenantId == tenantId && d.CreatedById == userId);
        var tasks = await _dbContext.QmsTasks.CountAsync(t => t.TenantId == tenantId && (t.CreatedById == userId || t.AssignedToId == userId));
        var audits = await _dbContext.Audits.CountAsync(a => a.TenantId == tenantId && (a.CreatedById == userId || a.LeadAuditorId == userId));
        var capas = await _dbContext.Capas.CountAsync(c => c.TenantId == tenantId && (c.CreatedById == userId || c.OwnerId == userId));
        var trainings = await _dbContext.TrainingRecords.CountAsync(t => t.TenantId == tenantId && t.UserId == userId);
        var notifications = await _dbContext.Notifications.CountAsync(n => n.UserId == userId);
        var messages = await _dbContext.ChatMessages.CountAsync(m => m.SenderId == userId);
        var sessions = await _dbContext.UserSessions.CountAsync(s => s.UserId == userId);
        var consents = await _dbContext.ConsentRecords.CountAsync(c => c.UserId == userId);
        var logs = await _dbContext.AuditLogEntries.CountAsync(a => a.UserId == userId);

        return new ExportPreview(
            documents,
            tasks,
            audits,
            capas,
            trainings,
            notifications,
            messages,
            sessions,
            consents,
            logs);
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : null;
    }

    private static byte[] BuildCsvZip(Dictionary<string, object?> payload)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, section) in payload)
            {
                if (section == null) continue;
                var element = JsonSerializer.SerializeToElement(section);
                var csv = ConvertJsonElementToCsv(element);
                if (string.IsNullOrWhiteSpace(csv)) continue;

                var entry = archive.CreateEntry($"{SanitizeFileName(name)}.csv", CompressionLevel.Optimal);
                using var stream = entry.Open();
                using var writer = new StreamWriter(stream);
                writer.Write(csv);
            }
        }
        return memory.ToArray();
    }

    private static string ConvertJsonElementToCsv(JsonElement element)
    {
        var rows = new List<Dictionary<string, string?>>();

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    FlattenObject(item, row, "");
                    rows.Add(row);
                }
                else
                {
                    rows.Add(new Dictionary<string, string?> { ["value"] = item.ToString() });
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            FlattenObject(element, row, "");
            rows.Add(row);
        }
        else
        {
            rows.Add(new Dictionary<string, string?> { ["value"] = element.ToString() });
        }

        if (rows.Count == 0) return string.Empty;

        var headers = rows.SelectMany(r => r.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();
        var lines = new List<string> { string.Join(",", headers.Select(EscapeCsv)) };
        lines.AddRange(rows.Select(row => string.Join(",", headers.Select(h => EscapeCsv(row.TryGetValue(h, out var v) ? v : "")))));
        return string.Join(Environment.NewLine, lines);
    }

    private static void FlattenObject(JsonElement element, IDictionary<string, string?> row, string prefix)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = string.IsNullOrWhiteSpace(prefix) ? property.Name : $"{prefix}.{property.Name}";
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    FlattenObject(property.Value, row, key);
                    break;
                case JsonValueKind.Array:
                    row[key] = property.Value.ToString();
                    break;
                case JsonValueKind.String:
                    row[key] = property.Value.GetString();
                    break;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    row[key] = property.Value.ToString();
                    break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    row[key] = string.Empty;
                    break;
                default:
                    row[key] = property.Value.ToString();
                    break;
            }
        }
    }

    private static string EscapeCsv(string? value)
    {
        var input = value ?? string.Empty;
        if (input.Contains('"') || input.Contains(',') || input.Contains('\n') || input.Contains('\r'))
        {
            return $"\"{input.Replace("\"", "\"\"")}\"";
        }
        return input;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    public record ExportRow(
        Guid Id,
        DateTime RequestedAt,
        string Status,
        DateTime? CompletedAt,
        string? DownloadUrl,
        DateTime? ExpiresAt,
        int TotalRecords);

    public record ExportPreview(
        int Documents,
        int Tasks,
        int Audits,
        int Capas,
        int Trainings,
        int Notifications,
        int ChatMessages,
        int Sessions,
        int Consents,
        int AuditLogs)
    {
        public static readonly ExportPreview Empty = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        public int TotalRecords => Documents + Tasks + Audits + Capas + Trainings + Notifications + ChatMessages + Sessions + Consents + AuditLogs;
    }
}
