using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Referrals;

/// <summary>
/// Snapshot of the calling customer's referral panel: lifetime code,
/// how many friends they've invited (Accepted), how many qualified, and
/// the per-side reward amount the UI renders in the share text.
/// </summary>
public class GetMyReferral
{
    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    public record Query(string UserId = "") : IQuery<Response>;

    public record Response(
        string Code,
        int TimesUsed,
        int QualifiedCount,
        int AcceptedCount,
        int PointsPerReferral);

    public class Handler(
        IReferralService referralService,
        IReferralRepository referralRepository) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            // Lazy-create the code on first read so existing users don't need
            // a backfill — same pattern as GetMyLoyalty.
            var code = await referralService.EnsureCodeForUserAsync(request.UserId, cancellationToken);

            // For v1 a single-page fetch is fine — no realistic user invites
            // tens of thousands of friends. If/when that's wrong we'll add
            // dedicated count-by-status repo methods.
            var totalCount = await referralRepository.CountByReferrerAsync(request.UserId, cancellationToken);
            var referrals = await referralRepository.GetByReferrerAsync(
                request.UserId, 0, totalCount > 0 ? totalCount : 0, cancellationToken);

            var qualified = referrals.Count(r => r.Status == ReferralStatus.Qualified);
            var accepted = referrals.Count(r => r.Status == ReferralStatus.Accepted);

            var response = new Response(
                Code: code.Code,
                TimesUsed: code.TimesUsed,
                QualifiedCount: qualified,
                AcceptedCount: accepted,
                PointsPerReferral: ReferralPolicy.PointsPerSide);

            return BusinessResult.Success(response);
        }
    }
}
