using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class SubmitOrderReview
{
    public record Command(
        string OrderId,
        int Rating,
        string? Comment,
        string UserId = ""
    ) : ICommand<OrderReviewDto>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);

            RuleFor(x => x.Rating)
                .InclusiveBetween(1, 5)
                .WithMessage(BusinessErrorMessage.ReviewRatingInvalid);

            RuleFor(x => x.Comment)
                .MaximumLength(1000)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(IOrderRepository orderRepository) : ICommandHandler<Command, OrderReviewDto>
    {
        public async Task<BusinessResult<OrderReviewDto>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.Reviews)
                .Include(o => o.OrderStatusHistory)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<OrderReviewDto>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            // Verify the order belongs to the user
            if (order.UserId != command.UserId)
            {
                return BusinessResult.Failure<OrderReviewDto>(
                    new Error(nameof(command.UserId), BusinessErrorMessage.OrderNotOwnedByUser));
            }

            // Verify order is completed
            var currentStatus = order.GetCurrentOrderStatus();
            if (currentStatus != OrderStatus.Completed)
            {
                return BusinessResult.Failure<OrderReviewDto>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotCompleted));
            }

            // Check if review already exists — update it
            var existingReview = order.Reviews.FirstOrDefault(r => r.UserId == command.UserId);
            if (existingReview != null)
            {
                existingReview.Update(command.Rating, command.Comment);
                return BusinessResult.Success(existingReview.MapToDto());
            }

            // Create new review
            var review = OrderReview.Create(command.OrderId, command.UserId, command.Rating, command.Comment);
            order.AddReview(review);

            return BusinessResult.Success(review.MapToDto());
        }
    }
}
