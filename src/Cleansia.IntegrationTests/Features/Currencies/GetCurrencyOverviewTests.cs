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
                    { Code = "EUR", Symbol = "$", Name = "Euro", ExchangeRate = 0.041M });
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
}