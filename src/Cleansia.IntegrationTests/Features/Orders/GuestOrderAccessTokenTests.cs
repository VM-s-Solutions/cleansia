using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// F69, over real Postgres. A cleaner assigned to a guest's booking was served the unredacted order
/// detail, and that detail carried the display number, the customer's e-mail and the confirmation code
/// — exactly the triple the anonymous guest endpoints accepted. So the cleaner could cancel their own
/// customer's booking, and the cancellation charged the customer the 25 % / 50 % tier.
///
/// <para>These cases pin the three halves of the fix together on one seeded booking: the guest reaches
/// and cancels their order with the token their e-mail carried, the old triple opens nothing, and
/// everything the assigned cleaner is served is free of both credentials.</para>
/// </summary>
[Collection("PostgresCollection")]
public class GuestOrderAccessTokenTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string OrderId = "order-guest-token";
    private const string CurrencyId = "currency-czk-guest-token";
    private const string CountryId = "country-cz-guest-token";
    private const string GuestEmail = "guest-token@cleansia.test";
    private const string CleanerUserId = "user-guest-token";
    private const string CleanerEmployeeId = "employee-guest-token";
    private const string CleanerEmail = "cleaner-guest-token@cleansia.test";

    private string _accessToken = null!;
    private string _displayOrderNumber = null!;
    private string _confirmationCode = null!;

    [Fact]
    public async Task A_Guest_Reads_And_Cancels_With_The_Token_And_The_Booking_Retires_It()
    {
        await TestMethod<bool>(
            setup: Anonymous,
            arrange: SeedGuestOrderWithACrew,
            act: async (IServiceProvider provider) =>
            {
                var mediator = provider.GetRequiredService<IMediator>();

                var lookup = await mediator.Send(new LookupOrder.Query(_accessToken));
                Assert.True(lookup.IsSuccess, lookup.Error?.Message);
                Assert.Equal(OrderId, lookup.Value.Id);

                var batch = await mediator.Send(new LookupOrderBatch.Query([_accessToken]));
                Assert.True(batch.IsSuccess);
                Assert.Equal(OrderId, Assert.Single(batch.Value.Orders).Id);

                var preview = await mediator.Send(new GetGuestCancellationFeePreview.Query(_accessToken));
                Assert.True(preview.IsSuccess, preview.Error?.Message);

                var cancelled = await mediator.Send(new CancelGuestOrder.Command(_accessToken));
                Assert.True(cancelled.IsSuccess, cancelled.Error?.Message);
                return true;
            },
            assert: async (CleansiaDbContext db, bool _) =>
            {
                var order = await db.Orders.IgnoreQueryFilters().SingleAsync();
                Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);

                var token = Assert.Single(await db.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());
                Assert.NotNull(token.RevokedOn);
                Assert.Equal(SecurityTokens.Hash(_accessToken), token.TokenHash);
            },
            transactional: false);
    }

    [Fact]
    public async Task Only_The_Hash_Is_Stored_And_The_Expiry_Follows_The_Cleaning()
    {
        await TestMethod<bool>(
            setup: Anonymous,
            arrange: SeedGuestOrderWithACrew,
            act: _ => Task.FromResult(true),
            assert: async (CleansiaDbContext db, bool _) =>
            {
                var order = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync();
                var token = Assert.Single(await db.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());

                Assert.NotEqual(_accessToken, token.TokenHash);
                Assert.Equal(SecurityTokens.Hash(_accessToken), token.TokenHash);
                Assert.Equal(
                    GuestOrderAccessToken.ExpiryFor(order.CleaningDateTime),
                    token.ExpiresOn,
                    TimeSpan.FromSeconds(1));
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Old_Triple_Opens_Nothing()
    {
        await TestMethod<bool>(
            setup: Anonymous,
            arrange: SeedGuestOrderWithACrew,
            act: async (IServiceProvider provider) =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                foreach (var stale in new[] { _displayOrderNumber, GuestEmail, _confirmationCode })
                {
                    Assert.Equal(
                        BusinessErrorMessage.OrderNotFound,
                        (await mediator.Send(new LookupOrder.Query(stale))).Error?.Message);
                    Assert.Empty((await mediator.Send(new LookupOrderBatch.Query([stale]))).Value!.Orders);
                    Assert.Equal(
                        BusinessErrorMessage.OrderNotFound,
                        (await mediator.Send(new GetGuestCancellationFeePreview.Query(stale))).Error?.Message);
                    Assert.Equal(
                        BusinessErrorMessage.OrderNotFound,
                        (await mediator.Send(new CancelGuestOrder.Command(stale))).Error?.Message);
                }

                return true;
            },
            assert: async (CleansiaDbContext db, bool _) =>
            {
                var order = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync();
                Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
                Assert.Null(order.CancelledAt);
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Assigned_Cleaner_Is_Served_Neither_Credential()
    {
        await TestMethod<BusinessResult<OrderItem>>(
            setup: AssignedCleanerSession,
            arrange: SeedGuestOrderWithACrew,
            act: async (IServiceProvider provider) => await provider.GetRequiredService<IMediator>()
                .Send(new GetOrderDetails.Query(OrderId)),
            assert: (CleansiaDbContext _, BusinessResult<OrderItem> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var detail = result.Value!;

                // The cleaner IS on this job and reads it in full — without that the rest proves nothing.
                Assert.True(detail.IsAssignedToCurrentUser);
                Assert.Equal(GuestEmail, detail.CustomerEmail);

                // Everything the payload carries, as the client receives it. Asserting against the
                // serialized form rather than a field list is the point: a member added tomorrow is
                // covered without anybody remembering this file.
                var payload = JsonSerializer.Serialize(detail);
                Assert.DoesNotContain(_confirmationCode, payload, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(_accessToken, payload, StringComparison.Ordinal);
                return Task.CompletedTask;
            });
    }

    private static Task Anonymous(IServiceCollection services) => Task.CompletedTask;

    private static Task AssignedCleanerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CleanerUserId,
            CleanerEmail,
            [
                new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()),
                new Claim(TestUserSessionProvider.EmployeeIdClaimType, CleanerEmployeeId),
            ])));
        return Task.CompletedTask;
    }

    private async Task SeedGuestOrderWithACrew(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        context.Currencies.Add(currency);

        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        context.CountryConfigurations.Add(CountryConfiguration.Create(CountryId, "CZK", "cs", 0.21m));

        var cleanerUser = User.CreateWithPassword(
            CleanerEmail, TestConstants.TestUserSession.TestUserPassword, "Petra", "Svobodova", UserProfile.Employee);
        cleanerUser.Id = CleanerUserId;
        context.Users.Add(cleanerUser);
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = CleanerEmployeeId;
        context.Employees.Add(cleaner);

        var order = Order.Create(
            customerName: "Jana Novakova",
            customerEmail: GuestEmail,
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: null);
        order.Id = OrderId;
        order.UpdateEstimatedTime(120);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        context.Orders.Add(order);

        var accessToken = GuestOrderAccessToken.Issue(
            order.Id, GuestOrderAccessToken.ExpiryFor(order.CleaningDateTime));
        context.GuestOrderAccessTokens.Add(accessToken);

        _accessToken = accessToken.RawToken!;
        _displayOrderNumber = order.DisplayOrderNumber;
        _confirmationCode = order.ConfirmationCode;

        await context.CommitAsync(CancellationToken.None);
    }
}
