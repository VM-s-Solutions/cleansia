using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Tenancy;

/// <summary>
/// ADR-0061 D5.1 — one identity per email across the holding. The DDL half: <c>IX_Users_Email</c> is
/// unique with no tenant term, so a second row with the same address in ANOTHER operating company is a
/// 23505. The register half: the pre-check refuses it as a 400 before the flush ever reaches the index
/// (TC-TEN-EMAIL), and an unconfirmed row is refreshed only from its own market.
/// </summary>
[Collection("PostgresCollection")]
public sealed class UsersEmailGloballyUniqueTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Email = "one-identity@cleansia.test";

    [Fact]
    public async Task The_Email_Index_Is_Global_And_The_Per_Tenant_One_Is_Gone()
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """SELECT indexname, indexdef FROM pg_indexes WHERE tablename = 'Users' AND indexname IN ('IX_Users_Email', 'IX_Users_TenantId_Email')""",
            conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var indexes = new Dictionary<string, string>();
        while (await reader.ReadAsync())
        {
            indexes[reader.GetString(0)] = reader.GetString(1);
        }

        var global = Assert.Single(indexes);
        Assert.Equal("IX_Users_Email", global.Key);
        Assert.Contains("UNIQUE", global.Value);
        Assert.DoesNotContain("TenantId", global.Value);
    }

    [Fact]
    public async Task A_Second_Row_With_The_Same_Email_In_Another_Company_Is_A_Unique_Violation()
    {
        await TestMethod(
            arrange: async context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                var first = User.CreateWithPassword(Email, "Passw0rd!", "First", "Holder");
                first.TenantId = TestTenants.Default;
                context.Users.Add(first);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var ctx = provider.GetRequiredService<CleansiaDbContext>();
                var second = User.CreateWithPassword(Email.ToUpperInvariant(), "Passw0rd!", "Second", "Holder");
                second.TenantId = TestTenants.Second;
                ctx.Users.Add(second);
                return await Assert.ThrowsAsync<DbUpdateException>(() => ctx.CommitAsync(CancellationToken.None));
            },
            assert: async (context, exception) =>
            {
                var postgres = Assert.IsType<PostgresException>(exception.InnerException);
                Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
                Assert.Equal("IX_Users_Email", postgres.ConstraintName);
                Assert.Equal(1, await context.Users.IgnoreQueryFilters().CountAsync());
            },
            transactional: false);
    }

    /// <summary>TC-TEN-EMAIL: refused by the validator, so no flush and no 23505 in the log.</summary>
    [Fact]
    public async Task Registering_An_Email_Held_By_A_Confirmed_User_Of_Another_Company_Is_Refused_Before_The_Flush()
    {
        await TestMethod(
            setup: MockEmail,
            arrange: async context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                var held = User.CreateWithPassword(Email, "Passw0rd!", "Slovak", "Holder");
                held.ConfirmEmail();
                held.TenantId = TestTenants.Second;
                context.Users.Add(held);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider => await provider.GetRequiredService<IMediator>().Send(Registration()),
            assert: async (context, result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                var error = Assert.Single(validation.Errors, e => e.Message == BusinessErrorMessage.ExistingUserWithEmail);
                Assert.Equal(nameof(Register.Command.Email), error.Code);
                var rows = await context.Users.IgnoreQueryFilters().Where(u => u.Email == Email).ToListAsync();
                Assert.Equal(TestTenants.Second, Assert.Single(rows).TenantId);
            },
            transactional: false);
    }

    [Fact]
    public async Task An_Unconfirmed_Row_Is_Refreshed_From_Its_Own_Market_And_Refused_From_Another()
    {
        string? codeBefore = null;
        await TestMethod(
            setup: MockEmail,
            arrange: async context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                var unconfirmed = User.CreateWithPassword(Email, "Passw0rd!", "Slovak", "Visitor");
                unconfirmed.TenantId = TestTenants.Second;
                codeBefore = unconfirmed.ConfirmationCode;
                context.Users.Add(unconfirmed);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var fromOtherMarket = await mediator.Send(Registration());

                provider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Second);
                var fromOwnMarket = await mediator.Send(Registration());
                return (fromOtherMarket, fromOwnMarket);
            },
            assert: async (context, results) =>
            {
                Assert.False(results.fromOtherMarket.IsSuccess);
                Assert.Contains(
                    Assert.IsAssignableFrom<IValidationResult>(results.fromOtherMarket).Errors,
                    e => e.Message == BusinessErrorMessage.ExistingUserWithEmail);
                Assert.True(results.fromOwnMarket.IsSuccess, results.fromOwnMarket.Error?.Message);

                var row = Assert.Single(await context.Users.IgnoreQueryFilters().Where(u => u.Email == Email).ToListAsync());
                Assert.Equal(TestTenants.Second, row.TenantId);
                Assert.NotEqual(codeBefore, row.ConfirmationCode);
            },
            transactional: false);
    }

    private static Register.Command Registration() => new(Email, TestUtilities.Constants.TestUserSession.TestUserPassword, "New", "Visitor", "en");

    private static Task MockEmail(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IEmailService>(_ => new Mock<IEmailService>().Object));
        return Task.CompletedTask;
    }
}
