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
        IReadOnlyList<ReviewTag>? Tags = null
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

            // Tags are a chip row, not a questionnaire. Every rule below refuses rather than silently
            // dropping: a client that sends an out-of-band tag has a bug, and quietly discarding it
            // would leave the stored review saying something the customer did not choose.
            When(x => x.Tags is { Count: > 0 }, () =>
            {
                RuleFor(x => x.Tags!)
                    .Cascade(CascadeMode.Stop)
                    .Must(tags => tags.Count <= ReviewTagPolarity.MaxTagsPerReview)
                    .WithMessage(BusinessErrorMessage.ReviewTooManyTags)
                    .Must(tags => tags.Distinct().Count() == tags.Count)
                    .WithMessage(BusinessErrorMessage.ReviewDuplicateTag)
                    .Must(tags => tags.All(Enum.IsDefined))
                    .WithMessage(BusinessErrorMessage.ReviewUnknownTag);

                // The polarity gate reads Rating, so it has to sit on the whole command rather than on
                // the Tags property. It is deliberately NOT cascaded off the rating rule above: an
                // invalid rating already failed there, and this arm only ever runs on a valid one.
                RuleFor(x => x)
                    .Must(command => command.Tags!.All(tag =>
                        ReviewTagPolarity.MatchesRating(tag, command.Rating)))
                    .WithMessage(BusinessErrorMessage.ReviewTagRatingMismatch)
                    .When(x => x.Rating is >= 1 and <= 5);
            });
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
                existingReview.Update(command.Rating, command.Comment, command.Tags);
                await RecalculateEmployeeRatings(order, cancellationToken);
                return BusinessResult.Success(existingReview.MapToDto());
            }

            var review = OrderReview.Create(
                command.OrderId, userId, command.Rating, command.Comment, command.Tags);
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
