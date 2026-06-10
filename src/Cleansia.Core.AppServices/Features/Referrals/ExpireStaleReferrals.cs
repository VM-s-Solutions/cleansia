using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Referrals;

/// <summary>
/// Background sweep that flips Accepted referrals past the
/// <see cref="ReferralPolicy.QualifyingWindowDays"/> qualifying window to Expired (no points granted).
/// Dispatched from the recurring timer Function so the transition commits through the UoW command
/// pipeline. Idempotent: the cutoff filter <see cref="IReferralRepository.GetExpirableAsync"/> only
/// returns rows still in Accepted state, so a re-run never re-touches an already-terminal referral
/// (S7).
/// </summary>
public class ExpireStaleReferrals
{
    public record Command : ICommand<Response>;

    public record Response(int ExpiredCount);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
        }
    }

    public class Handler(
        IReferralRepository referralRepository,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        private const string SystemActor = "system";

        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-ReferralPolicy.QualifyingWindowDays);
            var expirable = await referralRepository.GetExpirableAsync(cutoff, cancellationToken);

            foreach (var referral in expirable)
            {
                referral.MarkExpired(SystemActor);
            }

            if (expirable.Count > 0)
            {
                logger.LogInformation(
                    "Expired {Count} stale referrals past the {Days}-day qualifying window.",
                    expirable.Count, ReferralPolicy.QualifyingWindowDays);
            }

            return BusinessResult.Success(new Response(expirable.Count));
        }
    }
}
