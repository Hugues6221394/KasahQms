using KasahQMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior for automatic transaction management.
/// Wraps command handlers in database transactions.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Only wrap write operations (commands) in transactions
        // Queries (name ends with "Query") don't need transactions
        if (requestName.EndsWith("Query", StringComparison.OrdinalIgnoreCase))
        {
            return await next();
        }

        _logger.LogDebug("Processing command {RequestName}", requestName);

        try
        {
            var response = await next();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Command {RequestName} completed successfully", requestName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {RequestName} failed", requestName);
            throw;
        }
    }
}
