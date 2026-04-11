using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.FiscalFailures.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;

namespace Cleansia.Core.AppServices.Features.FiscalFailures;

public class GetFiscalFailures
{
    private const int MaxResults = 200;

    public record Query : IQuery<List<FiscalFailureDto>>;

    internal class Handler(IOrderReceiptRepository receiptRepository)
        : IRequestHandler<Query, BusinessResult<List<FiscalFailureDto>>>
    {
        public async Task<BusinessResult<List<FiscalFailureDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var receipts = await receiptRepository.GetRecentFiscalFailuresAsync(MaxResults, cancellationToken);

            var items = receipts
                .Select(r => new FiscalFailureDto(
                    ReceiptId: r.Id,
                    ReceiptNumber: r.ReceiptNumber,
                    OrderId: r.OrderId,
                    OrderNumber: r.Order?.DisplayOrderNumber,
                    IssuedAt: r.IssuedAt,
                    FiscalProviderKey: r.FiscalProviderKey,
                    ErrorKind: r.FiscalErrorKind,
                    ErrorMessage: r.FiscalError,
                    RetryCount: r.FiscalRetryCount,
                    LastRetryAt: r.FiscalLastRetryAt,
                    NextRetryAt: r.FiscalNextRetryAt,
                    Acknowledged: r.FiscalAcknowledged))
                .ToList();

            return BusinessResult.Success(items);
        }
    }
}
