using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Cleansia.IntegrationTests.Deploy;

[Collection("PostgresCollection")]
public sealed class PromoCodeEmailTranslationScriptTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private static readonly string[] Keys =
    [
        "ButtonText", "Closing", "DiscountText", "ExpiryNotice", "FooterText", "Greeting", "IgnoreText",
        "InstructionsText", "IntroText", "Subject", "SupportText", "TeamName",
    ];

    [Fact]
    public async Task The_actual_script_inserts_sixty_translations_with_null_optional_audit_and_is_idempotent()
    {
        await TestMethod(
            arrange: async (CleansiaDbContext _) => await RunScriptAsync("prod-bootstrap.sql"),
            act: async provider =>
            {
                var context = provider.GetRequiredService<CleansiaDbContext>();
                var rows = context.EmailTemplateTranslations.AsNoTracking()
                    .Where(row => row.EmailType == EmailType.PromoCode)
                    .OrderBy(row => row.Id)
                    .Select(row => new
                    {
                        row.Id, Language = row.Language!.Code, row.Key, row.Value, row.IsActive,
                        row.CreatedBy, row.CreatedOn, row.UpdatedBy, row.UpdatedOn, row.DeactivatedBy, row.DeactivatedOn,
                    });

                await RunScriptAsync(Path.Combine("seed", "insert_email_template_translations_promo_code.sql"));
                var first = await rows.ToListAsync();
                await RunScriptAsync(Path.Combine("seed", "insert_email_template_translations_promo_code.sql"));
                return (First: first, Second: await rows.ToListAsync());
            },
            assert: (context, runs) =>
            {
                Assert.Equal(60, runs.First.Count);
                Assert.Equal(["cs", "en", "ru", "sk", "uk"], runs.First.Select(row => row.Language).Distinct().Order());
                foreach (var language in runs.First.GroupBy(row => row.Language))
                {
                    Assert.Equal(Keys, language.Select(row => row.Key).Order());
                }

                Assert.All(runs.First, row =>
                {
                    Assert.True(row.IsActive);
                    Assert.False(string.IsNullOrWhiteSpace(row.Value));
                    Assert.Equal("System", row.CreatedBy);
                    Assert.NotEqual(default, row.CreatedOn);
                    Assert.Null(row.UpdatedBy);
                    Assert.Null(row.UpdatedOn);
                    Assert.Null(row.DeactivatedBy);
                    Assert.Null(row.DeactivatedOn);
                });
                Assert.Equal(runs.First, runs.Second);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    private async Task RunScriptAsync(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && directory.GetFiles("*.sln").Length == 0)
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "Could not locate the solution directory from the test base directory.");
        var script = File.ReadAllText(Path.GetFullPath(Path.Combine(directory!.FullName, "..", "sql-scripts", relativePath)));
        await using var connection = new NpgsqlConnection(Fixture.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(script, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }
}
