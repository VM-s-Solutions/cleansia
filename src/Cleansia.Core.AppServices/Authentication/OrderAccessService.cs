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

        // Employee browsing the available list: let them open detail of any
        // order that still has open spots, so they can read full info before
        // tapping Take.
        var role = _userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
        if (role != UserProfile.Employee.ToString())
        {
            return false;
        }

        var employeeId = await GetCallerEmployeeIdAsync(cancellationToken);
        return !string.IsNullOrEmpty(employeeId) && order.HasAvailableSpots;
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
