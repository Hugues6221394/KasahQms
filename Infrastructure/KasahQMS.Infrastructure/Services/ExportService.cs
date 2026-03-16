using System.IO.Compression;
using System.Text;
using KasahQMS.Application.Common.Interfaces.Services;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// Export service supporting PDF (HTML), Excel (XLSX), and CSV formats.
/// </summary>
public class ExportService : IExportService
{
    public Task<byte[]> ExportToPdfAsync(
        string templateName, object data, CancellationToken cancellationToken = default)
    {
        // Generate HTML document (integrate with a PDF rendering library for production use)
        var html = GenerateHtmlDocument(templateName, data);
        return Task.FromResult(Encoding.UTF8.GetBytes(html));
    }

    public Task<byte[]> ExportToExcelAsync(
        string sheetName, IEnumerable<Dictionary<string, object>> rows,
        CancellationToken cancellationToken = default)
    {
        var rowList = rows.ToList();

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddZipEntry(archive, "[Content_Types].xml", ContentTypesXml());
            AddZipEntry(archive, "_rels/.rels", RelsXml());
            AddZipEntry(archive, "xl/workbook.xml", WorkbookXml(sheetName));
            AddZipEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml());
            AddZipEntry(archive, "xl/worksheets/sheet1.xml", WorksheetXml(rowList));
        }

        stream.Position = 0;
        return Task.FromResult(stream.ToArray());
    }

    public Task<byte[]> ExportToCsvAsync(
        IEnumerable<Dictionary<string, object>> rows,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var rowList = rows.ToList();

        if (rowList.Count == 0)
            return Task.FromResult(Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(string.Empty)).ToArray());

        // Header
        var headers = rowList[0].Keys.ToList();
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsvField)));

        // Data rows
        foreach (var row in rowList)
        {
            var values = headers.Select(h =>
                row.TryGetValue(h, out var v) ? v?.ToString() ?? "" : "");
            sb.AppendLine(string.Join(",", values.Select(EscapeCsvField)));
        }

        var bom = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[bom.Length + content.Length];
        bom.CopyTo(result, 0);
        content.CopyTo(result, bom.Length);
        return Task.FromResult(result);
    }

    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }

    private static string GenerateHtmlDocument(string templateName, object data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head>");
        sb.AppendLine($"<title>{System.Net.WebUtility.HtmlEncode(templateName)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Arial,sans-serif;margin:40px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%}");
        sb.AppendLine("th,td{border:1px solid #ddd;padding:8px;text-align:left}");
        sb.AppendLine("th{background-color:#4472C4;color:white}");
        sb.AppendLine("</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h1>{System.Net.WebUtility.HtmlEncode(templateName)}</h1>");
        sb.AppendLine($"<p>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");

        if (data is IEnumerable<Dictionary<string, object>> rows)
        {
            var rowList = rows.ToList();
            if (rowList.Count > 0)
            {
                sb.AppendLine("<table><thead><tr>");
                foreach (var key in rowList[0].Keys)
                    sb.AppendLine($"<th>{System.Net.WebUtility.HtmlEncode(key)}</th>");
                sb.AppendLine("</tr></thead><tbody>");

                foreach (var row in rowList)
                {
                    sb.AppendLine("<tr>");
                    foreach (var val in row.Values)
                        sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(val?.ToString() ?? "")}</td>");
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</tbody></table>");
            }
        }
        else
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            sb.AppendLine($"<pre>{System.Net.WebUtility.HtmlEncode(json)}</pre>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    #region XLSX XML Templates

    private static string ContentTypesXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
        </Types>
        """;

    private static string RelsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string WorkbookXml(string sheetName) =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="{System.Security.SecurityElement.Escape(sheetName)}" sheetId="1" r:id="rId1"/>
          </sheets>
        </workbook>
        """;

    private static string WorkbookRelsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
        </Relationships>
        """;

    private static string WorksheetXml(List<Dictionary<string, object>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.AppendLine("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
        sb.AppendLine("<sheetData>");

        if (rows.Count > 0)
        {
            var headers = rows[0].Keys.ToList();

            // Header row
            sb.AppendLine("<row>");
            foreach (var header in headers)
                sb.AppendLine($"""<c t="inlineStr"><is><t>{System.Security.SecurityElement.Escape(header)}</t></is></c>""");
            sb.AppendLine("</row>");

            // Data rows
            foreach (var row in rows)
            {
                sb.AppendLine("<row>");
                foreach (var header in headers)
                {
                    var value = row.TryGetValue(header, out var v) ? v?.ToString() ?? "" : "";
                    sb.AppendLine($"""<c t="inlineStr"><is><t>{System.Security.SecurityElement.Escape(value)}</t></is></c>""");
                }
                sb.AppendLine("</row>");
            }
        }

        sb.AppendLine("</sheetData>");
        sb.AppendLine("</worksheet>");
        return sb.ToString();
    }

    #endregion

    private static void AddZipEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
