using Cleansia.Core.AppServices.Features.Credit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Nightly, take credit balances that have passed their expiry date.
///
/// <para>Owner ruling 2026-09-05: credit expires rather than being paid out — Cleansia does not do
/// Stripe payouts, and the alternative to expiry is a debt that sits on the books forever and a
/// customer who cannot be erased because of it. Twelve months from the customer's LAST movement, so a
/// customer who books once a year never loses anything.</para>
///
/// <para>Nightly rather than hourly: an expiry is a date, not a moment, and nobody is harmed by their
/// balance lasting until 03:00 the next morning. It shares the small hours with the other daily
/// janitors rather than adding load to a busy hour.</para>
/// </summary>
public class ExpireStaleCreditHandler(
    IMediator mediator,
    ILogger<ExpireStaleCreditHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("ExpireStaleCredit timer triggered at {Time}", DateTime.UtcNow);

        var result = await mediator.Send(new ExpireStaleCredit.Command(), ct);
        if (result.IsSuccess && result.Value != null)
        {
            logger.LogInformation(
                "ExpireStaleCredit completed; took {TotalByCurrency} across {Count} account(s)",
                string.Join(", ", result.Value.TotalExpiredByCurrencyId.Select(kv => $"{kv.Value} ({kv.Key})")),
                result.Value.AccountsExpired);
        }
        else
        {
            logger.LogError(
                "ExpireStaleCredit failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
