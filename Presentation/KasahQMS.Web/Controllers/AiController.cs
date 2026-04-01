using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Entities.Chat;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace KasahQMS.Web.Controllers;

[Authorize]
public class AiController : Controller
{
    private readonly IGroqService _groqService;
    private readonly ILogger<AiController> _logger;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    private static readonly string[] SupportedTextExtensions =
    [
        ".txt", ".md", ".markdown", ".csv", ".json", ".xml", ".html", ".htm", ".log", ".docx", ".pdf"
    ];

    private const int MaxAttachmentBytes = 5 * 1024 * 1024;
    private const int MaxAttachmentChars = 12000;
    private const string AttachmentMemoryPrefix = "[[ATTACHMENT_MEMORY]]";

    public AiController(
        IGroqService groqService,
        ILogger<AiController> logger,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _groqService = groqService;
        _logger = logger;
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? conversationId = null, CancellationToken cancellationToken = default)
    {
        var vm = new AiAssistantViewModel
        {
            ConversationId = string.IsNullOrWhiteSpace(conversationId) ? Guid.NewGuid().ToString("N") : conversationId
        };

        var userId = _currentUser.UserId;
        var tenantId = _currentUser.TenantId;
        if (userId.HasValue && tenantId.HasValue)
        {
            vm.Conversations = await LoadConversationsAsync(userId.Value, tenantId.Value, cancellationToken);
            vm.ActiveConversationId = vm.ConversationId;

            var existingHistory = await LoadConversationHistoryAsync(userId.Value, tenantId.Value, vm.ConversationId, cancellationToken);
            if (existingHistory.Count > 0)
            {
                vm.Messages = existingHistory
                    .Where(ShouldRenderMessage)
                    .Select(m => new AiAssistantMessageViewModel
                {
                    Role = m.Role,
                    Content = m.Content
                }).ToList();
                vm.HistoryJson = JsonSerializer.Serialize(existingHistory);
            }
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(
        string prompt,
        string? historyJson,
        string? conversationId,
        IFormFile? attachment,
        CancellationToken cancellationToken)
    {
        var history = ParseHistory(historyJson);
        var vm = new AiAssistantViewModel
        {
            Prompt = prompt ?? string.Empty,
            ConversationId = string.IsNullOrWhiteSpace(conversationId) ? Guid.NewGuid().ToString("N") : conversationId,
            ActiveConversationId = string.IsNullOrWhiteSpace(conversationId) ? Guid.NewGuid().ToString("N") : conversationId,
            Messages = history.Where(ShouldRenderMessage).Select(m => new AiAssistantMessageViewModel
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };

        string? attachmentContext = null;
        string? attachmentSummary = null;
        if (attachment is { Length: > 0 })
        {
            var extractionResult = await TryExtractAttachmentTextAsync(attachment, cancellationToken);
            if (!extractionResult.Success)
            {
                vm.ErrorMessage = extractionResult.ErrorMessage;
                vm.HistoryJson = JsonSerializer.Serialize(history);
                await LoadConversationsForVmAsync(vm, cancellationToken);
                if (IsAjaxRequest())
                {
                    return Json(new AiGenerateResponse
                    {
                        ConversationId = vm.ConversationId,
                        HistoryJson = vm.HistoryJson,
                        Messages = vm.Messages,
                        Conversations = vm.Conversations,
                        AttachmentName = vm.AttachmentName,
                        ErrorMessage = vm.ErrorMessage
                    });
                }
                return View("Index", vm);
            }

            attachmentContext = extractionResult.ExtractedText;
            attachmentSummary = extractionResult.AttachmentSummary;
            vm.AttachmentName = attachment.FileName;
        }

        if (string.IsNullOrWhiteSpace(prompt) && string.IsNullOrWhiteSpace(attachmentContext))
        {
            vm.ErrorMessage = "Please enter a prompt or attach a supported text document.";
            vm.HistoryJson = JsonSerializer.Serialize(history);
            await LoadConversationsForVmAsync(vm, cancellationToken);
            if (IsAjaxRequest())
            {
                return Json(new AiGenerateResponse
                {
                    ConversationId = vm.ConversationId,
                    HistoryJson = vm.HistoryJson,
                    Messages = vm.Messages,
                    Conversations = vm.Conversations,
                    AttachmentName = vm.AttachmentName,
                    ErrorMessage = vm.ErrorMessage
                });
            }
            return View("Index", vm);
        }

        var userPrompt = string.IsNullOrWhiteSpace(prompt)
            ? "Analyze the attached document and provide actionable insights."
            : prompt.Trim();

        var userDisplayMessage = attachmentSummary is null
            ? userPrompt
            : $"{userPrompt}\n\n[Attached: {attachmentSummary}]";

        if (!string.IsNullOrWhiteSpace(attachmentContext))
        {
            history.Add(new AiAssistantMessage
            {
                Role = "system",
                Content = $"{AttachmentMemoryPrefix} File: {attachmentSummary}\n{attachmentContext}"
            });
        }

        history.Add(new AiAssistantMessage { Role = "user", Content = userDisplayMessage });

        try
        {
            var llmMessages = await BuildLlmMessagesAsync(history, cancellationToken);
            var response = await _groqService.GenerateAsync(llmMessages, cancellationToken);
            history.Add(new AiAssistantMessage { Role = "assistant", Content = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI generation failed");
            vm.ErrorMessage = ex is InvalidOperationException
                ? ex.Message
                : "AI request failed. Verify configuration and try again.";
        }

        vm.Messages = history.Where(ShouldRenderMessage).Select(m => new AiAssistantMessageViewModel
        {
            Role = m.Role,
            Content = m.Content
        }).ToList();
        vm.HistoryJson = JsonSerializer.Serialize(history);
        vm.Prompt = string.Empty;

        await SaveConversationAsync(vm.ConversationId, history, cancellationToken);
        await LoadConversationsForVmAsync(vm, cancellationToken);

        if (IsAjaxRequest())
        {
            return Json(new AiGenerateResponse
            {
                ConversationId = vm.ConversationId,
                HistoryJson = vm.HistoryJson,
                Messages = vm.Messages,
                Conversations = vm.Conversations,
                AttachmentName = vm.AttachmentName,
                ErrorMessage = vm.ErrorMessage
            });
        }

        return View("Index", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConversation(string conversationId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var tenantId = _currentUser.TenantId;
        if (userId.HasValue && tenantId.HasValue && !string.IsNullOrWhiteSpace(conversationId))
        {
            var existing = await _db.AiConversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c =>
                    c.TenantId == tenantId.Value &&
                    c.UserId == userId.Value &&
                    c.ConversationKey == conversationId,
                    cancellationToken);

            if (existing != null)
            {
                _db.AiConversations.Remove(existing);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<List<GroqChatMessage>> BuildLlmMessagesAsync(
        IReadOnlyList<AiAssistantMessage> history,
        CancellationToken cancellationToken)
    {
        var context = await BuildSystemContextAsync(cancellationToken);
        var result = new List<GroqChatMessage>
        {
            new() { Role = "system", Content = context }
        };

        foreach (var message in history)
        {
            result.Add(new GroqChatMessage { Role = message.Role, Content = message.Content });
        }

        return result;
    }

    private async Task<string> BuildSystemContextAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var tenantId = _currentUser.TenantId;
        if (userId is null || tenantId is null)
        {
            return "You are KASAH QMS AI Assistant. Provide clear and concise professional support.";
        }

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Roles!)
            .FirstOrDefaultAsync(u => u.Id == userId.Value && u.TenantId == tenantId.Value, cancellationToken);
        if (user is null)
        {
            return "You are KASAH QMS AI Assistant. Provide clear and concise professional support.";
        }

        var pendingTaskStatuses = new[]
        {
            QmsTaskStatus.Open, QmsTaskStatus.InProgress, QmsTaskStatus.Overdue, QmsTaskStatus.AwaitingApproval, QmsTaskStatus.Rejected
        };

        var pendingTasksCount = await _db.QmsTasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId.Value &&
                        (t.AssignedToId == userId.Value || t.CreatedById == userId.Value) &&
                        pendingTaskStatuses.Contains(t.Status))
            .CountAsync(cancellationToken);

        var pendingTasksTitles = await _db.QmsTasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId.Value &&
                        (t.AssignedToId == userId.Value || t.CreatedById == userId.Value) &&
                        pendingTaskStatuses.Contains(t.Status))
            .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
            .Select(t => t.Title)
            .Take(5)
            .ToListAsync(cancellationToken);

        var pendingDocumentsCount = await _db.Documents
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId.Value &&
                        (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview) &&
                        (d.CreatedById == userId.Value ||
                         d.CurrentApproverId == userId.Value ||
                         d.TargetUserId == userId.Value ||
                         (user.OrganizationUnitId.HasValue &&
                          (d.TargetDepartmentId == user.OrganizationUnitId || d.ApproverDepartmentId == user.OrganizationUnitId))))
            .CountAsync(cancellationToken);

        var pendingDocumentsTitles = await _db.Documents
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId.Value &&
                        (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview) &&
                        (d.CreatedById == userId.Value ||
                         d.CurrentApproverId == userId.Value ||
                         d.TargetUserId == userId.Value ||
                         (user.OrganizationUnitId.HasValue &&
                          (d.TargetDepartmentId == user.OrganizationUnitId || d.ApproverDepartmentId == user.OrganizationUnitId))))
            .OrderByDescending(d => d.SubmittedAt ?? d.CreatedAt)
            .Select(d => d.Title)
            .Take(5)
            .ToListAsync(cancellationToken);

        var trainingTodoCount = await _db.TrainingRecords
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId.Value &&
                        t.UserId == userId.Value &&
                        (t.Status == TrainingStatus.Scheduled || t.Status == TrainingStatus.InProgress))
            .CountAsync(cancellationToken);

        var trainingTodoTitles = await _db.TrainingRecords
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId.Value &&
                        t.UserId == userId.Value &&
                        (t.Status == TrainingStatus.Scheduled || t.Status == TrainingStatus.InProgress))
            .OrderBy(t => t.ScheduledDate)
            .Select(t => t.Title)
            .Take(5)
            .ToListAsync(cancellationToken);

        var roles = user.Roles?.Select(r => r.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? [];

        return $"""
                You are KASAH QMS AI Assistant helping a logged-in user.
                User profile:
                - Name: {user.FullName}
                - Roles: {string.Join(", ", roles)}
                - Pending tasks: {pendingTasksCount} ({string.Join("; ", pendingTasksTitles.DefaultIfEmpty("None"))})
                - Pending documents: {pendingDocumentsCount} ({string.Join("; ", pendingDocumentsTitles.DefaultIfEmpty("None"))})
                - Pending/new trainings: {trainingTodoCount} ({string.Join("; ", trainingTodoTitles.DefaultIfEmpty("None"))})

                Rules:
                - Use this context when answering user questions about their workload.
                - If user asks about counts/statuses, cite these current values.
                - Keep answers concise, practical, and business-professional.
                - Do not fabricate inaccessible data.
                """;
    }

    private async Task SaveConversationAsync(string conversationKey, IReadOnlyList<AiAssistantMessage> history, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var tenantId = _currentUser.TenantId;
        if (!userId.HasValue || !tenantId.HasValue || string.IsNullOrWhiteSpace(conversationKey) || history.Count == 0)
        {
            return;
        }

        var conversation = await _db.AiConversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c =>
                c.TenantId == tenantId.Value &&
                c.UserId == userId.Value &&
                c.ConversationKey == conversationKey,
                cancellationToken);

        var now = DateTime.UtcNow;
        var titleSource = history.FirstOrDefault(m => m.Role == "user")?.Content ?? "New chat";
        var title = titleSource.Length > 80 ? titleSource[..80] : titleSource;

        if (conversation == null)
        {
            conversation = new AiConversation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                UserId = userId.Value,
                ConversationKey = conversationKey,
                Title = string.IsNullOrWhiteSpace(title) ? "New chat" : title,
                CreatedAt = now,
                UpdatedAt = now,
                Messages = new List<AiConversationMessage>()
            };
            _db.AiConversations.Add(conversation);
        }
        else
        {
            conversation.Title = string.IsNullOrWhiteSpace(title) ? conversation.Title : title;
            conversation.UpdatedAt = now;
            _db.AiConversationMessages.RemoveRange(conversation.Messages ?? []);
            conversation.Messages = new List<AiConversationMessage>();
        }

        foreach (var message in history)
        {
            conversation.Messages!.Add(new AiConversationMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Role = message.Role,
                Content = message.Content.Length > 12000 ? message.Content[..12000] : message.Content,
                CreatedAt = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<AiConversationSummaryViewModel>> LoadConversationsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        return await _db.AiConversations
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new AiConversationSummaryViewModel
            {
                ConversationId = c.ConversationKey,
                Title = c.Title,
                UpdatedAt = c.UpdatedAt,
                LastMessagePreview = c.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Content)
                    .FirstOrDefault() ?? string.Empty
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<AiAssistantMessage>> LoadConversationHistoryAsync(Guid userId, Guid tenantId, string conversationId, CancellationToken cancellationToken)
    {
        var existing = await _db.AiConversations
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.UserId == userId && c.ConversationKey == conversationId)
            .Select(c => c.Messages!
                .OrderBy(m => m.CreatedAt)
                .Select(m => new AiAssistantMessage { Role = m.Role, Content = m.Content })
                .ToList())
            .FirstOrDefaultAsync(cancellationToken);

        return existing ?? [];
    }

    private async Task LoadConversationsForVmAsync(AiAssistantViewModel vm, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var tenantId = _currentUser.TenantId;
        if (!userId.HasValue || !tenantId.HasValue)
        {
            vm.Conversations = new List<AiConversationSummaryViewModel>();
            vm.ActiveConversationId = vm.ConversationId;
            return;
        }

        vm.Conversations = await LoadConversationsAsync(userId.Value, tenantId.Value, cancellationToken);
        vm.ActiveConversationId = vm.ConversationId;
    }

    private static async Task<AttachmentExtractionResult> TryExtractAttachmentTextAsync(IFormFile attachment, CancellationToken cancellationToken)
    {
        if (attachment.Length > MaxAttachmentBytes)
        {
            return AttachmentExtractionResult.Failure("Attachment too large. Maximum size is 5 MB.");
        }

        var extension = Path.GetExtension(attachment.FileName).ToLowerInvariant();
        if (!SupportedTextExtensions.Contains(extension))
        {
            return AttachmentExtractionResult.Failure("Unsupported attachment format. Supported: .txt, .md, .csv, .json, .xml, .html, .log, .docx, .pdf");
        }

        await using var stream = attachment.OpenReadStream();
        var content = extension switch
        {
            ".docx" => await ExtractDocxTextAsync(stream, cancellationToken),
            ".pdf" => await ExtractPdfTextAsync(stream, cancellationToken),
            _ => await ReadPlainTextAsync(stream, cancellationToken)
        };

        if (string.IsNullOrWhiteSpace(content))
        {
            return AttachmentExtractionResult.Failure("The attached document is empty.");
        }

        if (content.Length > MaxAttachmentChars)
        {
            content = content[..MaxAttachmentChars];
        }

        return AttachmentExtractionResult.Successful(content, attachment.FileName);
    }

    private static async Task<string> ReadPlainTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async Task<string> ExtractDocxTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var documentXml = archive.GetEntry("word/document.xml");
        if (documentXml == null)
        {
            return string.Empty;
        }

        await using var xmlStream = documentXml.Open();
        using var reader = new StreamReader(xmlStream);
        var xml = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(xml))
        {
            return string.Empty;
        }

        var xdoc = XDocument.Parse(xml);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = xdoc
            .Descendants(w + "p")
            .Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value)).Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p));

        return string.Join(Environment.NewLine, paragraphs);
    }

    private static async Task<string> ExtractPdfTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        using var document = PdfDocument.Open(memory);
        var pages = document.GetPages().Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t));
        return string.Join(Environment.NewLine + Environment.NewLine, pages);
    }

    private static List<AiAssistantMessage> ParseHistory(string? historyJson)
    {
        if (string.IsNullOrWhiteSpace(historyJson))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<AiAssistantMessage>>(historyJson) ?? [];
            return parsed
                .Where(m => (m.Role == "user" || m.Role == "assistant" || m.Role == "system") && !string.IsNullOrWhiteSpace(m.Content))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static bool ShouldRenderMessage(AiAssistantMessage message)
    {
        if (string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (message.Content.StartsWith(AttachmentMemoryPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }
}

public class AiAssistantViewModel
{
    public string Prompt { get; set; } = string.Empty;
    public string ConversationId { get; set; } = Guid.NewGuid().ToString("N");
    public string? ActiveConversationId { get; set; }
    public string HistoryJson { get; set; } = "[]";
    public List<AiAssistantMessageViewModel> Messages { get; set; } = new();
    public List<AiConversationSummaryViewModel> Conversations { get; set; } = new();
    public string? AttachmentName { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AiConversationSummaryViewModel
{
    public string ConversationId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string LastMessagePreview { get; set; } = string.Empty;
}

public class AiAssistantMessageViewModel
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class AiGenerateResponse
{
    public string ConversationId { get; set; } = string.Empty;
    public string HistoryJson { get; set; } = "[]";
    public List<AiAssistantMessageViewModel> Messages { get; set; } = new();
    public List<AiConversationSummaryViewModel> Conversations { get; set; } = new();
    public string? AttachmentName { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AiAssistantMessage : GroqChatMessage
{
}

public record AttachmentExtractionResult(bool Success, string? ExtractedText, string? AttachmentSummary, string? ErrorMessage)
{
    public static AttachmentExtractionResult Successful(string text, string attachmentSummary) =>
        new(true, text, attachmentSummary, null);

    public static AttachmentExtractionResult Failure(string errorMessage) =>
        new(false, null, null, errorMessage);
}
