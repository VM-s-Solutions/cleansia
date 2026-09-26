using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Users;

/// <summary>
/// A phone change is copied onto the caller's own orders that carried the old number — and onto nobody
/// else's. The lookup used to match the number alone, so another customer or a guest who gave the same
/// number had their booking's contact rewritten by a stranger's profile save. Against real Postgres, because
/// the scope lives in the query: a repository mock hands back whatever it is given and could not tell.
///
/// <para>The second case is the one that needs no shared number at all: a customer with no phone on file
/// matches on the empty string, which is what every phoneless recurring order carries.</para>
/// </summary>
[Collection("PostgresCollection")]
public class UpdateCurrentUserPhoneBackfillTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CallerId = TestConstants.TestUserSession.TestUserId;
    private const string OtherCustomerId = "user-phone-backfill-other";
    private const string CountryId = "country-cz-phone-backfill";
    private const string CurrencyId = "currency-czk-phone-bf";
    private const string OldPhone = "+420777111333";
    private const string NewPhone = "+420777000999";

    private const string OwnOrderId = "order-phone-backfill-own";
    private const string OtherCustomersOrderId = "order-phone-backfill-other";
    private const string GuestOrderId = "order-phone-backfill-guest";

    [Fact]
    public async Task A_Phone_Change_Reaches_The_Callers_Own_Orders_And_Not_Another_Customers_Or_A_Guests_On_The_Same_Number()
    {
        await TestMethod(
            arrange: context => SeedAsync(context, callerPhone: OldPhone, orderPhone: OldPhone),
            act: SaveNewPhoneAsync,
            assert: async (CleansiaDbContext context, BusinessResult<UpdateCurrentUser.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                await AssertOnlyTheCallersOrderChangedAsync(context, untouchedPhone: OldPhone);
            });
    }

    [Fact]
    public async Task A_Caller_With_No_Phone_On_File_Rewrites_Only_Their_Own_Phoneless_Orders()
    {
        await TestMethod(
            arrange: context => SeedAsync(context, callerPhone: null, orderPhone: string.Empty),
            act: SaveNewPhoneAsync,
            assert: async (CleansiaDbContext context, BusinessResult<UpdateCurrentUser.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                await AssertOnlyTheCallersOrderChangedAsync(context, untouchedPhone: string.Empty);
            });
    }

    private static async Task<BusinessResult<UpdateCurrentUser.Response>> SaveNewPhoneAsync(IServiceProvider provider) =>
        await provider.GetRequiredService<IMediator>().Send(new UpdateCurrentUser.Command(
            Id: null,
            FirstName: TestConstants.TestUserSession.TestFirstName,
            LastName: TestConstants.TestUserSession.TestLastName,
            PhoneNumber: NewPhone,
            BirthDate: null,
            Photo: null,
            LanguageCode: null));

    private static async Task AssertOnlyTheCallersOrderChangedAsync(CleansiaDbContext context, string untouchedPhone)
    {
        var phones = await context.Orders.IgnoreQueryFilters().ToDictionaryAsync(o => o.Id, o => o.CustomerPhone);

        Assert.Equal(NewPhone, phones[OwnOrderId]);
        Assert.Equal(untouchedPhone, phones[OtherCustomersOrderId]);
        Assert.Equal(untouchedPhone, phones[GuestOrderId]);
    }

    private static async Task SeedAsync(CleansiaDbContext context, string? callerPhone, string orderPhone)
    {
        if (!await context.Languages.AnyAsync())
        {
            context.Languages.Add(Language.Create("en", "English"));
        }

        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var caller = User.CreateWithPassword(
            email: TestConstants.TestUserSession.TestUserEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName);
        caller.Id = CallerId;
        caller.ConfirmEmail();
        caller.Update(caller.FirstName, caller.LastName, callerPhone, birthDate: null);

        var other = User.CreateWithPassword("other.phone-backfill@cleansia.test", "Seed-Password-123", "Other", "Customer");
        other.Id = OtherCustomerId;
        other.ConfirmEmail();
        context.Users.AddRange(caller, other);

        context.Orders.AddRange(
            NewOrder(OwnOrderId, CallerId, TestConstants.TestUserSession.TestUserEmail, orderPhone),
            NewOrder(OtherCustomersOrderId, OtherCustomerId, "other.phone-backfill@cleansia.test", orderPhone),
            NewOrder(GuestOrderId, userId: null, "stranger.phone-backfill@cleansia.test", orderPhone));

        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(string id, string? userId, string email, string phone)
    {
        var order = Order.Create(
            customerName: "Seeded Customer",
            customerEmail: email,
            customerPhone: phone,
            customerAddress: Address.Create($"Ulice {id}", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-10),
            paymentType: PaymentType.Card,
            totalPrice: 1250m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: userId);
        order.Id = id;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        return order;
    }
}
