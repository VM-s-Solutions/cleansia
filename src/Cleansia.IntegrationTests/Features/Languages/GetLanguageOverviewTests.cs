using Cleansia.Core.AppServices.Features.Languages;
using Cleansia.TestUtilities.MockDataFactories.Languages;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Languages;

[Collection("PostgresCollection")]
public class GetLanguageOverviewTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task ShouldRetrieveAllLanguagesSuccessfully()
    {
        await TestMethod(
            arrange: async context =>
            {
                var language1 = LanguageMockFactory.Generate();
                var language2 = LanguageMockFactory.Generate(new LanguageMockFactory.LanguagePartial { Code = "EN", Name = "English" });
                context.Languages.AddRange(language1, language2);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var request = new GetLanguageOverview.Request();
                return await mediator.Send(request);
            },
            assert: async (context, result) =>
            {
                var languages = await context.Languages.ToListAsync();
                Assert.Equal(languages.Count, result.Count());
                foreach (var language in languages)
                {
                    var dto = result.FirstOrDefault(c => c.Id == language.Id);
                    Assert.NotNull(dto);
                    Assert.Equal(language.Name, dto.Name);
                }
            }
        );
    }
}