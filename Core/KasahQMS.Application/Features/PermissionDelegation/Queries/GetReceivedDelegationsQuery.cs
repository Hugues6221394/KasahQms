using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using MediatR;
using DelegationDto = KasahQMS.Application.Common.Interfaces.Services.DelegationDto;

namespace KasahQMS.Application.Features.PermissionDelegation.Queries;

[Authorize]
public record GetReceivedDelegationsQuery : IRequest<Result<IEnumerable<DelegationDto>>>;

public class GetReceivedDelegationsQueryHandler : IRequestHandler<GetReceivedDelegationsQuery, Result<IEnumerable<DelegationDto>>>
{
    private readonly IPermissionDelegationService _delegationService;

    public GetReceivedDelegationsQueryHandler(IPermissionDelegationService delegationService)
    {
        _delegationService = delegationService;
    }

    public async Task<Result<IEnumerable<DelegationDto>>> Handle(
        GetReceivedDelegationsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var delegations = await _delegationService.GetReceivedDelegationsAsync(cancellationToken);
            return Result.Success(delegations);
        }
        catch (Exception)
        {
            return Result.Failure<IEnumerable<DelegationDto>>(
                Error.Custom("Delegation.QueryFailed", "Failed to retrieve received delegations."));
        }
    }
}

