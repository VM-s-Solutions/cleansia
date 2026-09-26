using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.Packages.DTOs;
using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Guest order lookup. One factor: the per-order access token the confirmation e-mail carries.
///
/// <para>It replaced a (display order number, e-mail, confirmation code) triple. None of the three was
/// a secret the platform could keep: the display number is sequential, the e-mail is not private, and
/// the confirmation code was served on the order detail to every cleaner assigned to the job — so a
/// cleaner held the whole key to their own customer's booking, including the cancellation that charges
/// the customer the 25 % / 50 % tier. The token is 256 bits, stored only as a SHA-256 hash, and reaches
/// nobody but the person who received the e-mail.</para>
/// </summary>
public class LookupOrder
{
    public record Query(string AccessToken) : IQuery<Response>, IGuestOrderScopedRequest
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

    public record Response(
        string Id,
        string DisplayOrderNumber,
        string CustomerName,
        DateTime CleaningDateTime,
        Code PaymentType,
        Code PaymentStatus,
        decimal TotalPrice,
        int EstimatedTime,
        Code OrderStatus,
        /// <summary>
        /// The short human reference printed on the booking. A REFERENCE, not a credential: nothing
        /// authenticates on it any more, and it is served on the guest's own order only.
        /// </summary>
        string ConfirmationCode,
        CurrencyDetailDto Currency,
        IEnumerable<ServiceDetails> SelectedServices,
        IEnumerable<PackageDetails> SelectedPackages,
        IEnumerable<OrderStatusTrackDto> StatusHistory,
        DateTimeOffset CreatedOn);

    public class Handler(GuestOrderAccess guestOrderAccess) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var order = await guestOrderAccess.OrdersForKey(request)
                .Include(o => o.Currency)
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.SelectedServices)
                    .ThenInclude(s => s.Service)
                .Include(o => o.SelectedPackages)
                    .ThenInclude(op => op.Package)
                        .ThenInclude(p => p.IncludedServices)
                            .ThenInclude(s => s.Service)
                .AsSplitQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (order == null)
                return BusinessResult.Failure<Response>(new Error(nameof(request.AccessToken), BusinessErrorMessage.OrderNotFound));

            var detail = order.MapToDetail();

            return BusinessResult.Success(new Response(
                detail.Id,
                detail.DisplayOrderNumber,
                detail.CustomerName,
                detail.CleaningDateTime,
                detail.PaymentType,
                detail.PaymentStatus,
                detail.TotalPrice,
                detail.EstimatedTime,
                detail.OrderStatus,
                order.ConfirmationCode,
                detail.Currency,
                detail.SelectedServices,
                detail.SelectedPackages,
                detail.StatusHistory,
                detail.CreatedOn));
        }
    }
}
