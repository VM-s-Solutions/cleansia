using System.Security.Claims;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Services.BusinessRegistry;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Employees;

/// <summary>
/// A cleaner's save and an administrator's approval racing on real Postgres. The register is asked while
/// the other write commits on its own connection, so whichever commits second was judged against a row
/// that no longer holds: it is refused at commit, and nothing is left approved on a number that was never
/// checked at approval grade.
/// </summary>
[Collection("PostgresCollection")]
public class ApproveEmployeeRegisterRaceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string AdminId = "admin-register-race";
    private const string AdminEmail = "admin-register-race@cleansia.test";
    private const string CleanerUserId = "user-register-race";
    private const string CleanerEmail = "cleaner-register-race@cleansia.test";
    private const string EmployeeId = "emp-register-race";
    private const string CountryId = "country-cz-register-race";
    private const string CurrencyId = "currency-czk-register-race";
    private const string CheckedIco = "27082440";
    private const string ChangedIco = "25596641";

    [Fact]
    public async Task An_Ico_Changed_While_The_Approval_Checked_It_Is_Not_Approved()
    {
        var registry = new RacingRegistry(() => WriteOnItsOwnConnectionAsync(
            CleanerUserId, e => e.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, ChangedIco, null)));

        await TestMethod(
            setup: services =>
            {
                Session(services, AdminId, AdminEmail, UserProfile.Administrator);
                services.Replace(ServiceDescriptor.Scoped<IBusinessRegistry>(_ => registry));
                return Task.CompletedTask;
            },
            arrange: Seed,
            act: async provider => await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                provider.GetRequiredService<IMediator>().Send(new ApproveEmployee.Command(EmployeeId, CountryId))),
            assert: async (CleansiaDbContext context, DbUpdateConcurrencyException _) =>
            {
                Assert.Equal([CheckedIco], registry.Asked);
                var employee = await context.Employees.IgnoreQueryFilters().AsNoTracking().SingleAsync(e => e.Id == EmployeeId);
                Assert.Equal(ContractStatus.Pending, employee.ContractStatus);
                Assert.Null(employee.ApprovedAt);
                Assert.Equal(ChangedIco, employee.RegistrationNumber);
            },
            transactional: false);
    }

    [Fact]
    public async Task An_Approval_Committed_While_A_Pending_Save_Checked_Refuses_The_Save()
    {
        var registry = new RacingRegistry(() => WriteOnItsOwnConnectionAsync(
            AdminId, e => e.AssignWorkCountry(CountryId).Approve(AdminId)));

        await TestMethod(
            setup: services =>
            {
                Session(services, CleanerUserId, CleanerEmail, UserProfile.Employee);
                services.Replace(ServiceDescriptor.Scoped<IBusinessRegistry>(_ => registry));
                return Task.CompletedTask;
            },
            arrange: Seed,
            act: async provider => await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                provider.GetRequiredService<IMediator>().Send(new UpdateIdentificationInfo.Command(
                    EmployeeId: null,
                    NationalityId: CountryId,
                    PassportId: "AB1234567",
                    EntityType: EmployeeEntityType.NaturalPerson,
                    BusinessCountryId: CountryId,
                    RegistrationNumber: ChangedIco,
                    LegalEntityName: null))),
            assert: async (CleansiaDbContext context, DbUpdateConcurrencyException _) =>
            {
                Assert.Equal([ChangedIco], registry.Asked);
                var employee = await context.Employees.IgnoreQueryFilters().AsNoTracking().SingleAsync(e => e.Id == EmployeeId);
                Assert.Equal(ContractStatus.Approved, employee.ContractStatus);
                Assert.Equal(CheckedIco, employee.RegistrationNumber);
            },
            transactional: false);
    }

    private async Task WriteOnItsOwnConnectionAsync(string actorId, Action<Employee> write)
    {
        await using var context = new CleansiaDbContext(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(Fixture.GetConnectionString()).Options,
            new TestUserSessionProvider(actorId, $"{actorId}@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));
        write(await context.Employees.SingleAsync(e => e.Id == EmployeeId));
        await context.CommitAsync(CancellationToken.None);
    }

    private static void Session(IServiceCollection services, string userId, string email, UserProfile profile) =>
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            userId, email, [new Claim(ClaimTypes.Role, profile.ToString())])));

    private static async Task Seed(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        context.CountryConfigurations.Add(
            CountryConfiguration.Create(CountryId, "CZK", "cs", 0.21m).AssignOperator(TestTenants.Default));

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var admin = User.CreateWithPassword(
            AdminEmail, "Seed-Password-123", "Ad", "Min", UserProfile.Administrator, adminRole: AdminRole.Administrator);
        admin.Id = AdminId;
        admin.TenantId = TestTenants.Default;
        admin.ConfirmEmail();
        context.Users.Add(admin);

        var user = User.CreateWithPassword(CleanerEmail, "Seed-Password-123", "Jana", "Nováková", UserProfile.Employee);
        user.Id = CleanerUserId;
        user.Update("Jana", "Nováková", "+420777111222", new DateOnly(1990, 1, 1));

        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;
        employee.UpdateEmployeeDetails(
            EmployeeEntityType.NaturalPerson,
            registrationNumber: CheckedIco,
            legalEntityName: null,
            nationalityId: CountryId,
            passportId: "AB1234567",
            address: Address.Create("Hlavni 1", "Praha", "11000", CountryId),
            emergencyContactName: null,
            emergencyContactPhone: null);
        employee.UpdateBankDetails("CZ6508000000192000145399");
        context.Employees.Add(employee);

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    /// <summary>
    /// A register that, the first time it is asked, lets the competing write commit before it answers that
    /// the business is registered, in force and licensed.
    /// </summary>
    private sealed class RacingRegistry(Func<Task> race) : IBusinessRegistry
    {
        private Func<Task>? _race = race;

        public List<string> Asked { get; } = [];

        public async Task<BusinessRegistryRecord> LookupAsync(
            string countryIsoCode, string registrationNumber, CancellationToken cancellationToken)
        {
            Asked.Add(registrationNumber);
            if (Interlocked.Exchange(ref _race, null) is { } raceNow)
            {
                await raceNow();
            }

            return new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: false, TradeLicenceActive: true);
        }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
