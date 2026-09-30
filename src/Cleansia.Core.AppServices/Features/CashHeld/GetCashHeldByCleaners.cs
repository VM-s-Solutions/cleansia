using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.CashHeld.DTOs;
using Cleansia.Core.AppServices.Features.CashHeld.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.CashHeld;

/// <summary>
/// The administrator's view of the company's cash in its cleaners' hands (owner ruling 2026-09-28,
/// decision 23): every cleaner who holds any, per currency.
/// </summary>
public class GetCashHeldByCleaners
{
    public record Query : IQuery<IReadOnlyList<CleanerCashHeldDto>>;

    public class Handler(ICashLedgerRepository cashLedgerRepository)
        : IQueryHandler<Query, IReadOnlyList<CleanerCashHeldDto>>
    {
        public async Task<BusinessResult<IReadOnlyList<CleanerCashHeldDto>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var balances = await cashLedgerRepository.GetBalancesAsync(employeeId: null, cancellationToken);
            return BusinessResult.Success<IReadOnlyList<CleanerCashHeldDto>>(
                balances.Select(b => b.MapToCleanerDto()).ToList());
        }
    }
}
