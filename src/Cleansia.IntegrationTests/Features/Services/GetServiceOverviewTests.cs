using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.Domain.Services;
using Cleansia.TestUtilities.MockDataFactories.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Services;

[Collection("PostgresCollection")]
public class GetServiceOverviewTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task ShouldRetrieveAllServicesSuccessfully()
    {
        await TestMethod(
            arrange: async context =>
            {
                // Services FK to ServiceCategories — seed the parent category the factory references
                // (categoryId defaults to "test-category-id") before inserting Services, or the insert
                // violates FK_Services_ServiceCategories_CategoryId.
                var category = ServiceCategory.Create("test-category", "Test Category", "Seeded for FK");
                category.Id = "test-category-id";
                context.Set<ServiceCategory>().Add(category);
                await context.CommitAsync(CancellationToken.None);

                var service1 = ServiceMockFactory.Generate();
                var service2 = ServiceMockFactory.Generate(new ServiceMockFactory.ServicePartial { Name = "Name2", Description = "Description2" });
                context.Services.AddRange(service1, service2);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var request = new GetServiceOverview.Request();
                return await mediator.Send(request);
            },
            assert: async (context, result) =>
            {
                var services = await context.Services.ToListAsync();
                Assert.Equal(services.Count, result.Count());
                foreach (var service in services)
                {
                    var dto = result.FirstOrDefault(c => c.Id == service.Id);
                    Assert.NotNull(dto);
                    Assert.Equal(service.Name, dto.Name);
                }
            }
        );
    }
}