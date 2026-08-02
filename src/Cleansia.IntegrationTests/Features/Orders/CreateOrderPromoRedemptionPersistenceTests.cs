using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Service = Cleansia.Core.Domain.Services.Service;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// Drives the REAL <c>CreateOrder</c> handler end-to-end over REAL PostgreSQL (Testcontainers) with a
/// valid promo code, and asserts the <c>PromoCodeRedemptions</c> row actually lands. The existing unit
/// coverage all mocks <c>IPromoCodeRedemptionRepository</c>, so nothing in the suite executes the raw
/// reservation INSERT against the live <c>FK_PromoCodeRedemptions_Orders_OrderId</c>.
///
/// Runs non-transactional so the reservation's auto-commit and the UnitOfWork commit are the two
/// separate transactions they are in production, rather than being folded into one ambient scope.
/// </summary>
[Collection("PostgresCollection")]
public class CreateOrderPromoRedemptionPersistenceTests(PostgresContainerFixture fixture)
    : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-promoredeem";
    private const string CountryId = "country-cz-promoredeem";
    private const string CategoryId = "category-promoredeem";
    private const string ServiceId = "service-promoredeem";
    private const string PromoCodeId = "promo-promoredeem";
    private const string CustomerUserId = "user-cust-promoredeem";

    private const string CustomerEmail = "promo-redeem@cleansia.test";
    private const string PromoCodeText = "REDEEM20";
    private const string City = "Praha";

    private const decimal ServiceBasePrice = 1000m;
    private const decimal QuotedTotal = 1000m;
    private const decimal ExpectedDiscount = 200m;

    [Fact]
    public async Task CreateOrder_WithValidPromoCode_PersistsRedemptionRow()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedCatalogUserAndPromoCode,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                try
                {
                    var result = await mediator.Send(BuildCommand());
                    return new CreateAttempt(
                        result.IsSuccess ? result.Value.Id : null, result.Error?.Message, null);
                }
                catch (Exception ex)
                {
                    return new CreateAttempt(null, null, ex);
                }
            },
            assert: async (CleansiaDbContext context, CreateAttempt attempt) =>
            {
                Assert.True(
                    attempt.Exception is null,
                    $"CreateOrder threw instead of returning: {attempt.Exception}");

                Assert.False(
                    string.IsNullOrEmpty(attempt.OrderId),
                    $"CreateOrder failed with error: {attempt.ErrorMessage}");

                var orderExists = await context.Orders
                    .IgnoreQueryFilters()
                    .AnyAsync(o => o.Id == attempt.OrderId);
                Assert.True(orderExists, "The order row was not persisted.");

                var redemption = await context.PromoCodeRedemptions
                    .IgnoreQueryFilters()
                    .SingleOrDefaultAsync(r => r.OrderId == attempt.OrderId);

                Assert.True(
                    redemption is not null,
                    "No PromoCodeRedemptions row exists for the created order — the redemption was "
                    + "not recorded, so per-user and global usage limits are unenforced.");

                Assert.Equal(PromoCodeId, redemption!.PromoCodeId);
                Assert.Equal(CustomerUserId, redemption.UserId);
                Assert.Equal(ExpectedDiscount, redemption.AppliedDiscount);
                Assert.Equal(0, redemption.SlotOrdinal);
            },
            transactional: false);
    }

    private static CreateOrder.Command BuildCommand() => new(
        CustomerName: "Promo Customer",
        CustomerEmail: CustomerEmail,
        CustomerPhone: "+420777111222",
        CustomerAddress: new AddressDto("Testovaci 12", City, "11000", CountryId, null),
        SavedAddressId: null,
        SelectedPackageIds: [],
        SelectedServiceIds: [ServiceId],
        Rooms: 2,
        Bathrooms: 1,
        Extras: new Dictionary<string, bool>(),
        CleaningDate: DateTime.UtcNow.AddDays(3),
        PaymentType: PaymentType.Card,
        CurrencyId: CurrencyId,
        TotalPrice: QuotedTotal,
        PromoCode: PromoCodeText);

    private static Task ConfigureCustomerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CustomerUserId,
            CustomerEmail,
            [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));

        // Mobile channel short-circuits the Card branch before any Stripe call, so the test needs no
        // gateway; the promo path under test is identical on both channels.
        services.Replace(ServiceDescriptor.Singleton<IOrderChannelProvider>(
            _ => new OrderChannelProvider(OrderChannel.Mobile)));

        services.Replace(ServiceDescriptor.Scoped<IAddressGeocoder, NoopAddressGeocoder>());

        return Task.CompletedTask;
    }

    private static async Task SeedCatalogUserAndPromoCode(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        context.Add(ServiceCity.Create(CountryId, City));

        // Exchange rate 1.0 keeps the quoted total equal to the catalog price, so the validator's
        // price-match check is exact and the promo discount is a clean 20% of 1000.
        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var category = ServiceCategory.Create("promo-redeem", "Promo Redeem", "Category under test");
        category.Id = CategoryId;
        context.Add(category);

        var service = Service.Create(
            CategoryId, "Promo Redeem Service", "Service under test", ServiceBasePrice, 0m, 60);
        service.Id = ServiceId;
        context.Add(service);

        var user = User.CreateWithPassword(
            CustomerEmail,
            Constants.TestUserSession.TestUserPassword,
            "Promo",
            "Customer",
            UserProfile.Customer);
        user.Id = CustomerUserId;
        user.ConfirmEmail();
        context.Add(user);

        var promoCode = PromoCode.CreatePercent(PromoCodeText, 0.20m, maxRedemptionsPerUser: 1);
        promoCode.Id = PromoCodeId;
        context.Add(promoCode);

        await context.CommitAsync(CancellationToken.None);
    }

    private sealed record CreateAttempt(string? OrderId, string? ErrorMessage, Exception? Exception);

    private sealed class NoopAddressGeocoder : IAddressGeocoder
    {
        public Task PopulateCoordinatesAsync(Address address, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
