using System.Security.Claims;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Authentication;

public class OrderAccessService : IOrderAccessService
{
    private readonly IUserSessionProvider _userSessionProvider;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly Lazy<Task<string?>> _callerEmployeeId;

    public OrderAccessService(
        IUserSessionProvider userSessionProvider,
        IEmployeeRepository employeeRepository)
    {
        _userSessionProvider = userSessionProvider;
        _employeeRepository = employeeRepository;
        _callerEmployeeId = new Lazy<Task<string?>>(ResolveCallerEmployeeIdAsync);
    }

    public bool IsCustomerCaller()
    {
        var role = _userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
        return role == UserProfile.Customer.ToString();
    }

    public Task<string?> GetCallerEmployeeIdAsync(CancellationToken cancellationToken)
    {
        return _callerEmployeeId.Value;
    }

    public async Task<bool> CanAccessOrderAsync(Order order, CancellationToken cancellationToken)
    {
        var role = _userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
        if (role == UserProfile.Administrator.ToString())
        {
            return true;
        }

        var userId = _userSessionProvider.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        if (order.UserId == userId)
        {
            return true;
        }

        if (role != UserProfile.Employee.ToString())
        {
            return false;
        }

        var employeeId = await GetCallerEmployeeIdAsync(cancellationToken);
        if (string.IsNullOrEmpty(employeeId))
        {
            return false;
        }

        return order.AssignedEmployees.Any(ae => ae.EmployeeId == employeeId);
    }

    public async Task<bool> CanBrowseOrderAsync(Order order, CancellationToken cancellationToken)
    {
        if (await CanAccessOrderAsync(order, cancellationToken))
        {
            return true;
        }

        // Employee browsing the available list: let them open detail of an
        // order they could take, so they can read it before tapping Take.
        var role = _userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
        if (role != UserProfile.Employee.ToString())
        {
            return false;
        }

        // Three questions, three rules, all in-memory forms of the ones the board and the take gate
        // already read. OrderAvailability (ADR-0037, Q-BROWSE-01 owner ruling) is the one that was
        // missing: without it this branch admitted cancelled orders, finished ones whose crew was
        // never filled, and card orders whose money has not landed — none of which TakeOrder accepts.
        // Reached only after CanAccessOrderAsync returned true for an administrator, for the order's
        // own customer and for an assigned cleaner, so narrowing it cannot hide a job from anyone who
        // is on it.
        var employeeId = await GetCallerEmployeeIdAsync(cancellationToken);
        return !string.IsNullOrEmpty(employeeId)
            && OrderAvailability.IsOfferable(
                order.CurrentStatus, order.PaymentType, order.PaymentStatus, order.RecurringTemplateId)
            && order.HasAvailableSpots
            && OrderVisibility.NotHeldFrom(order, employeeId, DateTime.UtcNow);
    }

    private async Task<string?> ResolveCallerEmployeeIdAsync()
    {
        var fromClaim = _userSessionProvider.GetEmployeeId();
        if (!string.IsNullOrEmpty(fromClaim))
        {
            return fromClaim;
        }

        var email = _userSessionProvider.GetUserEmail();
        if (string.IsNullOrEmpty(email))
        {
            return null;
        }

        var employee = await _employeeRepository.GetByUserEmailAsync(email);
        return employee?.Id;
    }
}
