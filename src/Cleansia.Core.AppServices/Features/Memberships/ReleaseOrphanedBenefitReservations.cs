using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Memberships;

/// <summary>
/// ADR-0035 AM-7 — reclaim benefit slots that were reserved and then stranded.
///
/// <para>The reservation auto-commits before the order exists, so a failure anywhere between the claim
/// and the order's unit-of-work commit leaves a live row with a NULL <c>OrderId</c>: a credit spent on a
/// booking that never happened. Without this sweep a member who abandons two payment sheets in a month
/// silently loses the month's entire quota, and there is no row a support agent can find by order id.</para>
///
/// <para><c>CleanupStalePendingOrders</c> structurally cannot do this and never could — it queries
/// <c>Orders</c>, and an orphan is by definition a row whose order was never inserted. It rides the same
/// timer as a second command rather than getting a schedule of its own.</para>
///
/// <para>A sweep, not a compensation: the input is a durable predicate over persisted state, so it does
/// not depend on the failing request surviving long enough to clean up after itself.</para>
/// </summary>
public class ReleaseOrphanedBenefitReservations
{
    public record Command(int OlderThanHours = 1) : ICommand<Response>;

    public record Response(int ReleasedCount);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OlderThanHours).InclusiveBetween(1, 168);
        }
    }

    public class Handler(
        IMembershipBenefitUsageRepository benefitUsageRepository,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var cutoffUtc = DateTime.UtcNow.AddHours(-command.OlderThanHours);
            var released = await benefitUsageRepository
                .ReleaseOrphanedReservationsAsync(cutoffUtc, cancellationToken);

            if (released > 0)
            {
                logger.LogInformation(
                    "Released {Count} orphaned membership benefit reservations older than {Hours}h",
                    released, command.OlderThanHours);
            }

            return BusinessResult.Success(new Response(released));
        }
    }
}
