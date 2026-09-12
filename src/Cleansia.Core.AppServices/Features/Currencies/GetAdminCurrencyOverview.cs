using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Currencies;

/// <summary>
/// The currency list for the ADMIN, which is the same rows plus one boolean: whether the platform
/// operates in each.
///
/// <para>It exists as its own query rather than as a flag on <see cref="CurrencyListItem"/> because
/// that DTO is nested inside <c>OrderListItem</c> and reaches every host — both committed mobile
/// specs, both Android apps and iOS. Putting a LIVE market switch on a historical order row is the
/// same defect <c>CurrencyMappers</c> already documents for <c>ExchangeRate</c>: the same named field
/// would disagree between the quote and the order list for the same order. An admin screen wanting a
/// boolean is not a reason to put one there.</para>
///
/// <para>Same route, same permission, same ordering as <see cref="GetCurrencyOverview"/> — only the
/// admin host's response type moves.</para>
/// </summary>
public class GetAdminCurrencyOverview
{
    public record Request : IRequest<IEnumerable<AdminCurrencyListItem>>;

    public class Handler(ICurrencyRepository currencyRepository)
        : IRequestHandler<Request, IEnumerable<AdminCurrencyListItem>>
    {
        public async Task<IEnumerable<AdminCurrencyListItem>> Handle(
            Request request, CancellationToken cancellationToken)
        {
            return await currencyRepository.GetAll()
                .OrderByDescending(c => c.IsDefault)
                .ThenBy(c => c.Name)
                .Select(currency => currency.MapToAdminListItem())
                .ToListAsync(cancellationToken);
        }
    }
}
