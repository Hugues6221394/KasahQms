using System.Net;
using System.Text.Json;

namespace KasahQMS.Web.Middleware;

/// <summary>
/// Global exception handling middleware.
/// Provides consistent error responses and logging for all unhandled exceptions.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.TraceIdentifier;

        // Log the exception with full details
        _logger.LogError(
            exception,
            "Unhandled exception occurred [CorrelationId: {CorrelationId}] [Path: {Path}] [User: {User}]",
            correlationId,
            context.Request.Path,
            context.User.Identity?.Name ?? "Anonymous");

        // Determine status code and message based on exception type
        var (statusCode, message) = GetStatusCodeAndMessage(exception);

        context.Response.StatusCode = (int)statusCode;

        // For API requests, return JSON
        if (context.Request.Path.StartsWithSegments("/api") ||
            context.Request.Headers["Accept"].Any(h => h?.Contains("application/json") ?? false))
        {
            context.Response.ContentType = "application/json";

            var response = new ErrorResponse
            {
                Status = (int)statusCode,
                Title = GetErrorTitle(statusCode),
                Message = message,
                CorrelationId = correlationId,
                Details = _environment.IsDevelopment() ? exception.ToString() : null
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
        else
        {
            // For web requests, redirect to error page
            context.Response.Redirect($"/Error?statusCode={(int)statusCode}&correlationId={correlationId}");
        }
    }

    private (HttpStatusCode statusCode, string message) GetStatusCodeAndMessage(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "You do not have permission to perform this action."),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, "The requested resource was not found."),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message),
            TimeoutException => (HttpStatusCode.RequestTimeout, "The request timed out. Please try again."),
            OperationCanceledException => (HttpStatusCode.BadRequest, "The operation was cancelled."),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.")
        };
    }

    private string GetErrorTitle(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "Bad Request",
            HttpStatusCode.Unauthorized => "Unauthorized",
            HttpStatusCode.Forbidden => "Forbidden",
            HttpStatusCode.NotFound => "Not Found",
            HttpStatusCode.RequestTimeout => "Request Timeout",
            HttpStatusCode.InternalServerError => "Internal Server Error",
            _ => "Error"
        };
    }
}

/// <summary>
/// Standard error response structure.
/// </summary>
public class ErrorResponse
{
    public int Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string? Details { get; set; }
}

/// <summary>
/// Extension methods for ExceptionHandlingMiddleware.
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}

