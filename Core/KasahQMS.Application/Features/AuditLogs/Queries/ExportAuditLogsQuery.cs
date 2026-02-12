using System.Linq;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.AuditLog;

using MediatR;

using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.AuditLogs.Queries;

public record ExportAuditLogsQuery(
    DateTime? StartDate,
    DateTime? EndDate,
    Guid? DepartmentId,
    string? DocumentType,
    string Format = "PDF") : IRequest<Result<byte[]>>;

public class ExportAuditLogsQueryHandler : IRequestHandler<ExportAuditLogsQuery, Result<byte[]>>
{
    private readonly IAuditLogEntryRepository _auditLogRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ExportAuditLogsQueryHandler> _logger;

    public ExportAuditLogsQueryHandler(
        IAuditLogEntryRepository auditLogRepository,
        ICurrentUserService currentUserService,
        ILogger<ExportAuditLogsQueryHandler> logger)
    {
        _auditLogRepository = auditLogRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<byte[]>> Handle(ExportAuditLogsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
            {
                return Result.Failure<byte[]>(Error.Unauthorized);
            }

            var logs = await _auditLogRepository.GetFilteredAsync(
                tenantId.Value,
                request.StartDate,
                request.EndDate,
                null, // userId - could be filtered by department later
                null, // actionType
                request.DocumentType,
                null, // isSuccessful
                cancellationToken);

            // For now, return a simple text export
            // In production, use a PDF library like QuestPDF or iTextSharp
            var exportContent = GenerateExportContent(logs.ToList(), request.Format);

            return Result.Success(exportContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs");
            return Result.Failure<byte[]>(Error.Custom("Export.Failed", "Failed to export audit logs."));
        }
    }

    private byte[] GenerateExportContent(List<AuditLogEntry> logs, string format)
    {
        if (format.ToUpper() == "PDF")
        {
            // Simple text representation - in production, use PDF library
            var content = $"AUDIT LOG EXPORT\n" +
                         $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n" +
                         $"Total Entries: {logs.Count}\n\n";

            foreach (var log in logs)
            {
                content += $"[{log.Timestamp:yyyy-MM-dd HH:mm:ss}] {log.Action} - {log.EntityType}\n";
                if (!string.IsNullOrWhiteSpace(log.Description))
                {
                    content += $"  Description: {log.Description}\n";
                }
                content += "\n";
            }

            return System.Text.Encoding.UTF8.GetBytes(content);
        }
        else // ZIP format
        {
            // For ZIP, create a structured export
            var content = System.Text.Json.JsonSerializer.Serialize(logs, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            return System.Text.Encoding.UTF8.GetBytes(content);
        }
    }
}
