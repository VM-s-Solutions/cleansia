using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.LiveActivities;

/// <summary>
/// Registers (upserts) one ActivityKit token for the caller (ADR-0029 D3). <c>OrderId</c> null is the
/// per-install push-to-start token; non-null is a per-activity update token. The caller's own token is
/// enforced by the endpoint policy; <c>UserId</c> is taken from the session, never the body (S1).
/// </summary>
public static class RegisterLiveActivityToken
{
    public record Command(
        string DeviceId,
        string Token,
        string? OrderId) : ICommand<Response>;

    internal class Validator : AbstractValidator<Command>
    {
        private static readonly OrderStatus[] RegisterableStatuses =
        [
            OrderStatus.Confirmed,
            OrderStatus.OnTheWay,
            OrderStatus.InProgress,
        ];

        private readonly IOrderRepository _orderRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            IOrderRepository orderRepository,
            IUserSessionProvider userSessionProvider)
        {
            _orderRepository = orderRepository;
            _userSessionProvider = userSessionProvider;

            RuleFor(x => x.DeviceId).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.Token).NotEmpty().WithMessage(BusinessErrorMessage.Required);

            // OrderId absent = the push-to-start token — nothing to own or gate.
            When(x => !string.IsNullOrWhiteSpace(x.OrderId), () =>
            {
                RuleFor(x => x)
                    .Cascade(CascadeMode.Stop)
                    // S3: a foreign or non-existent order is indistinguishable — OrderNotFound either way.
                    .MustAsync(OrderBelongsToCallerAsync).WithMessage(BusinessErrorMessage.OrderNotFound)
                    .MustAsync(OrderIsRegisterableAsync).WithMessage(BusinessErrorMessage.LiveActivityOrderNotActive);
            });
        }

        private async Task<bool> OrderBelongsToCallerAsync(Command command, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId)) return false;

            var order = await _orderRepository
                .GetQueryable()
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order is not null && order.UserId == userId;
        }

        private async Task<bool> OrderIsRegisterableAsync(Command command, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order?.CurrentStatus is { } status && RegisterableStatuses.Contains(status);
        }
    }

    internal class Handler(
        ILiveActivityTokenRepository liveActivityTokenRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(userId), BusinessErrorMessage.UserNotFound));
            }

            var orderId = string.IsNullOrWhiteSpace(command.OrderId) ? null : command.OrderId;

            var existing = await liveActivityTokenRepository
                .GetByUserDeviceOrderAsync(userId, command.DeviceId, orderId, cancellationToken);

            if (existing is not null)
            {
                existing.Refresh(command.Token);
                return BusinessResult.Success(new Response(existing.Id));
            }

            var token = LiveActivityToken.Create(userId, command.DeviceId, orderId, command.Token, tenantId: null);
            liveActivityTokenRepository.Add(token);

            return BusinessResult.Success(new Response(token.Id));
        }
    }

    public record Response(string Id);
}
