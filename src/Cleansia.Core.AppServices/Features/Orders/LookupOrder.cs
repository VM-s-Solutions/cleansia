using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.Packages.DTOs;
using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Guest order lookup. Three factors, all required: the display order number,
/// the e-mail the confirmation went to, and the order's own confirmation code.
///
/// The code is the point. Without it this endpoint answered to an order number
/// and an e-mail — a display number is sequential and an e-mail is not a
/// secret, so anyone who could guess both could read a stranger's booking. The
/// code is six characters of a GUID, generated per order and delivered only in
/// the confirmation e-mail, and it is checked in the SAME query as the other
/// two so a wrong code and a non-existent order are indistinguishable: the
/// endpoint never confirms that an order exists.
///
/// `LookupOrderBatch` deliberately does NOT take one. It is keyed on the
/// order's ULID rather than its display number — 26 unguessable characters the
/// browser only has because it placed the order — so the id is itself the
/// secret, and requiring a code there would break the remembered-orders list
/// for every guest without giving anything up.
/// </summary>
public class LookupOrder
{
    public record Query(string DisplayOrderNumber, string Email, string ConfirmationCode)
        : IQuery<Response>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.DisplayOrderNumber).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.Email).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.ConfirmationCode).NotEmpty().WithMessage(BusinessErrorMessage.Required);
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
        string ConfirmationCode,
        CurrencyDetailDto Currency,
        IEnumerable<ServiceDetails> SelectedServices,
        IEnumerable<PackageDetails> SelectedPackages,
        IEnumerable<OrderStatusTrackDto> StatusHistory,
        DateTimeOffset CreatedOn);

    public class Handler(IOrderRepository orderRepository) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var order = await orderRepository.GetQueryable()
                .Include(o => o.Currency)
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.SelectedServices)
                    .ThenInclude(s => s.Service)
                .Include(o => o.SelectedPackages)
                    .ThenInclude(op => op.Package)
                        .ThenInclude(p => p.IncludedServices)
                            .ThenInclude(s => s.Service)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o =>
                    o.DisplayOrderNumber == request.DisplayOrderNumber &&
                    o.CustomerEmail.ToLower() == request.Email.ToLower() &&
                    // Case-insensitive: the code is generated uppercase but is
                    // read off an e-mail and typed by hand. Part of the SAME
                    // predicate as the other two so a wrong code returns the
                    // same "not found" as a wrong number — the endpoint must
                    // not become an oracle for which orders exist.
                    o.ConfirmationCode.ToUpper() == request.ConfirmationCode.ToUpper(),
                    cancellationToken);

            if (order == null)
                return BusinessResult.Failure<Response>(new Error(nameof(request.DisplayOrderNumber), BusinessErrorMessage.OrderNotFound));

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
                detail.ConfirmationCode,
                detail.Currency,
                detail.SelectedServices,
                detail.SelectedPackages,
                detail.StatusHistory,
                detail.CreatedOn));
        }
    }
}
