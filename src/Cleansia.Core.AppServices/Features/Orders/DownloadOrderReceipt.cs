using Cleansia.Core.AppServices.Abstractions;
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
        private readonly IOrderReceiptRepository _receiptRepository;

        public Validator(
            IOrderRepository orderRepository,
            IOrderReceiptRepository receiptRepository)
        {
            _receiptRepository = receiptRepository;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                .MustAsync(OrderHasReceiptAsync)
                .WithMessage(BusinessErrorMessage.ReceiptNotFound);
        }

        private async Task<bool> OrderHasReceiptAsync(string orderId, CancellationToken cancellationToken)
        {
            var receipt = await _receiptRepository
                .GetQueryable()
                .FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);

            return receipt != null;
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IReceiptService receiptService) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.Receipt)
                .FirstOrDefaultAsync(o => o.Id == query.OrderId, cancellationToken);

            var pdfBytes = await receiptService.DownloadReceiptPdfAsync(order!.Receipt!, cancellationToken);

            return BusinessResult.Success(new Response(
                PdfBytes: pdfBytes,
                FileName: order.Receipt!.FileName,
                ContentType: "application/pdf"
            ));
        }
    }
}
