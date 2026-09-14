using System.IdentityModel.Tokens.Jwt;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Tenancy;

/// <summary>
/// ADR-0051's bypass-and-re-pin cell after activation (ADR-0061 D3 last bullet, D4 corollary): an
/// anonymous read keyed on a server-issued secret reaches a stamped row without knowing which company
/// stamped it. TC-TEN-LOOKUP — a guest order is found by (number, email, code) from a request with no
/// market, and not with a wrong email. TC-TEN-CONFIRM — a legacy 128-bit confirmation link confirms a
/// stamped account and the JWT it returns carries that account's company; the confirmation writes an
/// audit row on refusal, so it is market-scoped (default market) and the seed carries that market —
/// the account's own company still wins on the token (override replaces override).
/// </summary>
[Collection("PostgresCollection")]
public sealed class SecretKeyedAnonymousReadsTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-secret";
    private const string CurrencyId = "currency-czk-secret";
    private const string GuestEmail = "guest@cleansia.test";

    [Fact]
    public async Task A_Guest_Order_Is_Found_By_Its_Secret_From_A_Request_With_No_Market_And_Not_By_A_Wrong_Email()
    {
        Order? order = null;
        await TestMethod(
            setup: Anonymous,
            arrange: async context =>
            {
                var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
                country.Id = CountryId;
                context.Countries.Add(country);
                var currency = Currency.Create("CZK", "Kč", "Czech koruna");
                currency.Id = CurrencyId;
                currency.IsActive = true;
                context.Currencies.Add(currency);

                order = Order.Create(
                    customerName: "Guest",
                    customerEmail: GuestEmail,
                    customerPhone: "+420777000222",
                    customerAddress: Address.Create("Secret St 1", "Brno", "60200", CountryId),
                    rooms: 1,
                    bathrooms: 1,
                    cleaningDateTime: DateTime.UtcNow.AddDays(2),
                    paymentType: PaymentType.Cash,
                    totalPrice: 1000m,
                    currencyId: CurrencyId,
                    paymentStatus: PaymentStatus.Pending,
                    userId: null);
                order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
                order.TenantId = TestTenants.Second;
                context.Orders.Add(order);
                StampUnstampedAdded(context, TestTenants.Second);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var found = await mediator.Send(new LookupOrder.Query(order!.DisplayOrderNumber, GuestEmail, order.ConfirmationCode));
                var wrongEmail = await mediator.Send(new LookupOrder.Query(order.DisplayOrderNumber, "someone-else@cleansia.test", order.ConfirmationCode));
                return (found, wrongEmail);
            },
            assert: (_, results) =>
            {
                Assert.True(results.found.IsSuccess, results.found.Error?.Message);
                Assert.Equal(order!.Id, results.found.Value.Id);
                Assert.False(results.wrongEmail.IsSuccess);
                Assert.Equal(BusinessErrorMessage.OrderNotFound, results.wrongEmail.Error?.Message);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Legacy_Confirmation_Link_Confirms_A_Stamped_Account_And_The_Jwt_Carries_Its_Company()
    {
        var rawLegacyToken = SecurityTokens.Generate();
        string? userId = null;
        await TestMethod(
            setup: Anonymous,
            arrange: async context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                var czk = Currency.Create("CZK", "Kč", "Czech koruna");
                czk.Id = CurrencyId;
                czk.IsActive = true;
                czk.SetAsDefault(true);
                context.Currencies.Add(czk);
                var czechia = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
                czechia.Id = CountryId;
                context.Countries.Add(czechia);
                context.CountryConfigurations.Add(
                    CountryConfiguration.Create(CountryId, "CZK", "cs", 0.21m).AssignOperator(TestTenants.Default).SetAsDefaultMarket(true));
                var user = User.CreateWithPassword("legacy-link@cleansia.test", TestUtilities.Constants.TestUserSession.TestUserPassword, "Leg", "Acy");
                user.TenantId = TestTenants.Second;
                context.Users.Add(user);
                await context.CommitAsync(CancellationToken.None);
                userId = user.Id;

                // An in-flight pre-OTP link: the row holds the hash of a 22-char token, not a 6-digit code.
                await context.Database.ExecuteSqlAsync(
                    $"""UPDATE "Users" SET "ConfirmationCode" = {SecurityTokens.Hash(rawLegacyToken)}, "ConfirmationCodeExpiresAt" = {DateTimeOffset.UtcNow.AddMinutes(15)} WHERE "Id" = {user.Id}""");
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new ConfirmUserEmail.Command(rawLegacyToken)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.True(result.Value.IsEmailConfirmed);
                var claims = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.Token).Claims;
                Assert.Equal(TestTenants.Second, Assert.Single(claims, c => c.Type == "tenant_id").Value);

                var confirmed = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == userId);
                Assert.True(confirmed.IsEmailConfirmed);
                Assert.Equal(TestTenants.Second, (await context.RefreshTokens.IgnoreQueryFilters().SingleAsync(t => t.UserId == userId)).TenantId);
            },
            transactional: false);
    }

    private static Task Anonymous(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(sp =>
            new TenantProvider(sp.GetRequiredService<IHttpContextAccessor>())));
        return Task.CompletedTask;
    }
}
