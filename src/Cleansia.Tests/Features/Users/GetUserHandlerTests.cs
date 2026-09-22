using System.Security.Claims;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.TestUtilities.MockDataFactories.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// ADR-0001 §D3 part 3 — the inner ownership gate inside
/// <c>GetUser.Handler</c>. The policy is the outer gate; this handler check is the inner gate that
/// holds regardless of host or invocation path:
///   - a NON-admin caller asking for a UserId that is NOT their own sub gets the not-found business
///     error (<see cref="BusinessErrorMessage.NotExistingUserWithId"/>) and NOT the other user's PII;
///   - an Admin caller, or a caller asking for their OWN UserId, gets the detail.
/// These tests predate the handler fix (red → green) per knowledge/testing.md.
/// </summary>
public class GetUserHandlerTests
{
    private const string CallerSub = "caller-sub-1";
    private const string OtherUserId = "other-user-2";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ITenantRepository> _tenantRepository = new();

    private GetUser.Handler CreateHandler() =>
        new(_userRepository.Object, _session.Object, _orderRepository.Object, _tenantRepository.Object);

    private void SetCaller(string sub, UserProfile role)
    {
        _session.Setup(s => s.GetUserId()).Returns(sub);
        _session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, role.ToString()));
    }

    private User ArrangeUser(string id)
    {
        var user = UserMockFactory.Generate();
        // Force the entity id to the value the query asks for (BaseEntity.Id has a public setter).
        user.Id = id;
        _userRepository
            .Setup(r => r.GetByIdNoTrackingAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        return user;
    }

    [Fact]
    public async Task NonAdmin_Requesting_Other_UserId_Returns_NotFound_And_No_Pii()
    {
        var other = ArrangeUser(OtherUserId);
        SetCaller(CallerSub, UserProfile.Employee);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetUser.Query(OtherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.NotExistingUserWithId, result.Error!.Message);
        Assert.Equal(nameof(GetUser.Query.UserId), result.Error.Code);
        // The other user's PII never leaves the handler.
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public async Task NonAdmin_Requesting_Own_UserId_Returns_Detail()
    {
        ArrangeUser(CallerSub);
        SetCaller(CallerSub, UserProfile.Customer);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetUser.Query(CallerSub), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CallerSub, result.Value.Id);
    }

    [Fact]
    public async Task Admin_Requesting_Other_UserId_Returns_Detail()
    {
        var other = ArrangeUser(OtherUserId);
        SetCaller("admin-sub-9", UserProfile.Administrator);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetUser.Query(OtherUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OtherUserId, result.Value.Id);
    }

    /// <summary>
    /// A customer of another company sits behind the tenant filter, so the plain read misses them.
    /// The admin's own filtered order is the proof that lets the masked panel cross it — and only an
    /// order that belongs to that customer.
    /// </summary>
    private void ArrangeCustomerOfAnotherCompany(string customerId, params Order[] operatorOrders)
    {
        var customer = UserMockFactory.Generate();
        customer.Id = customerId;
        customer.TenantId = "company-a";
        _userRepository
            .Setup(r => r.GetByIdNoTrackingAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(operatorOrders.AsQueryable().BuildMock());
        _tenantRepository
            .Setup(r => r.GetByIdAsync("company-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Tenant.Create("company-a", "Company A"));
    }

    [Fact]
    public async Task Admin_Without_An_Order_Proof_Cannot_Reach_A_Customer_Of_Another_Company()
    {
        ArrangeCustomerOfAnotherCompany(OtherUserId);
        SetCaller("admin-sub-9", UserProfile.Administrator);

        var result = await CreateHandler().Handle(new GetUser.Query(OtherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.NotExistingUserWithId, result.Error!.Message);
    }

    [Fact]
    public async Task An_Order_Of_Someone_Else_Does_Not_Prove_Access_To_The_Customer()
    {
        var someoneElsesOrder = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial { Id = "order-1", UserId = "third-user" });
        ArrangeCustomerOfAnotherCompany(OtherUserId, someoneElsesOrder);
        SetCaller("admin-sub-9", UserProfile.Administrator);

        var result = await CreateHandler().Handle(new GetUser.Query(OtherUserId, "order-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.NotExistingUserWithId, result.Error!.Message);
    }

    [Fact]
    public async Task The_Customers_Own_Order_Proves_Access_To_The_Masked_Panel_Only()
    {
        var theCustomersOrder = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial { Id = "order-1", UserId = OtherUserId });
        ArrangeCustomerOfAnotherCompany(OtherUserId, theCustomersOrder);
        SetCaller("admin-sub-9", UserProfile.Administrator);

        var result = await CreateHandler().Handle(new GetUser.Query(OtherUserId, "order-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.CustomerOfAnotherCompany);
        Assert.Equal(OtherUserId, result.Value.CustomerOfAnotherCompany!.Id);
        Assert.Equal("Company A", result.Value.CustomerOfAnotherCompany.CompanyName);
        Assert.Equal(string.Empty, result.Value.Email);
        Assert.Equal(string.Empty, result.Value.LastName);
    }
}
