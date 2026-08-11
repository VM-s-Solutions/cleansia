using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.TestUtilities.MockDataFactories.Packages;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Packages;

[Collection("PostgresCollection")]
public class GetPackageOverviewTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task ShouldRetrieveAllPackagesSuccessfully()
    {
        await TestMethod(
            arrange: async context =>
            {
                var package1 = PackageMockFactory.Generate();
                var package2 = PackageMockFactory.Generate(new PackageMockFactory.PackagePartial { Name = "Name2", Description = "Description2" });
                context.Packages.AddRange(package1, package2);

                // The overview offers an entry only when it is quotable, so each seeded package needs
                // its platform-wide pay config or the wizard withholds it and this asserts nothing.
                var currency = Currency.Create("CZK", "Kc", "Czech Koruna", 1m);
                currency.Created("system", DateTimeOffset.UtcNow);
                context.Currencies.Add(currency);
                foreach (var payConfig in new[]
                         {
                             EmployeePayConfig.CreateForPackage(package1.Id, 250m, currency.Id),
                             EmployeePayConfig.CreateForPackage(package2.Id, 250m, currency.Id)
                         })
                {
                    payConfig.Created("system", DateTimeOffset.UtcNow);
                    context.EmployeePayConfigs.Add(payConfig);
                }
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var request = new GetPackageOverview.Request();
                return await mediator.Send(request);
            },
            assert: async (context, result) =>
            {
                var packages = await context.Packages.ToListAsync();
                Assert.Equal(packages.Count, result.Count());
                foreach (var package in packages)
                {
                    var dto = result.FirstOrDefault(c => c.Id == package.Id);
                    Assert.NotNull(dto);
                    Assert.Equal(package.Name, dto.Name);
                }
            }
        );
    }
}