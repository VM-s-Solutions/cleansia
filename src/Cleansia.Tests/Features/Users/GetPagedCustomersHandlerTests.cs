using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Core.AppServices.Features.Users.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Users;

public class GetPagedCustomersHandlerTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private Expression<Func<User, bool>>? _captured;

    public GetPagedCustomersHandlerTests()
    {
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<User, bool>>?, CancellationToken>((f, _) => _captured = f)
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.GetPagedSort<UserSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<User>().AsQueryable().BuildMock());
    }

    private Task<PagedData<AdminCustomerListItem>> Handle(GetPagedCustomers.Request request)
    {
        var handlerType = typeof(GetPagedCustomers).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, _repository.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<AdminCustomerListItem>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private async Task<Func<User, bool>> PredicateFor(CustomerFilter? filter)
    {
        await Handle(new GetPagedCustomers.Request { Filter = filter });
        Assert.NotNull(_captured);
        return _captured!.Compile();
    }

    private static User Customer(string firstName = "Jana", string lastName = "Novakova",
        string email = "jn.home@example.cz", string? phone = "+420777123456")
    {
        var user = User.CreateWithPassword(email, "hash", firstName, lastName, UserProfile.Customer);
        user.Update(firstName, lastName, phone);
        return user;
    }

    [Fact]
    public async Task Only_Customers_Are_Listed_Never_Cleaners_Or_Administrators()
    {
        var predicate = await PredicateFor(null);

        Assert.True(predicate(Customer()));
        Assert.False(predicate(User.CreateWithPassword("cleaner@example.cz", "hash", "Jana", "Novakova", UserProfile.Employee)));
        Assert.False(predicate(User.CreateWithPassword("admin@example.cz", "hash", "Jana", "Novakova", UserProfile.Administrator, adminRole: AdminRole.Support)));
    }

    [Theory]
    [InlineData("jAnA")]
    [InlineData("novak")]
    [InlineData("JN.HOME")]
    [InlineData("777123")]
    public async Task The_Search_Matches_First_Name_Last_Name_Email_And_Phone(string term)
    {
        var predicate = await PredicateFor(new CustomerFilter(SearchTerm: term, IsActive: null));

        Assert.True(predicate(Customer()));
        Assert.False(predicate(Customer("Petr", "Svoboda", "petr@example.sk", "+421900000000")));
    }

    [Fact]
    public async Task A_Customer_Without_A_Phone_Is_Not_Matched_By_A_Phone_Search()
    {
        var predicate = await PredicateFor(new CustomerFilter(SearchTerm: "777", IsActive: null));

        Assert.False(predicate(Customer(phone: null)));
    }

    [Fact]
    public async Task The_Active_Filter_Splits_Active_From_Deactivated_And_Null_Lists_Both()
    {
        var active = Customer();
        var deactivated = Customer();
        deactivated.IsActive = false;

        var onlyActive = await PredicateFor(new CustomerFilter(SearchTerm: null, IsActive: true));
        Assert.True(onlyActive(active));
        Assert.False(onlyActive(deactivated));

        var onlyDeactivated = await PredicateFor(new CustomerFilter(SearchTerm: null, IsActive: false));
        Assert.False(onlyDeactivated(active));
        Assert.True(onlyDeactivated(deactivated));

        var both = await PredicateFor(new CustomerFilter(SearchTerm: null, IsActive: null));
        Assert.True(both(active));
        Assert.True(both(deactivated));
    }

    [Fact]
    public async Task Projects_The_Row_And_The_Page_Metadata()
    {
        var customer = Customer();
        customer.ConfirmEmail();
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        _repository
            .Setup(r => r.GetPagedSort<UserSort>(
                5, 5, It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { customer }.AsQueryable().BuildMock());

        var result = await Handle(new GetPagedCustomers.Request { Offset = 5, Limit = 5 });

        Assert.Equal(7, result.Total);
        Assert.Equal(2, result.PageNumber);
        var row = Assert.Single(result.Data);
        Assert.Equal(
            new AdminCustomerListItem(customer.Id, "Jana", "Novakova", "jn.home@example.cz", "+420777123456",
                IsActive: true, IsEmailConfirmed: true, customer.CreatedOn),
            row);
    }
}
