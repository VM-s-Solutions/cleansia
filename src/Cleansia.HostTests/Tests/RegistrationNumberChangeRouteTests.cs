using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;
using Cleansia.HostTests.Infrastructure;
using Cleansia.Infra.Services.BusinessRegistry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Owner ruling 2026-10-05 through the real hosts: a company ID (IČO) that replaces an approved cleaner's
/// stored one passes approval's full register check, on the cleaner's own saves (Partner web, Partner
/// Mobile) and on an administrator's edit. A refusal writes nothing.
/// </summary>
public sealed class RegistrationNumberChangeRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string StoredNumber = "REG-123456";
    private const string SeededFirstName = "Emp";
    private const string SeededPassportId = "P1234567";
    private const string NewNumber = "27082440";

    private static readonly BusinessRegistryRecord Ceased =
        new(BusinessRegistryAnswer.Registered, Ceased: true, TradeLicenceActive: true);

    private static readonly BusinessRegistryRecord NoTradeLicence =
        new(BusinessRegistryAnswer.Registered, Ceased: false, TradeLicenceActive: false);

    private readonly AnsweringRegistry _registry = new();

    protected override void ConfigurePartnerHostServices(IServiceCollection services) => UseRegistry(services);

    protected override void ConfigureMobileHostServices(IServiceCollection services) => UseRegistry(services);

    protected override void ConfigureAdminHostServices(IServiceCollection services) => UseRegistry(services);

    private void UseRegistry(IServiceCollection services)
    {
        services.RemoveAll<IBusinessRegistry>();
        services.AddSingleton<IBusinessRegistry>(_registry);
    }

    private sealed record Seeded(string UserId, string EmployeeId, string Email);

    private async Task<Seeded> SeedApprovedCleanerAsync(string email)
    {
        var userId = "";
        var employeeId = "";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var user = DomainSeed.EmployeeUser(email);
            var employee = DomainSeed.ApprovedEmployee(user);
            ctx.Users.Add(user);
            ctx.Employees.Add(employee);
            userId = user.Id;
            employeeId = employee.Id;
        });
        return new Seeded(userId, employeeId, email);
    }

    private async Task<string> SeedAdminTokenAsync(string email)
    {
        var adminId = "";
        await SeedAsync(ctx =>
        {
            var admin = DomainSeed.Admin(email);
            ctx.Users.Add(admin);
            adminId = admin.Id;
            return Task.CompletedTask;
        });
        return TestJwtFactory.Mint(AdminAudience, adminId, email, UserProfile.Administrator);
    }

    private static string CleanerToken(Seeded cleaner, string audience) =>
        TestJwtFactory.Mint(audience, cleaner.UserId, cleaner.Email, UserProfile.Employee, cleaner.EmployeeId);

    private Task<Employee> ReadEmployeeAsync(string employeeId) => QueryAsync(ctx => ctx.Employees
        .IgnoreQueryFilters()
        .Include(e => e.User)
        .AsNoTracking()
        .FirstAsync(e => e.Id == employeeId));

    private static object IdentificationBody(string registrationNumber) => new
    {
        nationalityId = DomainSeed.CountryId,
        passportId = "SELFPASS9",
        entityType = (int)EmployeeEntityType.NaturalPerson,
        businessCountryId = DomainSeed.CountryId,
        registrationNumber,
        legalEntityName = (string?)null,
    };

    private static object FullProfileBody(string registrationNumber) => new
    {
        firstName = "Selfwritten",
        lastName = "Loyee",
        birthDate = "1990-01-01",
        street = "Test St 1",
        city = "Prague",
        zipCode = "11000",
        countryId = DomainSeed.CountryId,
        state = (string?)null,
        nationalityId = DomainSeed.CountryId,
        phone = "+420777111222",
        passportId = SeededPassportId,
        entityType = (int)EmployeeEntityType.NaturalPerson,
        registrationNumber,
        legalEntityName = (string?)null,
        emergencyName = "ICE",
        emergencyPhone = "+420777000000",
        consent = true,
    };

    private static object AdminEditBody(string employeeId, string registrationNumber) => new
    {
        employeeId,
        firstName = "Edited",
        lastName = "Loyee",
        phone = "+420777111222",
        street = "Test St 1",
        city = "Prague",
        zipCode = "11000",
        countryId = DomainSeed.CountryId,
        nationalityId = DomainSeed.CountryId,
        passportId = SeededPassportId,
        entityType = (int)EmployeeEntityType.NaturalPerson,
        registrationNumber,
        emergencyName = "ICE",
        emergencyPhone = "+420777000000",
    };

    public static TheoryData<string, string> ApprovalOnlyRefusals => new()
    {
        { nameof(Ceased), BusinessErrorMessage.EmployeeBusinessCeased },
        { nameof(NoTradeLicence), BusinessErrorMessage.EmployeeTradeLicenceInactive },
        { nameof(BusinessRegistryRecord.Unavailable), BusinessErrorMessage.EmployeeBusinessRegistryUnavailable },
    };

    private static BusinessRegistryRecord Answer(string name) => name switch
    {
        nameof(Ceased) => Ceased,
        nameof(NoTradeLicence) => NoTradeLicence,
        nameof(BusinessRegistryRecord.Unavailable) => BusinessRegistryRecord.Unavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [MemberData(nameof(ApprovalOnlyRefusals))]
    public async Task An_approved_cleaner_changing_their_ico_on_the_mobile_host_is_refused_and_nothing_is_written(
        string answer, string refusal)
    {
        var cleaner = await SeedApprovedCleanerAsync($"ico-mobile-{answer}@hosttests.local");
        _registry.Answer = Answer(answer);

        var response = await MobileClient(CleanerToken(cleaner, MobileAudience))
            .PutAsJsonAsync("/api/Employee/UpdateIdentificationInfo", IdentificationBody(NewNumber));

        await HttpAssert.RejectedAsync(response, refusal);
        var employee = await ReadEmployeeAsync(cleaner.EmployeeId);
        Assert.Equal(StoredNumber, employee.RegistrationNumber);
        Assert.Equal(SeededPassportId, employee.PassportId);
    }

    [Fact]
    public async Task An_approved_cleaner_resaving_their_stored_ico_is_not_held_to_the_approval_check()
    {
        var cleaner = await SeedApprovedCleanerAsync("ico-mobile-same@hosttests.local");
        _registry.Answer = Ceased;

        var response = await MobileClient(CleanerToken(cleaner, MobileAudience))
            .PutAsJsonAsync("/api/Employee/UpdateIdentificationInfo", IdentificationBody(StoredNumber));

        HttpAssert.IsOk(response);
        Assert.Equal("SELFPASS9", (await ReadEmployeeAsync(cleaner.EmployeeId)).PassportId);
    }

    [Fact]
    public async Task The_partner_web_profile_save_with_a_changed_ico_is_refused_whole_while_the_register_does_not_answer()
    {
        var cleaner = await SeedApprovedCleanerAsync("ico-web@hosttests.local");
        _registry.Answer = BusinessRegistryRecord.Unavailable;

        var response = await PartnerClient(CleanerToken(cleaner, PartnerAudience))
            .PutAsJsonAsync("/api/Employee/UpdateEmployee", FullProfileBody(NewNumber));

        await HttpAssert.RejectedAsync(response, BusinessErrorMessage.EmployeeBusinessRegistryUnavailable);
        var employee = await ReadEmployeeAsync(cleaner.EmployeeId);
        Assert.Equal(StoredNumber, employee.RegistrationNumber);
        Assert.Equal(SeededFirstName, employee.User!.FirstName);
    }

    [Fact]
    public async Task An_admin_changing_an_approved_cleaners_ico_to_an_ended_business_is_refused()
    {
        var cleaner = await SeedApprovedCleanerAsync("ico-admin-ceased@hosttests.local");
        var admin = AdminClient(await SeedAdminTokenAsync("ico-admin-ceased-admin@hosttests.local"));
        _registry.Answer = Ceased;

        var response = await admin.PutAsJsonAsync(
            $"/api/AdminEmployee/{cleaner.EmployeeId}/update", AdminEditBody(cleaner.EmployeeId, NewNumber));

        await HttpAssert.RejectedAsync(response, BusinessErrorMessage.EmployeeBusinessCeased);
        var employee = await ReadEmployeeAsync(cleaner.EmployeeId);
        Assert.Equal(StoredNumber, employee.RegistrationNumber);
        Assert.Equal(SeededFirstName, employee.User!.FirstName);
    }

    [Fact]
    public async Task An_admin_edit_resending_the_stored_ico_is_saved_without_asking_the_register()
    {
        var cleaner = await SeedApprovedCleanerAsync("ico-admin-same@hosttests.local");
        var admin = AdminClient(await SeedAdminTokenAsync("ico-admin-same-admin@hosttests.local"));
        _registry.Answer = Ceased;

        var response = await admin.PutAsJsonAsync(
            $"/api/AdminEmployee/{cleaner.EmployeeId}/update", AdminEditBody(cleaner.EmployeeId, StoredNumber));

        HttpAssert.IsOk(response);
        Assert.Equal("Edited", (await ReadEmployeeAsync(cleaner.EmployeeId)).User!.FirstName);
        Assert.Equal(0, _registry.Lookups);
    }

    [Fact]
    public async Task An_admin_cannot_clear_an_ico_the_country_requires()
    {
        var cleaner = await SeedApprovedCleanerAsync("ico-admin-clear@hosttests.local");
        var admin = AdminClient(await SeedAdminTokenAsync("ico-admin-clear-admin@hosttests.local"));

        var response = await admin.PutAsJsonAsync(
            $"/api/AdminEmployee/{cleaner.EmployeeId}/update", AdminEditBody(cleaner.EmployeeId, ""));

        await HttpAssert.RejectedAsync(response, BusinessErrorMessage.RegistrationNumberInvalidFormat);
        Assert.Equal(StoredNumber, (await ReadEmployeeAsync(cleaner.EmployeeId)).RegistrationNumber);
    }

    [Fact]
    public async Task An_admin_change_the_register_does_not_refuse_is_written()
    {
        var cleaner = await SeedApprovedCleanerAsync("ico-admin-new@hosttests.local");
        var admin = AdminClient(await SeedAdminTokenAsync("ico-admin-new-admin@hosttests.local"));
        _registry.Answer = BusinessRegistryRecord.NotConsulted;

        var response = await admin.PutAsJsonAsync(
            $"/api/AdminEmployee/{cleaner.EmployeeId}/update", AdminEditBody(cleaner.EmployeeId, NewNumber));

        HttpAssert.IsOk(response);
        Assert.Equal(NewNumber, (await ReadEmployeeAsync(cleaner.EmployeeId)).RegistrationNumber);
    }

    private sealed class AnsweringRegistry : IBusinessRegistry
    {
        private int _lookups;

        public BusinessRegistryRecord Answer { get; set; } = BusinessRegistryRecord.NotConsulted;

        public int Lookups => _lookups;

        public Task<BusinessRegistryRecord> LookupAsync(
            string countryIsoCode, string registrationNumber, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _lookups);
            return Task.FromResult(Answer);
        }
    }
}
