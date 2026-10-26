using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Infra.Database;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Countries;

/// <summary>
/// The booking gate's read over real Postgres (owner ruling 2026-10-25, decision 9): a row's ZIP prefix
/// is read with its name, so a Prague row serves a Prague postcode whatever the address's city says and
/// refuses another valid postcode even when the name matches, while a row without a prefix serves by name
/// and an inactive row serves nothing.
/// </summary>
[Collection("PostgresCollection")]
public class ServiceCityRepositoryTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-servicecity";

    [Fact]
    public async Task A_Row_With_A_Zip_Prefix_Serves_By_The_Postcode_And_A_Row_Without_One_By_The_Name()
    {
        await TestMethod(
            arrange: SeedAsync,
            act: async provider =>
            {
                var repository = provider.GetRequiredService<IServiceCityRepository>();
                return new[]
                {
                    await repository.CityIsServicedAsync(CountryId, "Praha", "110 00", CancellationToken.None),
                    await repository.CityIsServicedAsync(CountryId, "Прага", "110 00", CancellationToken.None),
                    await repository.CityIsServicedAsync(CountryId, "Praha", "602 00", CancellationToken.None),
                    await repository.CityIsServicedAsync(CountryId, "Praha", "", CancellationToken.None),
                    await repository.CityIsServicedAsync(CountryId, "Brno-střed", "602 00", CancellationToken.None),
                    await repository.CityIsServicedAsync(CountryId, "Ostrava", "702 00", CancellationToken.None),
                };
            },
            assert: (CleansiaDbContext _, bool[] serviced) =>
            {
                Assert.Equal([true, true, false, true, true, false], serviced);
                return Task.CompletedTask;
            });
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        context.Add(ServiceCity.Create(CountryId, "Praha", "1"));
        context.Add(ServiceCity.Create(CountryId, "Brno"));
        var ostrava = ServiceCity.Create(CountryId, "Ostrava");
        ostrava.IsActive = false;
        context.Add(ostrava);

        await context.CommitAsync(CancellationToken.None);
    }
}
