using Cleansia.Core.AppServices.Features.Packages;
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