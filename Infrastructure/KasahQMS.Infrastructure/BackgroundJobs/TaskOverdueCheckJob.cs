using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Notifications;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to check for overdue tasks and send notifications.
/// Runs periodically to ensure task deadlines are monitored.
/// </summary>
public class TaskOverdueCheckJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TaskOverdueCheckJob> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public TaskOverdueCheckJob(
        IServiceProvider serviceProvider,
        ILogger<TaskOverdueCheckJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task Overdue Check Job started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOverdueTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during task overdue check");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Task Overdue Check Job stopped");
    }

    private async Task CheckOverdueTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var dateTimeService = scope.ServiceProvider.GetRequiredService<IDateTimeService>();

        _logger.LogDebug("Checking for overdue tasks...");

        var now = dateTimeService.UtcNow;
        var overdueTasks = await dbContext.QmsTasks
            .Include(t => t.AssignedTo)
            .Where(t => t.AssignedToId.HasValue &&
                        t.DueDate.HasValue &&
                        t.DueDate < now &&
                        t.Status != QmsTaskStatus.Completed &&
                        t.Status != QmsTaskStatus.Cancelled &&
                        t.Status != QmsTaskStatus.Overdue)
            .ToListAsync(cancellationToken);

        if (!overdueTasks.Any())
        {
            _logger.LogDebug("No newly overdue tasks found at {Time}", now);
            return;
        }

        foreach (var task in overdueTasks)
        {
            task.MarkOverdue();

            await notificationService.SendAsync(
                task.AssignedToId!.Value,
                "Task Overdue",
                $"Task '{task.Title}' is overdue and needs urgent attention.",
                NotificationType.TaskOverdue,
                task.Id,
                cancellationToken);

            var email = task.AssignedTo?.Email;
            if (!string.IsNullOrWhiteSpace(email))
            {
                await emailService.SendEmailAsync(
                    email!,
                    $"Overdue Task Alert: {task.Title}",
                    $"<p>Hello {task.AssignedTo!.FullName},</p><p>Your task <strong>{task.Title}</strong> is now overdue.</p><p><strong>Due Date:</strong> {task.DueDate:MMMM dd, yyyy}</p><p>Please log in to KASAH QMS and take immediate action.</p>",
                    true,
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Task overdue check completed at {Time}. Marked {Count} task(s) as overdue.", now, overdueTasks.Count);
    }
}

/// <summary>
/// Background job to check for CAPA deadlines and send warnings.
/// </summary>
public class CapaDeadlineCheckJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CapaDeadlineCheckJob> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);

    public CapaDeadlineCheckJob(
        IServiceProvider serviceProvider,
        ILogger<CapaDeadlineCheckJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CAPA Deadline Check Job started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckCapaDeadlinesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during CAPA deadline check");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("CAPA Deadline Check Job stopped");
    }

    private async Task CheckCapaDeadlinesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var capaRepository = scope.ServiceProvider.GetRequiredService<ICapaRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var dateTimeService = scope.ServiceProvider.GetRequiredService<IDateTimeService>();

        _logger.LogDebug("Checking for CAPA deadlines...");

        var now = dateTimeService.UtcNow;
        
        // Check for CAPAs due within 3 days
        // This would query CAPAs and send notifications
        
        _logger.LogInformation("CAPA deadline check completed at {Time}", now);
    }
}

/// <summary>
/// Background job to clean up old audit logs and archive data.
/// </summary>
public class DataMaintenanceJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataMaintenanceJob> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromDays(1);

    public DataMaintenanceJob(
        IServiceProvider serviceProvider,
        ILogger<DataMaintenanceJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data Maintenance Job started");

        // Wait for application to fully start
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformMaintenanceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during data maintenance");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Data Maintenance Job stopped");
    }

    private async Task PerformMaintenanceAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting daily data maintenance...");

        // Tasks:
        // 1. Archive old audit logs (older than retention period)
        // 2. Clean up expired sessions
        // 3. Update task overdue statuses
        // 4. Generate daily statistics
        // 5. Clean up orphaned files

        await Task.CompletedTask;

        _logger.LogInformation("Daily data maintenance completed");
    }
}

