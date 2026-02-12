using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.PermissionDelegation.Commands;

[Authorize(Permissions = Permissions.Users.ViewAll)]
public record DelegatePermissionCommand(
    Guid SubordinateId,
    string Permission,
    int? ExpiresAfterDays = null) : IRequest<Result>;

public class DelegatePermissionCommandHandler : IRequestHandler<DelegatePermissionCommand, Result>
{
    private readonly IPermissionDelegationService _delegationService;
    private readonly ILogger<DelegatePermissionCommandHandler> _logger;

    public DelegatePermissionCommandHandler(
        IPermissionDelegationService delegationService,
        ILogger<DelegatePermissionCommandHandler> logger)
    {
        _delegationService = delegationService;
        _logger = logger;
    }

    public async Task<Result> Handle(DelegatePermissionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _delegationService.DelegatePermissionAsync(
                request.SubordinateId,
                request.Permission,
                request.ExpiresAfterDays,
                cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Permission {Permission} delegated to user {SubordinateId}",
                    request.Permission,
                    request.SubordinateId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delegating permission {Permission} to {SubordinateId}", 
                request.Permission, request.SubordinateId);
            return Result.Failure(Error.Custom("Delegation.Failed", "Failed to delegate permission."));
        }
    }
}

