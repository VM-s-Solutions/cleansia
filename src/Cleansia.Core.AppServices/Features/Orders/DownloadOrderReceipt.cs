using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class DownloadOrderReceipt
{
    public record Query(string OrderId) : IQuery<Response>;

    public record Response(
        byte[] PdfBytes,
        string FileName,
        string ContentType);

    public class Validator : AbstractValidator<Query>
    {
        private readonly IOrderAccessService _orderAccessService;

        public Validator(IOrderAccessService orderAccessService)
        {
            _orderAccessService = orderAccessService;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderAccessService.OrderExistsForCallerAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                .MustAsync(OrderHasReceiptAsync)
                .WithMessage(BusinessErrorMessage.ReceiptNotFound);
        }

        // The receipt is reached through the order the caller may read, never on its own: it carries
        // the order's operator, which for a booking made across the border is not the customer's company.
        private Task<bool> OrderHasReceiptAsync(string orderId, CancellationToken cancellationToken)
        {
            return _orderAccessService
                .OrdersForCaller()
                .AnyAsync(o => o.Id == orderId && o.Receipt != null, cancellationToken);
        }
    }

    public class Handler(
        IOrderAccessService orderAccessService,
        IReceiptService receiptService) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var order = await orderAccessService
                .OrdersForCaller()
                .Include(o => o.Receipt)
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == query.OrderId, cancellationToken);

            if (order == null || !await orderAccessService.CanAccessOrderAsync(order, cancellationToken))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(query.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            var pdfBytes = await receiptService.DownloadReceiptPdfAsync(order.Receipt!, cancellationToken);

            return BusinessResult.Success(new Response(
                PdfBytes: pdfBytes,
                FileName: order.Receipt!.FileName,
                ContentType: "application/pdf"
            ));
        }
    }
}
