using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.PermissionDelegation.Commands;

[Authorize(Permissions = Permissions.Users.ViewAll)]
public record RevokeDelegationCommand(Guid DelegationId) : IRequest<Result>;

public class RevokeDelegationCommandHandler : IRequestHandler<RevokeDelegationCommand, Result>
{
    private readonly IPermissionDelegationService _delegationService;
    private readonly ILogger<RevokeDelegationCommandHandler> _logger;

    public RevokeDelegationCommandHandler(
        IPermissionDelegationService delegationService,
        ILogger<RevokeDelegationCommandHandler> logger)
    {
        _delegationService = delegationService;
        _logger = logger;
    }

    public async Task<Result> Handle(RevokeDelegationCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _delegationService.RevokeDelegationAsync(
                request.DelegationId,
                cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Delegation {DelegationId} revoked", request.DelegationId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking delegation {DelegationId}", request.DelegationId);
            return Result.Failure(Error.Custom("Delegation.RevokeFailed", "Failed to revoke delegation."));
        }
    }
}

