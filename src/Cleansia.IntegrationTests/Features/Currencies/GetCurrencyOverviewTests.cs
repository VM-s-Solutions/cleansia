using Cleansia.Core.AppServices.Features.Currencies;
using Cleansia.TestUtilities.MockDataFactories.Currencies;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Currencies;

[Collection("PostgresCollection")]
public class GetCurrencyOverviewTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task ShouldRetrieveAllCurrenciesSuccessfully()
    {
        await TestMethod(
            arrange: async context =>
            {
                var currency1 = CurrencyMockFactory.Generate();
                var currency2 = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial
                    { Code = "EUR", Symbol = "$", Name = "Euro" });
                context.Currencies.AddRange(currency1, currency2);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var request = new GetCurrencyOverview.Request();
                return await mediator.Send(request);
            },
            assert: async (context, result) =>
            {
                var currencies = await context.Currencies.ToListAsync();
                Assert.Equal(currencies.Count, result.Count());
                foreach (var currency in currencies)
                {
                    var dto = result.FirstOrDefault(c => c.Id == currency.Id);
                    Assert.NotNull(dto);
                    Assert.Equal(currency.Name, dto.Name);
                }
            }
        );
    }

    /// <summary>
    /// The admin list carries the market switch, and it has to survive the round trip to Postgres.
    ///
    /// <para>This is the shape the DEV seed ships — CZK operated, EUR present and not yet switched on
    /// — and the whole point of the separate admin DTO. A projection that dropped the flag, or one
    /// that read <c>IsActive</c> from the wrong place, would look identical in a unit test built from
    /// objects; only a real query answers it.</para>
    /// </summary>
    [Fact]
    public async Task AdminOverviewCarriesWhetherThePlatformOperatesInEachCurrency()
    {
        await TestMethod(
            arrange: async context =>
            {
                var operated = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial
                    { Code = "CZK", Symbol = "Kc", Name = "Czech koruna" });
                var notOperated = CurrencyMockFactory.Generate(new CurrencyMockFactory.CurrencyPartial
                    { Code = "EUR", Symbol = "E", Name = "Euro" });
                notOperated.IsActive = false;
                context.Currencies.AddRange(operated, notOperated);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new GetAdminCurrencyOverview.Request());
            },
            assert: async (context, result) =>
            {
                var rows = result.ToList();

                var czk = rows.FirstOrDefault(c => c.Code == "CZK");
                Assert.NotNull(czk);
                Assert.True(czk.IsActive);

                var eur = rows.FirstOrDefault(c => c.Code == "EUR");
                Assert.NotNull(eur);
                Assert.False(eur.IsActive);

                // The inactive row is RETURNED, not filtered. An admin has to see a currency in order
                // to price the catalogue in it before switching it on, which is the only path a
                // currency has into service.
                Assert.Equal(await context.Currencies.CountAsync(), rows.Count);
            }
        );
    }
}