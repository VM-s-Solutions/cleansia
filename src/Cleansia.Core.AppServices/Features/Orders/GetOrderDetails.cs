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
using Microsoft.EntityFrameworkCore;

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
        IOrderPhotoRepository orderPhotoRepository,
        IEmployeeRepository employeeRepository,
        IExpressWaiverConsumer expressWaiverConsumer,
        IUserMembershipRepository userMembershipRepository) : IQueryHandler<Query, OrderItem>
    {
        public async Task<BusinessResult<OrderItem>> Handle(Query query, CancellationToken cancellationToken)
        {
            var order = await orderRepository.GetByIdAsync(query.OrderId, cancellationToken);
            if (order == null || !await orderAccessService.CanBrowseOrderAsync(order, cancellationToken))
            {
                return BusinessResult.Failure<OrderItem>(new Error(
                    nameof(query.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            // Browse admits a cleaner to an order they have not taken so they can judge it; only the
            // strict gate entitles a caller to the customer. Read from the same seam that granted
            // access rather than re-deriving "assigned or owner" here, so the two cannot disagree.
            var isEntitledToCustomerData = await orderAccessService.CanAccessOrderAsync(order, cancellationToken);

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

            // Customer-only: a cleaner must never see a customer's entitlements. Null for every other
            // caller, which the clients render as "no marking".
            var isCustomerCaller = orderAccessService.IsCustomerCaller();
            bool? expressWaiverForfeitedOnCancel = isCustomerCaller
                ? await expressWaiverConsumer.WouldForfeitOnCustomerCancelAsync(
                    order.Id, order.AssignedEmployees.Count > 0, cancellationToken)
                : null;

            var detail = order.MapToDetail(
                estimatedCleanerPay,
                isAssignedToCurrentUser,
                hasAfterPhotos,
                isCustomerCaller,
                expressWaiverForfeitedOnCancel,
                isCustomerCaller
                    ? await ResolvePreferredOfferAsync(order, DateTime.UtcNow, cancellationToken)
                    : null);

            if (!isEntitledToCustomerData)
            {
                return BusinessResult.Success(detail.RedactForBrowsingCleaner());
            }

            // The customer wrote these instructions and the assigned cleaner is standing at the door;
            // an admin is neither. They get the reveal route, which is audited — see
            // OrderPiiRedaction.WithholdAccessInstructions.
            var isAdminCaller = role == UserProfile.Administrator.ToString();

            return BusinessResult.Success(isAdminCaller
                ? detail.WithholdAccessInstructions()
                : detail);
        }

        /// <summary>
        /// ADR-0045 D7.2. Customer-only by its one call site: a cleaner must never learn that an order
        /// is reserved for somebody else, and nobody is ever told they were passed over.
        ///
        /// <para>ADR-0049 — and null once the block's sentence has stopped being true of this booking.
        /// The whole block goes rather than a fifth state, so no client is left holding a value it has
        /// to be told to render as nothing.</para>
        /// </summary>
        private async Task<PreferredOfferDetails?> ResolvePreferredOfferAsync(
            Order order, DateTime nowUtc, CancellationToken cancellationToken)
        {
            var beneficiaryIsAssigned = !string.IsNullOrEmpty(order.PreferredEmployeeId)
                && order.AssignedEmployees.Any(ae => ae.EmployeeId == order.PreferredEmployeeId);

            var state = PreferredOffer.StateOf(
                order.PreferredEmployeeId, order.PreferredHoldUntilUtc, beneficiaryIsAssigned, nowUtc);

            if (!PreferredOffer.IsDisclosable(state, order.CurrentStatus, order.AvailableSpots))
            {
                return null;
            }

            var cleanerName = string.IsNullOrEmpty(order.PreferredEmployeeId)
                ? null
                : await employeeRepository.GetQueryable()
                    .AsNoTracking()
                    .Where(e => e.Id == order.PreferredEmployeeId && e.User != null)
                    .Select(e => (e.User!.FirstName + " " + e.User.LastName).Trim())
                    .FirstOrDefaultAsync(cancellationToken);

            var callerHasActiveMembership = await PreferredOfferExit.CallerHasActiveMembershipAsync(
                userSessionProvider, userMembershipRepository, cancellationToken);

            return new PreferredOfferDetails(
                State: state,
                CleanerName: cleanerName,
                RespondByUtc: state == PreferredOfferState.AwaitingConfirmation
                    ? order.PreferredHoldUntilUtc
                    : null,
                CanChooseAnother: PreferredOfferExit.IsOpen(order, callerHasActiveMembership, nowUtc));
        }
    }
}
