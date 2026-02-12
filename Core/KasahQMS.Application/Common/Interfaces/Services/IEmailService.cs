namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for sending emails.
/// </summary>
public interface IEmailService
{
    Task SendEmailAsync(
        string to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default);
    
    Task SendWelcomeEmailAsync(
        string email,
        string fullName,
        string username,
        string temporaryPassword,
        CancellationToken cancellationToken = default);
    
    Task SendPasswordResetEmailAsync(
        string email,
        string fullName,
        string resetToken,
        CancellationToken cancellationToken = default);
    
    Task SendTaskAssignmentEmailAsync(
        string email,
        string fullName,
        string taskTitle,
        DateTime dueDate,
        string? assignedBy,
        CancellationToken cancellationToken = default);
    
    Task SendDocumentApprovalRequestEmailAsync(
        string email,
        string fullName,
        string documentTitle,
        string documentNumber,
        string submittedBy,
        CancellationToken cancellationToken = default);
}
