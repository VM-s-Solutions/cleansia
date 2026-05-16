using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Referrals.Admin.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Referrals.Admin;

/// <summary>
/// "Show me everything this user is involved in" — split into two lists so
/// the admin UI can render two stacked tables: AsReferrer (people they
/// invited) and AsReferred (the row that brought them in, if any).
/// </summary>
public class GetReferralsByUser
{
    public record Query(string UserId) : IQuery<Response>;

    public record Response(
        IReadOnlyList<AdminReferralListItem> AsReferrer,
        IReadOnlyList<AdminReferralListItem> AsReferred);

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.UserNotFound);
        }
    }

    public class Handler(IReferralRepository referralRepository) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var rows = await referralRepository.GetByUserAsync(request.UserId, cancellationToken);

            var asReferrer = rows
                .Where(r => r.ReferrerUserId == request.UserId)
                .Select(r => Map(r))
                .ToList();

            var asReferred = rows
                .Where(r => r.ReferredUserId == request.UserId)
                .Select(r => Map(r))
                .ToList();

            return BusinessResult.Success(new Response(asReferrer, asReferred));
        }

        private static AdminReferralListItem Map(Cleansia.Core.Domain.Loyalty.Referral r) =>
            new(
                Id: r.Id,
                ReferrerUserId: r.ReferrerUserId,
                ReferrerEmail: r.Referrer?.Email,
                ReferredUserId: r.ReferredUserId,
                ReferredEmail: r.Referred?.Email,
                Status: r.Status,
                AcceptedOn: r.AcceptedOn,
                FirstQualifyingOrderOn: r.FirstQualifyingOrderOn,
                PointsAwardedToReferrer: r.PointsAwardedToReferrer,
                PointsAwardedToReferred: r.PointsAwardedToReferred);
    }
}
