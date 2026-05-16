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
        string? Comment
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

    public class Handler(
        IOrderRepository orderRepository,
        IEmployeeRepository employeeRepository,
        IUserSessionProvider userSessionProvider
    ) : ICommandHandler<Command, OrderReviewDto>
    {
        public async Task<BusinessResult<OrderReviewDto>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.Reviews)
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<OrderReviewDto>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            var userId = userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId) || order.UserId != userId)
            {
                return BusinessResult.Failure<OrderReviewDto>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            var currentStatus = order.GetCurrentOrderStatus();
            if (currentStatus != OrderStatus.Completed)
            {
                return BusinessResult.Failure<OrderReviewDto>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotCompleted));
            }

            var existingReview = order.Reviews.FirstOrDefault(r => r.UserId == userId);
            if (existingReview != null)
            {
                existingReview.Update(command.Rating, command.Comment);
                await RecalculateEmployeeRatings(order, cancellationToken);
                return BusinessResult.Success(existingReview.MapToDto());
            }

            var review = OrderReview.Create(command.OrderId, userId, command.Rating, command.Comment);
            order.AddReview(review);

            await RecalculateEmployeeRatings(order, cancellationToken);

            return BusinessResult.Success(review.MapToDto());
        }

        private async Task RecalculateEmployeeRatings(Order order, CancellationToken cancellationToken)
        {
            foreach (var assignedEmployee in order.AssignedEmployees)
            {
                var employee = await employeeRepository.GetByIdAsync(assignedEmployee.EmployeeId, cancellationToken);
                if (employee == null) continue;

                var allReviews = await orderRepository
                    .GetQueryable()
                    .Where(o => o.AssignedEmployees.Any(ae => ae.EmployeeId == employee.Id))
                    .SelectMany(o => o.Reviews)
                    .ToListAsync(cancellationToken);

                if (allReviews.Count == 0) continue;

                var averageRating = (decimal)allReviews.Average(r => r.Rating);
                employee.UpdateRating(Math.Round(averageRating, 2), employee.ComplaintsCount);
            }
        }
    }
}
