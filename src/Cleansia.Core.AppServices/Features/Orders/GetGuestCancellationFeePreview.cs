using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class GetGuestCancellationFeePreview
{
    public record Query(string AccessToken)
        : IQuery<GetCancellationFeePreview.Response>, IGuestOrderScopedRequest
    {
        string? IOperatorScopedRequest.CountryId => null;
    }

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.AccessToken).NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        GuestOrderAccess guestOrderAccess,
        ICancellationPolicyResolver cancellationPolicyResolver,
        IExpressWaiverConsumer expressWaiverConsumer,
        TimeProvider timeProvider)
        : IQueryHandler<Query, GetCancellationFeePreview.Response>
    {
        public async Task<BusinessResult<GetCancellationFeePreview.Response>> Handle(
            Query query, CancellationToken cancellationToken)
        {
            var order = await guestOrderAccess.OrdersForKey(query)
                .Include(o => o.AssignedEmployees)
                .Include(o => o.Currency)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (order is null)
            {
                return BusinessResult.Failure<GetCancellationFeePreview.Response>(
                    new Error(nameof(query.AccessToken), BusinessErrorMessage.OrderNotFound));
            }

            if (CancellationAssessor.BlockedReason(order) is { } blockedReason)
            {
                return BusinessResult.Failure<GetCancellationFeePreview.Response>(
                    new Error(nameof(query.AccessToken), blockedReason));
            }

            var policy = await cancellationPolicyResolver.ResolveForUserAsync(null, cancellationToken);
            var assessment = CancellationAssessor.Assess(order, policy, timeProvider.GetUtcNow().UtcDateTime);
            var forfeit = await expressWaiverConsumer.WouldForfeitOnCustomerCancelAsync(
                order.Id, assessment.HasBeenAccepted, cancellationToken);
            return BusinessResult.Success(new GetCancellationFeePreview.Response(
                OrderId: order.Id,
                Tier: assessment.Tier,
                FeeRate: assessment.FeeRate,
                FeeAmount: assessment.FeeAmount,
                RefundAmount: assessment.RefundAmount,
                TotalPrice: order.TotalPrice,
                CurrencyCode: order.Currency!.Code,
                ExpressWaiverForfeitedOnCancel: forfeit,
                OopsWindowMinutes: policy.OopsWindowMinutes));
        }
    }
}
