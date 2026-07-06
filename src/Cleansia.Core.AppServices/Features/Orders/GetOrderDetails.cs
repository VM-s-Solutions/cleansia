using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
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
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider,
        IEmployeePayConfigRepository payConfigRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository,
        IOrderPhotoRepository orderPhotoRepository) : IQueryHandler<Query, OrderItem>
    {
        public async Task<BusinessResult<OrderItem>> Handle(Query query, CancellationToken cancellationToken)
        {
            var order = await orderRepository.GetByIdAsync(query.OrderId, cancellationToken);
            if (order == null || !await orderAccessService.CanBrowseOrderAsync(order, cancellationToken))
            {
                return BusinessResult.Failure<OrderItem>(new Error(
                    nameof(query.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            // Photos count is cheap to look up and lets the partner
            // mobile gate the Complete slide client-side. Same query
            // CompleteOrder.Validator uses, so the two stay in sync.
            var afterPhotoCount = await orderPhotoRepository
                .GetPhotoCountByOrderIdAndTypeAsync(order.Id, PhotoType.After, cancellationToken);
            var hasAfterPhotos = afterPhotoCount > 0;

            // Resolve caller-context fields: only employee callers get a
            // pay estimate or a meaningful "is this mine?" flag. Admins
            // and customers get null + false respectively.
            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            var isEmployeeCaller = role == UserProfile.Employee.ToString();

            decimal? estimatedCleanerPay = null;
            var isAssignedToCurrentUser = false;

            if (isEmployeeCaller)
            {
                var callerEmployeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
                if (!string.IsNullOrEmpty(callerEmployeeId))
                {
                    isAssignedToCurrentUser = order.AssignedEmployees
                        .Any(ae => ae.EmployeeId == callerEmployeeId);

                    // Prefer the persisted OrderEmployeePay row when the
                    // cleaner has actually been paid for this order — that's
                    // the authoritative number. Fall back to a live estimate
                    // from the pay-config repo for offers they could still
                    // take and confirmed work that hasn't been paid yet.
                    var existingPay = await orderEmployeePayRepository.GetByOrderAndEmployeeAsync(
                        order.Id, callerEmployeeId, cancellationToken);
                    if (existingPay?.TotalPay != null)
                    {
                        estimatedCleanerPay = existingPay.TotalPay;
                    }
                    else
                    {
                        var serviceIds = order.SelectedServices.Select(s => s.ServiceId).Distinct().ToList();
                        var packageIds = order.SelectedPackages.Select(p => p.PackageId).Distinct().ToList();

                        IReadOnlyList<EmployeePayConfig> serviceConfigs = Array.Empty<EmployeePayConfig>();
                        IReadOnlyList<EmployeePayConfig> packageConfigs = Array.Empty<EmployeePayConfig>();
                        if (serviceIds.Count > 0)
                        {
                            serviceConfigs = await payConfigRepository.GetServiceConfigsForOrderAsync(
                                serviceIds, callerEmployeeId, cancellationToken);
                        }
                        if (packageIds.Count > 0)
                        {
                            packageConfigs = await payConfigRepository.GetPackageConfigsForOrderAsync(
                                packageIds, callerEmployeeId, cancellationToken);
                        }

                        estimatedCleanerPay = OrderPayEstimator.Estimate(
                            order, callerEmployeeId, serviceConfigs, packageConfigs);
                    }
                }
            }

            return BusinessResult.Success(order.MapToDetail(
                estimatedCleanerPay,
                isAssignedToCurrentUser,
                hasAfterPhotos,
                orderAccessService.IsCustomerCaller()));
        }
    }
}
