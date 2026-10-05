using System.Security.Claims;
using Cleansia.Core.AppServices.Common;
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
/// Owner ruling 2026-10-04 through the real <c>ApproveEmployee</c> pipeline on real Postgres: the business
/// register refuses an approval and nothing is approved. The register is reached through the composition
/// the hosts use, and the test configuration switches ARES off the way local development does: with it
/// off, a company ID the live register does not hold (12345678) is approved, so this suite never calls
/// ares.gov.cz.
/// </summary>
[Collection("PostgresCollection")]
public class ApproveEmployeeBusinessRegisterTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string AdminId = "admin-business-register";
    private const string AdminEmail = "admin-business-register@cleansia.test";
    private const string EmployeeId = "emp-business-register";
    private const string CountryId = "country-cz-bizreg";
    private const string CurrencyId = "currency-czk-bizreg";
    private const string UnknownToAres = "12345678";

    [Fact]
    public async Task A_Business_The_Register_Says_Has_Ended_Is_Not_Approved()
    {
        await TestMethod(
            setup: services =>
            {
                AdminSession(services);
                services.Replace(ServiceDescriptor.Scoped<IBusinessRegistry>(_ => new AnsweringRegistry(
                    new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: true, TradeLicenceActive: true))));
                return Task.CompletedTask;
            },
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new ApproveEmployee.Command(EmployeeId, CountryId)),
            assert: async (CleansiaDbContext context, BusinessResult<ApproveEmployee.Response> result) =>
            {
                Assert.True(result.IsFailure);
                var refusal = Assert.Single(Assert.IsAssignableFrom<IValidationResult>(result).Errors);
                Assert.Equal(BusinessErrorMessage.EmployeeBusinessCeased, refusal.Message);
                Assert.Equal(nameof(ApproveEmployee.Command.EmployeeId), refusal.Code);

                var employee = await context.Employees.IgnoreQueryFilters().SingleAsync(e => e.Id == EmployeeId);
                Assert.Equal(ContractStatus.Pending, employee.ContractStatus);
                Assert.Null(employee.ApprovedAt);
            },
            transactional: false);
    }

    [Fact]
    public async Task With_Ares_Switched_Off_The_Approval_Is_Judged_Without_The_Register()
    {
        await TestMethod(
            setup: services =>
            {
                AdminSession(services);
                return Task.CompletedTask;
            },
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new ApproveEmployee.Command(EmployeeId, CountryId)),
            assert: async (CleansiaDbContext context, BusinessResult<ApproveEmployee.Response> result) =>
            {
                Assert.True(result.IsSuccess, string.Join("; ",
                    (result as IValidationResult)?.Errors.Select(e => $"{e.Code}={e.Message}") ?? [result.Error?.Message]));

                var employee = await context.Employees.IgnoreQueryFilters().SingleAsync(e => e.Id == EmployeeId);
                Assert.Equal(ContractStatus.Approved, employee.ContractStatus);
                Assert.Equal(CountryId, employee.WorkCountryId);
            },
            transactional: false);
    }

    private static void AdminSession(IServiceCollection services) =>
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            AdminId, AdminEmail, [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));

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

        var user = User.CreateWithPassword(
            "cleaner-business-register@cleansia.test", "Seed-Password-123", "Jana", "Nováková", UserProfile.Employee);
        user.Id = "user-business-register";
        user.Update("Jana", "Nováková", "+420777111222", new DateOnly(1990, 1, 1));

        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;
        employee.UpdateEmployeeDetails(
            EmployeeEntityType.NaturalPerson,
            registrationNumber: UnknownToAres,
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

    private sealed class AnsweringRegistry(BusinessRegistryRecord record) : IBusinessRegistry
    {
        public Task<BusinessRegistryRecord> LookupAsync(
            string countryIsoCode, string registrationNumber, CancellationToken cancellationToken)
            => Task.FromResult(record);
    }
}
