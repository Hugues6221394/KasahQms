using KasahQMS.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// Email service implementation.
/// In production, integrate with SMTP or email provider (SendGrid, AWS SES, etc.)
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Implement actual email sending via SMTP or email provider
            // For now, log the email attempt
            _logger.LogInformation(
                "Email would be sent - To: {To}, Subject: {Subject}",
                to, subject);

            // In production:
            // var smtpSettings = _configuration.GetSection("Smtp");
            // using var client = new SmtpClient(smtpSettings["Host"], int.Parse(smtpSettings["Port"]));
            // ...

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            throw;
        }
    }

    public async Task SendWelcomeEmailAsync(
        string email,
        string fullName,
        string username,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        var subject = "Welcome to Kasah QMS";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h1 style='color: #0c88e8;'>Welcome to Kasah QMS</h1>
                    <p>Dear {fullName},</p>
                    <p>Your account has been created in the Kasah Quality Management System.</p>
                    <div style='background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p><strong>Username:</strong> {username}</p>
                        <p><strong>Temporary Password:</strong> {temporaryPassword}</p>
                    </div>
                    <p style='color: #e05c4c;'><strong>Important:</strong> You will be required to change your password upon first login.</p>
                    <p>For security reasons, please do not share your credentials with anyone.</p>
                    <p>If you have any questions, please contact your system administrator.</p>
                    <hr style='border: none; border-top: 1px solid #ddd; margin: 30px 0;'>
                    <p style='font-size: 12px; color: #666;'>
                        This is an automated message from Kasah QMS. Please do not reply to this email.
                    </p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(email, subject, body, true, cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(
        string email,
        string fullName,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["Application:BaseUrl"] ?? "https://qms.kasah.tech";
        var resetLink = $"{baseUrl}/Account/ResetPassword?token={resetToken}";

        var subject = "Password Reset Request - Kasah QMS";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h1 style='color: #0c88e8;'>Password Reset</h1>
                    <p>Dear {fullName},</p>
                    <p>We received a request to reset your password for your Kasah QMS account.</p>
                    <p>Click the button below to reset your password:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{resetLink}' style='background-color: #0c88e8; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                            Reset Password
                        </a>
                    </div>
                    <p>This link will expire in 24 hours.</p>
                    <p style='color: #e05c4c;'>If you did not request this password reset, please ignore this email or contact your system administrator if you have concerns.</p>
                    <hr style='border: none; border-top: 1px solid #ddd; margin: 30px 0;'>
                    <p style='font-size: 12px; color: #666;'>
                        This is an automated message from Kasah QMS. Please do not reply to this email.
                    </p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(email, subject, body, true, cancellationToken);
    }

    public async Task SendTaskAssignmentEmailAsync(
        string email,
        string fullName,
        string taskTitle,
        DateTime dueDate,
        string? assignedBy,
        CancellationToken cancellationToken = default)
    {
        var subject = $"New Task Assigned: {taskTitle}";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h1 style='color: #0c88e8;'>New Task Assignment</h1>
                    <p>Dear {fullName},</p>
                    <p>A new task has been assigned to you:</p>
                    <div style='background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p><strong>Task:</strong> {taskTitle}</p>
                        <p><strong>Due Date:</strong> {dueDate:MMMM dd, yyyy}</p>
                        {(assignedBy != null ? $"<p><strong>Assigned By:</strong> {assignedBy}</p>" : "")}
                    </div>
                    <p>Please log in to Kasah QMS to view the task details and get started.</p>
                    <hr style='border: none; border-top: 1px solid #ddd; margin: 30px 0;'>
                    <p style='font-size: 12px; color: #666;'>
                        This is an automated message from Kasah QMS.
                    </p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(email, subject, body, true, cancellationToken);
    }

    public async Task SendDocumentApprovalRequestEmailAsync(
        string email,
        string fullName,
        string documentTitle,
        string documentNumber,
        string submittedBy,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Document Approval Required: {documentTitle}";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h1 style='color: #0c88e8;'>Document Approval Request</h1>
                    <p>Dear {fullName},</p>
                    <p>A document has been submitted for your approval:</p>
                    <div style='background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p><strong>Document:</strong> {documentTitle}</p>
                        <p><strong>Document Number:</strong> {documentNumber}</p>
                        <p><strong>Submitted By:</strong> {submittedBy}</p>
                    </div>
                    <p>Please log in to Kasah QMS to review and approve/reject this document.</p>
                    <hr style='border: none; border-top: 1px solid #ddd; margin: 30px 0;'>
                    <p style='font-size: 12px; color: #666;'>
                        This is an automated message from Kasah QMS.
                    </p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(email, subject, body, true, cancellationToken);
    }
}

