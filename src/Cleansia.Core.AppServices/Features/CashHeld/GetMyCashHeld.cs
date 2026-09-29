using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.CashHeld.DTOs;
using Cleansia.Core.AppServices.Features.CashHeld.Mappers;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.CashHeld;

/// <summary>
/// The company's cash the calling cleaner holds, per currency (owner ruling 2026-09-28, decision 23): what
/// they collected at the door and have not yet handed back, had written off or had set off against an invoice.
/// Each currency states the company's float cap and whether it hides cash jobs from them (decision 25). A
/// caller who is not a cleaner holds none.
/// </summary>
public class GetMyCashHeld
{
    public record Query : IQuery<IReadOnlyList<CashHeldDto>>;

    public class Handler(
        ICashLedgerRepository cashLedgerRepository,
        IOrderAccessService orderAccessService,
        IAppConfigurationProvider configurationProvider)
        : IQueryHandler<Query, IReadOnlyList<CashHeldDto>>
    {
        public async Task<BusinessResult<IReadOnlyList<CashHeldDto>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId))
            {
                return BusinessResult.Success<IReadOnlyList<CashHeldDto>>([]);
            }

            var balances = await cashLedgerRepository.GetBalancesAsync(employeeId, cancellationToken);
            var floatCap = await configurationProvider.GetAsync(TenantSettingCatalog.CashFloatCap, cancellationToken);
            return BusinessResult.Success<IReadOnlyList<CashHeldDto>>(balances.Select(b => b.MapToDto(floatCap)).ToList());
        }
    }
}
