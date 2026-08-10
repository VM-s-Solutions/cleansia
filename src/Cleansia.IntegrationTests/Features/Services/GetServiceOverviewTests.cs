using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Internationalization;
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

                // The overview offers an entry only when it is quotable, so each seeded service needs
                // its platform-wide pay config or the wizard withholds it and this asserts nothing.
                var currency = Currency.Create("CZK", "Kc", "Czech Koruna", 1m);
                currency.Created("system", DateTimeOffset.UtcNow);
                context.Currencies.Add(currency);
                foreach (var payConfig in new[]
                         {
                             EmployeePayConfig.CreateForService(service1.Id, 500m, currency.Id),
                             EmployeePayConfig.CreateForService(service2.Id, 500m, currency.Id)
                         })
                {
                    payConfig.Created("system", DateTimeOffset.UtcNow);
                    context.EmployeePayConfigs.Add(payConfig);
                }
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