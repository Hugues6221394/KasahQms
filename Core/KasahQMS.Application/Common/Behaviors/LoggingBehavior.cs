using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Common.Behaviors;

/// <summary>
/// Logging behavior for MediatR pipeline.
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        
        _logger.LogDebug("Handling {RequestName}", requestName);

        var response = await next();

        _logger.LogDebug("Handled {RequestName}", requestName);

        return response;
    }
}
