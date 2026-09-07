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
    /// <summary>
    /// A score for ONE item of the order. Same <c>(ServiceId, PackageId?)</c> identity as a dispute
    /// line and a refund line, so all three name an order's items the same way.
    /// </summary>
    public record ReviewLineScore(string ServiceId, string? PackageId, int Rating);

    public record Command(
        string OrderId,
        int Rating,
        string? Comment,
        IReadOnlyList<ReviewTag>? Tags = null,
        /// <summary>
        /// Optional per-item scores. The order-level <see cref="Rating"/> stays the headline and stays
        /// required — these add what one number cannot say, which is that the oven was excellent and
        /// the bathroom was skipped. Empty is ordinary and is what every review written before this
        /// existed carries.
        /// </summary>
        IReadOnlyList<ReviewLineScore>? Lines = null
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

            // SHAPE only — whether an item is on the order is checked in the handler, after the
            // ownership gate, against the graph it already loads. A membership rule here would answer
            // "is service X on order Y" for an order the caller does not own, and the handler's
            // deliberate not-found-not-forbidden answer exists to deny exactly that.
            When(x => x.Lines is { Count: > 0 }, () =>
            {
                RuleFor(x => x.Lines!)
                    .Cascade(CascadeMode.Stop)
                    .Must(lines => lines.All(l => l.Rating is >= 1 and <= 5))
                    .WithMessage(BusinessErrorMessage.ReviewRatingInvalid)
                    .Must(lines => lines
                        .Select(l => (l.ServiceId, l.PackageId))
                        .Distinct()
                        .Count() == lines.Count)
                    .WithMessage(BusinessErrorMessage.ReviewDuplicateTag);
            });

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
                // The items a per-item score may name. Loaded here rather than checked in the
                // validator, which would need a second load of the same graph for an order the caller
                // may not even own.
                .Include(o => o.SelectedServices)
                .Include(o => o.SelectedPackages)
                    .ThenInclude(op => op.Package)
                        .ThenInclude(p => p!.IncludedServices)
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

            var scores = command.Lines ?? [];
            if (scores.Count > 0)
            {
                var onOrder = OrderLineIdentities(order);
                if (scores.Any(line => !onOrder.Contains((line.ServiceId, line.PackageId))))
                {
                    return BusinessResult.Failure<OrderReviewDto>(new Error(
                        nameof(command.Lines), BusinessErrorMessage.ReviewLineNotOnOrder));
                }
            }

            var existingReview = order.Reviews.FirstOrDefault(r => r.UserId == userId);
            if (existingReview != null)
            {
                existingReview.Update(command.Rating, command.Comment, command.Tags);
                // Editing re-states the scores wholesale rather than merging, so what is stored is
                // always exactly what the customer last submitted — including "none", when they
                // removed them.
                existingReview.SetLines(
                    scores.Select(l => (l.ServiceId, l.PackageId, l.Rating)), userId);
                await RecalculateEmployeeRatings(order, cancellationToken);
                return BusinessResult.Success(existingReview.MapToDto());
            }

            var review = OrderReview.Create(
                command.OrderId, userId, command.Rating, command.Comment, command.Tags);
            review.SetLines(scores.Select(l => (l.ServiceId, l.PackageId, l.Rating)), userId);
            order.AddReview(review);

            await RecalculateEmployeeRatings(order, cancellationToken);

            return BusinessResult.Success(review.MapToDto());
        }

        /// <summary>
        /// Every item identity this order contains — standalone services, and the services inside each
        /// package. Mirrors the same helper in <c>CreateDispute</c>: a customer must not be able to
        /// score an item they did not buy.
        /// </summary>
        private static HashSet<(string ServiceId, string? PackageId)> OrderLineIdentities(Order order)
        {
            var identities = new HashSet<(string, string?)>();

            foreach (var service in order.SelectedServices)
            {
                identities.Add((service.ServiceId, null));
            }

            foreach (var orderPackage in order.SelectedPackages)
            {
                foreach (var included in orderPackage.Package?.IncludedServices ?? [])
                {
                    identities.Add((included.ServiceId, orderPackage.PackageId));
                }
            }

            return identities;
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
