using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Receivables.DTOs;
using Cleansia.Core.AppServices.Features.Receivables.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Receivables;

/// <summary>
/// What the caller owes any operating company and has not settled — the debt that holds back their cash
/// bookings (owner ruling 2026-09-28, decision 18), each payable through its pay link.
/// </summary>
public class GetMyReceivables
{
    public record Query : IQuery<IReadOnlyList<MyReceivableDto>>;

    public class Handler(
        IReceivableRepository receivableRepository,
        IUserSessionProvider userSessionProvider) : IQueryHandler<Query, IReadOnlyList<MyReceivableDto>>
    {
        public async Task<BusinessResult<IReadOnlyList<MyReceivableDto>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var receivables = await receivableRepository.GetOpenForUserAsync(userSessionProvider.GetUserId()!, cancellationToken);
            return BusinessResult.Success<IReadOnlyList<MyReceivableDto>>(receivables.Select(r => r.MapToMyDto()).ToList());
        }
    }
}
