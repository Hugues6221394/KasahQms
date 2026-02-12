using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using MediatR;
using DelegationDto = KasahQMS.Application.Common.Interfaces.Services.DelegationDto;

namespace KasahQMS.Application.Features.PermissionDelegation.Queries;

[Authorize(Permissions = Permissions.Users.ViewAll)]
public record GetMyDelegationsQuery : IRequest<Result<IEnumerable<DelegationDto>>>;

public class GetMyDelegationsQueryHandler : IRequestHandler<GetMyDelegationsQuery, Result<IEnumerable<DelegationDto>>>
{
    private readonly IPermissionDelegationService _delegationService;

    public GetMyDelegationsQueryHandler(IPermissionDelegationService delegationService)
    {
        _delegationService = delegationService;
    }

    public async Task<Result<IEnumerable<DelegationDto>>> Handle(
        GetMyDelegationsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var delegations = await _delegationService.GetMyDelegationsAsync(cancellationToken);
            return Result.Success(delegations);
        }
        catch (Exception)
        {
            return Result.Failure<IEnumerable<DelegationDto>>(
                Error.Custom("Delegation.QueryFailed", "Failed to retrieve delegations."));
        }
    }
}

