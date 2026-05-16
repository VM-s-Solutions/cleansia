using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Orders;

public class GetOrderDetails
{
    public class Validator : AbstractValidator<Query>
    {
        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);
        }
    }

    public record Query(string OrderId) : IQuery<OrderItem>;

    public class Handler(
        IOrderRepository orderRepository,
        IOrderAccessService orderAccessService) : IQueryHandler<Query, OrderItem>
    {
        public async Task<BusinessResult<OrderItem>> Handle(Query query, CancellationToken cancellationToken)
        {
            var order = await orderRepository.GetByIdAsync(query.OrderId, cancellationToken);
            if (order == null || !await orderAccessService.CanBrowseOrderAsync(order, cancellationToken))
            {
                return BusinessResult.Failure<OrderItem>(new Error(
                    nameof(query.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            return BusinessResult.Success(order.MapToDetail());
        }
    }
}
