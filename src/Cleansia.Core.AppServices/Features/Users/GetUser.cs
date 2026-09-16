using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Users;

public class GetUser
{
    public class Validator : AbstractValidator<Query>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(user => user.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    public record Query(
        string UserId, string? OrderId = null)
        : IQuery<UserItem>;

    public class Handler(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IOrderRepository orderRepository,
        ITenantRepository tenantRepository)
        : IQueryHandler<Query, UserItem>
    {
        public async Task<BusinessResult<UserItem>> Handle(Query query, CancellationToken cancellationToken)
        {
            // Inner ownership gate (ADR-0001 §D3): a non-admin caller may only resolve
            // their own user record. Mirrors GetPeriodPays — a non-owner gets the not-found business
            // error rather than the other user's PII. The policy is the outer gate; this holds on any
            // invocation path.
            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            if (role != UserProfile.Administrator.ToString() &&
                query.UserId != userSessionProvider.GetUserId())
            {
                return BusinessResult.Failure<UserItem>(new Error(
                    nameof(Query.UserId), BusinessErrorMessage.NotExistingUserWithId));
            }

            var user = await userRepository.GetByIdNoTrackingAsync(query.UserId, cancellationToken);
            if (user is null)
            {
                if (role == UserProfile.Administrator.ToString() && !string.IsNullOrEmpty(query.OrderId))
                {
                    // The admin's filtered order proves access before the customer crosses the tenant filter.
                    var order = await orderRepository.GetQueryable().AsNoTracking()
                        .FirstOrDefaultAsync(o => o.Id == query.OrderId && o.UserId == query.UserId, cancellationToken);
                    if (order is not null)
                    {
                        var customer = await userRepository.GetByIdIgnoringTenantAsync(order.UserId!, cancellationToken);
                        if (customer is not null && customer.Profile == UserProfile.Customer && customer.TenantId is not null)
                        {
                            var company = await tenantRepository.GetByIdAsync(customer.TenantId, cancellationToken);
                            var email = customer.Email;
                            var at = email.IndexOf('@');
                            var maskedEmail = at > 0 ? $"{email[0]}***{email[at..]}" : "***";
                            var panel = new CustomerOfAnotherCompanyDto(customer.Id, customer.FirstName, maskedEmail, company?.Name ?? string.Empty);
                            return BusinessResult.Success(new UserItem(
                                string.Empty, customer.FirstName, string.Empty, null,
                                UserProfile.Customer.MapToCode(), default(Core.Domain.Enums.AuthenticationType).MapToCode(),
                                false, null, null, null, null, customer.Id, true, panel));
                        }
                    }
                }
                return BusinessResult.Failure<UserItem>(new Error(
                    nameof(Query.UserId), BusinessErrorMessage.NotExistingUserWithId));
            }

            return BusinessResult.Success(user.MapToDetailDto())!;
        }
    }
}
