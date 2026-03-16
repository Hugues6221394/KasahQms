namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for exporting data to PDF, Excel, and CSV formats.
/// </summary>
public interface IExportService
{
    Task<byte[]> ExportToPdfAsync(string templateName, object data, CancellationToken cancellationToken = default);
    Task<byte[]> ExportToExcelAsync(string sheetName, IEnumerable<Dictionary<string, object>> rows, CancellationToken cancellationToken = default);
    Task<byte[]> ExportToCsvAsync(IEnumerable<Dictionary<string, object>> rows, CancellationToken cancellationToken = default);
}
