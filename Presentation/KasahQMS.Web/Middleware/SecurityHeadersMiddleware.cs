namespace KasahQMS.Web.Middleware;

/// <summary>
/// Middleware to add security headers to all responses.
/// Implements enterprise-grade security headers for protection against common web vulnerabilities.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before processing the request
        AddSecurityHeaders(context.Response.Headers);

        await _next(context);
    }

    private void AddSecurityHeaders(IHeaderDictionary headers)
    {
        // Strict Transport Security - Enforce HTTPS for 1 year
        // includeSubDomains ensures all subdomains are also HTTPS
        // preload allows submission to browser preload lists
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";

        // Content Security Policy - Restrict resource loading
        // This is a strict CSP that should be adjusted based on your needs
        headers["Content-Security-Policy"] = string.Join("; ", new[]
        {
            "default-src 'self'",
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.tailwindcss.com https://cdn.jsdelivr.net",
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
            "font-src 'self' https://fonts.gstatic.com",
            "img-src 'self' data: https:",
            "connect-src 'self'",
            "frame-ancestors 'none'",
            "form-action 'self'",
            "base-uri 'self'",
            "upgrade-insecure-requests"
        });

        // X-Content-Type-Options - Prevent MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // X-Frame-Options - Prevent clickjacking (also covered by CSP frame-ancestors)
        headers["X-Frame-Options"] = "DENY";

        // X-XSS-Protection - Enable XSS filter in older browsers
        headers["X-XSS-Protection"] = "1; mode=block";

        // Referrer-Policy - Control referrer information
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions-Policy - Restrict browser features
        headers["Permissions-Policy"] = string.Join(", ", new[]
        {
            "accelerometer=()",
            "ambient-light-sensor=()",
            "autoplay=()",
            "battery=()",
            "camera=()",
            "display-capture=()",
            "document-domain=()",
            "encrypted-media=()",
            "execution-while-not-rendered=()",
            "execution-while-out-of-viewport=()",
            "fullscreen=(self)",
            "geolocation=()",
            "gyroscope=()",
            "magnetometer=()",
            "microphone=()",
            "midi=()",
            "navigation-override=()",
            "payment=()",
            "picture-in-picture=()",
            "publickey-credentials-get=()",
            "screen-wake-lock=()",
            "sync-xhr=()",
            "usb=()",
            "web-share=()",
            "xr-spatial-tracking=()"
        });

        // Cache-Control for security-sensitive pages
        headers["Cache-Control"] = "no-store, no-cache, must-revalidate, proxy-revalidate";
        headers["Pragma"] = "no-cache";

        // Remove server header (information disclosure)
        headers.Remove("Server");
        headers.Remove("X-Powered-By");
        headers.Remove("X-AspNet-Version");
        headers.Remove("X-AspNetMvc-Version");
    }
}

/// <summary>
/// Extension methods for SecurityHeadersMiddleware.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}

