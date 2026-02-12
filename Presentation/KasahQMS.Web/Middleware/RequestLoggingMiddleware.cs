using System.Diagnostics;

namespace KasahQMS.Web.Middleware;

/// <summary>
/// Middleware for structured request/response logging.
/// Provides observability for all HTTP requests with timing and context.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Generate correlation ID for request tracing
        var correlationId = context.TraceIdentifier;
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path;
        var requestMethod = context.Request.Method;

        try
        {
            await _next(context);
            stopwatch.Stop();

            var statusCode = context.Response.StatusCode;
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // Log with appropriate level based on status code
            if (statusCode >= 500)
            {
                _logger.LogError(
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [CorrelationId: {CorrelationId}]",
                    requestMethod, requestPath, statusCode, elapsedMs, correlationId);
            }
            else if (statusCode >= 400)
            {
                _logger.LogWarning(
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [CorrelationId: {CorrelationId}]",
                    requestMethod, requestPath, statusCode, elapsedMs, correlationId);
            }
            else if (elapsedMs > 1000)
            {
                // Log slow requests as warnings
                _logger.LogWarning(
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms (SLOW) [CorrelationId: {CorrelationId}]",
                    requestMethod, requestPath, statusCode, elapsedMs, correlationId);
            }
            else
            {
                _logger.LogInformation(
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [CorrelationId: {CorrelationId}]",
                    requestMethod, requestPath, statusCode, elapsedMs, correlationId);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(
                ex,
                "HTTP {Method} {Path} threw exception after {ElapsedMs}ms [CorrelationId: {CorrelationId}]",
                requestMethod, requestPath, stopwatch.ElapsedMilliseconds, correlationId);

            throw; // Re-throw to let exception handling middleware deal with it
        }
    }
}

/// <summary>
/// Extension methods for RequestLoggingMiddleware.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}

