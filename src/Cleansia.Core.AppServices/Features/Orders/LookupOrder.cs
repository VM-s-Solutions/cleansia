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
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class LookupOrder
{
    public record Query(string DisplayOrderNumber, string Email) : IQuery<Response>;

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
                    o.CustomerEmail.ToLower() == request.Email.ToLower(), cancellationToken);

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
