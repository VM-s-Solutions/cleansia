using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Credit;

/// <summary>
/// The caller's own credit balance.
///
/// <para>Reads only. It never opens an account: a customer who has never been credited has a zero
/// balance, and writing a row to say so on every page load would be a write on a read path.</para>
/// </summary>
public class GetMyCredit
{
    public record Query : IQuery<Response>;

    /// <param name="Balance">What is spendable, in <paramref name="CurrencyCode"/>.</param>
    /// <param name="CurrencyCode">
    /// The currency the balance is held in — the account's own, which is not necessarily the one the
    /// customer is currently browsing in. Credit only applies to an order priced in the same currency.
    /// </param>
    /// <param name="MaxShareOfOrder">
    /// The share of an order credit may settle, as a fraction. The client needs the NUMBER, not a
    /// hardcoded sentence: owner ruling 2026-09-05 requires the customer be told plainly that credit
    /// cannot cover a whole booking, and a percentage baked into five locales drifts from the rule the
    /// backend enforces the first time the rule moves. -> BookingPolicy.MaxCreditShareOfOrder
    /// </param>
    /// <param name="AppliesAutomatically">
    /// Always true today, and present because the customer has to be told. Owner ruling 2026-09-05:
    /// credit is applied by the platform, to the next eligible order — there is no "spend it now"
    /// control, and a balance that silently does nothing until some unexplained future booking is
    /// worse than one the customer knows is queued.
    /// </param>
    /// <param name="ExpiresOn">
    /// When the balance expires if the customer does nothing. Null when there is nothing to expire.
    ///
    /// <para>Shown, not hidden. Owner ruling 2026-09-05: credit expires rather than being paid out,
    /// and an expiry the customer only discovers after the fact is the version that generates a
    /// complaint nobody can answer. Every movement pushes it out, so a customer who books once a year
    /// never loses anything. → CreditAccount.ExpiryMonths</para>
    /// </param>
    public record Response(
        decimal Balance,
        string CurrencyCode,
        decimal MaxShareOfOrder,
        bool AppliesAutomatically,
        DateTimeOffset? ExpiresOn);

    public class Handler(
        ICreditAccountRepository creditAccountRepository,
        ICurrencyRepository currencyRepository,
        IUserSessionProvider userSessionProvider) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(
            Query request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var spendable = await creditAccountRepository.GetSpendableAsync(userId, cancellationToken);

            // No account is the ordinary case, not an error: nothing has ever gone wrong for this
            // customer. It answers zero, in the platform's default currency, so the client renders one
            // shape either way.
            var currency = spendable == null
                ? await currencyRepository.GetDefaultAsync(cancellationToken)
                : await currencyRepository.GetByIdAsync(spendable.CurrencyId, cancellationToken);

            return BusinessResult.Success(new Response(
                Balance: spendable?.Balance ?? 0m,
                CurrencyCode: currency?.Code ?? string.Empty,
                MaxShareOfOrder: BookingPolicy.MaxCreditShareOfOrder,
                AppliesAutomatically: true,
                ExpiresOn: spendable?.ExpiresOn));
        }
    }
}
