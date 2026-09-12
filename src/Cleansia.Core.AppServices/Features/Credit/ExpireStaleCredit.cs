using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Credit;

/// <summary>
/// Take balances that have expired.
///
/// <para>Owner ruling 2026-09-05: credit expires rather than being paid out. Cleansia does not do
/// Stripe payouts, so the alternative to expiry is a debt that sits on the books forever and a
/// customer who cannot be erased because of it. Twelve months from the last movement, and every
/// movement resets the clock — a customer who books once a year never loses anything.
/// → CreditAccount.ExpiryMonths</para>
///
/// <para>The customer is told the date on their profile and in the booking summary, so this can never
/// be the first they hear of it.</para>
/// </summary>
public class ExpireStaleCredit
{
    /// <param name="BatchSize">
    /// How many accounts one pass may take. A sweep that has never run has an unbounded backlog, and
    /// loading all of it into one transaction is how a nightly job becomes an outage. The next run
    /// picks up where this one stopped — there is no state to carry, because an account either is past
    /// its date or is not.
    /// </param>
    public record Command(int BatchSize = 500) : ICommand<Response>;

    /// <param name="TotalExpiredByCurrencyId">
    /// What was taken, PER CURRENCY, keyed by <c>CreditAccount.CurrencyId</c>. Balances are
    /// denominated and one sweep crosses every account, so a single total would add crowns to euros.
    /// Read by nothing but the timer handler's log line.
    /// </param>
    public record Response(int AccountsExpired, IReadOnlyDictionary<string, decimal> TotalExpiredByCurrencyId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            // Bare, like CleanupStalePendingOrders' own bound: this command has no user-facing
            // surface — it is invoked by a scheduler — so a translated key would be read by nobody.
            RuleFor(x => x.BatchSize).InclusiveBetween(1, 5000);
        }
    }

    public class Handler(
        ICreditAccountRepository creditAccountRepository,
        ITenantProvider tenantProvider,
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        /// <summary>No JWT on a sweep. Matches CleanupStalePendingOrders and LoyaltyService.</summary>
        private const string SystemActor = "system";

        public async Task<BusinessResult<Response>> Handle(
            Command command, CancellationToken cancellationToken)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var expired = await creditAccountRepository.GetExpiredAsync(
                nowUtc, command.BatchSize, cancellationToken);

            var accountsExpired = 0;
            var totalExpired = new Dictionary<string, decimal>();

            // System job — no JWT context. The read above ignores the tenant filter, so the rows come
            // from every tenant at once; group them and set the override per group, because a
            // CreditTransaction is stamped from the AMBIENT tenant at commit time. One deferred commit
            // would stamp every group with whichever tenant was processed last.
            // -> CleanupStalePendingOrders is the reference shape.
            foreach (var tenantGroup in expired.GroupBy(a => a.TenantId ?? string.Empty))
            {
                tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(tenantGroup.Key))
                {
                    tenantProvider.SetTenantOverride(tenantGroup.Key);
                }

                foreach (var account in tenantGroup)
                {
                    var taken = account.Drain(SystemActor, nowUtc);
                    if (taken <= 0m)
                    {
                        continue;
                    }

                    // The ledger row keeps Balance == SUM(Transactions.Amount) true through the
                    // expiry, which is the one invariant this design has. Negative, because the money
                    // left. Keyed on the account and the date so a re-run of the same night's sweep
                    // cannot take it twice — the unique index on IdempotencyKey is the backstop, and
                    // the balance is already zero by then anyway.
                    account.RecordExpiry(taken, $"credit-expired:{account.Id}:{nowUtc:yyyy-MM-dd}", SystemActor);

                    accountsExpired++;
                    totalExpired[account.CurrencyId] = totalExpired.GetValueOrDefault(account.CurrencyId) + taken;
                }

                await unitOfWork.CommitAsync(cancellationToken);
            }

            tenantProvider.ClearTenantOverride();

            if (accountsExpired > 0)
            {
                logger.LogInformation(
                    "ExpireStaleCredit took {TotalByCurrency} across {Count} account(s) whose balances had "
                    + "passed their expiry date.",
                    string.Join(", ", totalExpired.Select(kv => $"{kv.Value} ({kv.Key})")),
                    accountsExpired);
            }

            return BusinessResult.Success(new Response(accountsExpired, totalExpired));
        }
    }
}
