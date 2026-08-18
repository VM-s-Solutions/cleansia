using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// ADR-0034 D1.1.2 / verification 10 — the payout row must be gone after an erasure, through
/// <c>GdprDeletionService</c>'s REAL query shape.
///
/// <para>This runs against real Postgres deliberately. The deletion aggregate is loaded with
/// <c>.Include(u =&gt; u.Employee).ThenInclude(e =&gt; e!.Address)</c> and nothing else, and this repository
/// has no lazy loading — so a clear that reached through <c>Employee.PayoutDetails</c> would find
/// <c>null</c>, do nothing, and return success while the account number, SWIFT and holder name survived
/// in plaintext. A unit test with a hand-populated navigation passes green over exactly that bug.</para>
/// </summary>
[Collection("PostgresCollection")]
public class PayoutDetailsErasureTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string AccountNumber = "5885638003";
    private const string Iban = "CZ3155000000005885638003";

    /// <remarks>
    /// Calls <c>IGdprDeletionService</c> directly rather than sending <c>DeleteUserAccount.Command</c>,
    /// which is what it used to do. Per ADR-0052 a cleaner's own deletion now FILES a request and
    /// erases nothing, so the self path no longer reaches the code this test exists to cover.
    ///
    /// <para>The admin COMMAND is not the substitute either: its validator refuses a self-delete, and
    /// the integration session is the subject. Going straight at the service with
    /// <c>deferEmployeeErasure: false</c> is both simpler and closer to the stated purpose — this test
    /// is about the query shape <c>GdprDeletionService</c> loads the aggregate with, not about the
    /// pipeline that reaches it, and that shape is shared by both paths.</para>
    ///
    /// <para>The commit is explicit because the UnitOfWork behaviour is what used to provide it, and
    /// it only runs for a MediatR request.</para>
    /// </remarks>
    [Fact]
    public async Task Erasing_A_Cleaner_Deletes_The_Payout_Row_Regardless_Of_What_The_Caller_Loaded()
    {
        await TestMethod(
            arrange: SeedCleanerWithPayoutDetails,
            act: async provider =>
            {
                var service = provider.GetRequiredService<IGdprDeletionService>();
                var context = provider.GetRequiredService<CleansiaDbContext>();

                var result = await service.DeleteUserAccountAsync(
                    TestConstants.TestUserSession.TestUserId,
                    GdprAuditReasons.AdminDeletion,
                    _ => ("integration-admin", null),
                    deferEmployeeErasure: false,
                    CancellationToken.None);

                await context.CommitAsync(CancellationToken.None);
                return result;
            },
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess);

                var rows = await context.EmployeePayoutDetails.IgnoreQueryFilters().ToListAsync();
                Assert.Empty(rows);

                var employee = await context.Employees
                    .IgnoreQueryFilters()
                    .FirstAsync(e => e.UserId == TestConstants.TestUserSession.TestUserId);
                Assert.False(employee.HasPayoutDetails);
            });
    }

    /// <summary>
    /// ADR-0034 D1.1 verification 6(b) — the denormalized flag's one invariant, asserted across the whole
    /// table after a real write through the pipeline. A second place the truth can live is the cost this
    /// design accepted; this is what keeps it honest.
    /// </summary>
    [Fact]
    public async Task After_The_Write_Path_Runs_The_Gate_Flag_Agrees_With_Row_Existence_Table_Wide()
    {
        await TestMethod(
            arrange: SeedCleanerWithoutPayoutDetails,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var employeeId = await EmployeeIdAsync(provider);

                return await mediator.Send(new UpdateBankDetails.Command(
                    EmployeeId: employeeId,
                    Iban: null,
                    BankCountryId: await CzCountryIdAsync(provider),
                    AccountNumber: AccountNumber,
                    BankCode: "5500"));
            },
            assert: async (CleansiaDbContext context, BusinessResult<UpdateBankDetails.Response> result) =>
            {
                Assert.True(result.IsSuccess);

                var stored = await context.EmployeePayoutDetails.IgnoreQueryFilters().SingleAsync();
                Assert.Equal(Iban, stored.Iban);
                Assert.Equal(PayoutScheme.CzskDomesticWithIban, stored.Scheme);
                Assert.Equal(PayoutDetailsStatus.Provided, stored.Status);

                var disagreeing = await context.Employees
                    .IgnoreQueryFilters()
                    .Where(e => e.HasPayoutDetails !=
                                context.EmployeePayoutDetails.IgnoreQueryFilters().Any(p => p.EmployeeId == e.Id))
                    .Select(e => e.Id)
                    .ToListAsync();

                Assert.Empty(disagreeing);
            });
    }

    private static async Task<string> EmployeeIdAsync(IServiceProvider provider)
    {
        var context = provider.GetRequiredService<CleansiaDbContext>();
        return await context.Employees.IgnoreQueryFilters()
            .Where(e => e.UserId == TestConstants.TestUserSession.TestUserId)
            .Select(e => e.Id)
            .SingleAsync();
    }

    private static async Task<string> CzCountryIdAsync(IServiceProvider provider)
    {
        var context = provider.GetRequiredService<CleansiaDbContext>();
        return await context.Countries.IgnoreQueryFilters().Select(c => c.Id).FirstAsync();
    }

    private static async Task SeedCleanerWithoutPayoutDetails(CleansiaDbContext context)
    {
        await SeedCleanerWithPayoutDetails(context);

        var payout = await context.EmployeePayoutDetails.IgnoreQueryFilters().SingleAsync();
        context.EmployeePayoutDetails.Remove(payout);

        var employee = await context.Employees.IgnoreQueryFilters().SingleAsync();
        employee.ClearPayoutDetails();

        var country = await context.Countries.IgnoreQueryFilters().SingleAsync();
        var configuration = CountryConfiguration.Create(country.Id, "CZK", "cs", 0.21m);
        typeof(CountryConfiguration)
            .GetProperty(nameof(CountryConfiguration.PayoutScheme))!
            .SetValue(configuration, PayoutScheme.CzskDomesticWithIban);
        context.CountryConfigurations.Add(configuration);

        await context.CommitAsync(CancellationToken.None);
    }

    private static async Task SeedCleanerWithPayoutDetails(CleansiaDbContext context)
    {
        if (!await context.Languages.AnyAsync())
        {
            context.Languages.Add(Language.Create("en", "English"));
            await context.SaveChangesAsync();
        }

        var user = User.CreateWithPassword(
            email: TestConstants.TestUserSession.TestUserEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName);
        user.Id = TestConstants.TestUserSession.TestUserId;
        user.ConfirmEmail();
        context.Users.Add(user);
        await context.CommitAsync(CancellationToken.None);

        var country = Country.Create("Czechia", "CZ");
        context.Countries.Add(country);
        await context.CommitAsync(CancellationToken.None);

        var employee = Employee.CreateWithUser(user);
        employee.MarkPayoutDetailsProvided();
        context.Employees.Add(employee);
        await context.CommitAsync(CancellationToken.None);

        context.EmployeePayoutDetails.Add(EmployeePayoutDetails.Create(
            employee.Id,
            PayoutScheme.CzskDomesticWithIban,
            country.Id,
            PayoutDetailsStatus.Provided,
            accountPrefix: null,
            accountNumber: AccountNumber,
            bankCode: "5500",
            iban: Iban,
            swift: "RZBCCZPP",
            bankName: "Raiffeisenbank",
            holderName: "Milada Novotna",
            confirmedAt: DateTime.UtcNow));

        await context.CommitAsync(CancellationToken.None);
    }
}
