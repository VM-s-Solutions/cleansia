using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class GetOrderCustomer
{
    public record Query(string OrderId) : IQuery<UserItem>;

    public class Handler(IOrderRepository orderRepository, IUserSessionProvider userSessionProvider, IMediator mediator)
        : IQueryHandler<Query, UserItem>
    {
        public async Task<BusinessResult<UserItem>> Handle(Query query, CancellationToken cancellationToken)
        {
            if (userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value != UserProfile.Administrator.ToString())
                return NotFound();

            var userId = await orderRepository.GetQueryable().AsNoTracking()
                .Where(o => o.Id == query.OrderId)
                .Select(o => o.UserId)
                .FirstOrDefaultAsync(cancellationToken);
            return userId is null
                ? NotFound()
                : await mediator.Send(new GetUser.Query(userId, query.OrderId), cancellationToken);
        }

        private static BusinessResult<UserItem> NotFound() => BusinessResult.Failure<UserItem>(
            new Error(nameof(Query.OrderId), BusinessErrorMessage.OrderNotFound));
    }
}
