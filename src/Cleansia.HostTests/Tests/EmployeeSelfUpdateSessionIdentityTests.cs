using System.Net.Http.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// End-to-end identity contract for the seven partner self-service employee writes: the row written is
/// ALWAYS the JWT caller's employee record, and the body's <c>employeeId</c> is inert (S1, [OWN-DATA]).
///
/// Each of these authorized by comparing the session-resolved employee to a CLIENT-supplied id, which
/// passes only for a caller that already knows the answer — so it protected nothing and made the route
/// uncallable for any client that cannot supply it. The unit tests could not see the worst of it: MVC's
/// implicit-required for a non-nullable reference member rejects an ABSENT <c>employeeId</c> with a 400
/// before the command ever reaches MediatR, and a unit test constructs the command directly.
/// </summary>
public sealed class EmployeeSelfUpdateSessionIdentityTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    public enum Route
    {
        PersonalInfo,
        AddressInfo,
        EmergencyContact,
        Availability,
        BankDetails,
        IdentificationInfo,
        FullProfile,
    }

    // Seeded baselines from DomainSeed.BuildCompleteEmployee — what an untouched victim still reads.
    private const string SeededFirstName = "Emp";
    private const string SeededStreet = "Test St 1";
    private const string SeededEmergencyName = "ICE";
    private const string SeededPassportId = "P1234567";
    private const string SeededIban = "CZ6508000000192000145399";

    private const string WrittenFirstName = "Selfwritten";
    private const string WrittenStreet = "Selfwritten Street 42";
    private const string WrittenEmergencyName = "Selfwritten ICE";
    private const string WrittenAvailabilityDay = "2026-09-01";
    private const string WrittenPassportId = "SELFPASS9";
    private const string WrittenIban = "CZ3155000000005885638003";

    private sealed record Seeded(string UserId, string EmployeeId, string Email);

    private async Task<Seeded> SeedCleanerAsync(string email)
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

    private string TokenFor(Seeded cleaner, string audience) =>
        TestJwtFactory.Mint(audience, cleaner.UserId, cleaner.Email, UserProfile.Employee, cleaner.EmployeeId);

    private static string Path(Route route) => route switch
    {
        Route.PersonalInfo => "/api/Employee/UpdatePersonalInfo",
        Route.AddressInfo => "/api/Employee/UpdateAddressInfo",
        Route.EmergencyContact => "/api/Employee/UpdateEmergencyContact",
        Route.Availability => "/api/Employee/UpdateAvailability",
        Route.BankDetails => "/api/Employee/UpdateBankDetails",
        Route.IdentificationInfo => "/api/Employee/UpdateIdentificationInfo",
        Route.FullProfile => "/api/Employee/UpdateEmployee",
        _ => throw new ArgumentOutOfRangeException(nameof(route)),
    };

    private static Dictionary<string, object?> BodyFor(Route route) => route switch
    {
        Route.PersonalInfo => new()
        {
            ["firstName"] = WrittenFirstName,
            ["lastName"] = "Loyee",
            ["birthDate"] = "1990-01-01",
            ["phone"] = "+420777111222",
        },
        Route.AddressInfo => new()
        {
            ["street"] = WrittenStreet,
            ["city"] = "Prague",
            ["zipCode"] = "11000",
            ["countryId"] = DomainSeed.CountryId,
            ["state"] = null,
            // Supplied coordinates keep the server-side geocoder out of the path entirely.
            ["latitude"] = 50.0755,
            ["longitude"] = 14.4378,
        },
        Route.EmergencyContact => new()
        {
            ["emergencyName"] = WrittenEmergencyName,
            ["emergencyPhone"] = "+420777000001",
        },
        Route.Availability => new()
        {
            ["availability"] = new Dictionary<string, object?>
            {
                [WrittenAvailabilityDay] = new[] { new { start = "09:00", end = "17:00" } },
            },
        },
        Route.BankDetails => new()
        {
            ["iban"] = WrittenIban,
        },
        Route.IdentificationInfo => new()
        {
            ["nationalityId"] = DomainSeed.CountryId,
            ["passportId"] = WrittenPassportId,
            ["entityType"] = (int)EmployeeEntityType.NaturalPerson,
            ["businessCountryId"] = DomainSeed.CountryId,
            ["registrationNumber"] = "12345678",
            ["vatNumber"] = null,
            ["legalEntityName"] = null,
        },
        Route.FullProfile => new()
        {
            ["firstName"] = WrittenFirstName,
            ["lastName"] = "Loyee",
            ["birthDate"] = "1990-01-01",
            ["street"] = WrittenStreet,
            ["city"] = "Prague",
            ["zipCode"] = "11000",
            ["countryId"] = DomainSeed.CountryId,
            ["state"] = null,
            ["nationalityId"] = DomainSeed.CountryId,
            ["phone"] = "+420777111222",
            ["passportId"] = WrittenPassportId,
            ["entityType"] = (int)EmployeeEntityType.NaturalPerson,
            ["registrationNumber"] = "12345678",
            ["vatNumber"] = null,
            ["legalEntityName"] = null,
            ["emergencyName"] = WrittenEmergencyName,
            ["emergencyPhone"] = "+420777000001",
            ["consent"] = true,
            ["documents"] = null,
            ["availability"] = null,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(route)),
    };

    /// <summary>The shape a client that cannot know its employee id puts on the wire: the member is
    /// absent entirely, which is the case MVC's implicit-required rejects before MediatR is reached.</summary>
    private static HttpContent WithoutEmployeeId(Route route) => JsonContent.Create(BodyFor(route));

    private static HttpContent WithEmployeeId(Route route, string? employeeId)
    {
        var body = BodyFor(route);
        body["employeeId"] = employeeId;
        return JsonContent.Create(body);
    }

    private Task<Employee> ReadEmployeeAsync(string employeeId) => QueryAsync(ctx => ctx.Employees
        .IgnoreQueryFilters()
        .Include(e => e.User)
        .Include(e => e.Address)
        .AsNoTracking()
        .FirstAsync(e => e.Id == employeeId));

    private async Task AssertWrittenAsync(Route route, string employeeId)
    {
        var employee = await ReadEmployeeAsync(employeeId);
        switch (route)
        {
            case Route.PersonalInfo:
                Assert.Equal(WrittenFirstName, employee.User!.FirstName);
                break;
            case Route.AddressInfo:
                Assert.Equal(WrittenStreet, employee.Address!.Street);
                break;
            case Route.EmergencyContact:
                Assert.Equal(WrittenEmergencyName, employee.EmergencyContactName);
                break;
            case Route.Availability:
                Assert.Contains(WrittenAvailabilityDay, employee.Availability.Keys);
                break;
            case Route.BankDetails:
                Assert.Equal(WrittenIban, employee.IBAN);
                break;
            case Route.IdentificationInfo:
                Assert.Equal(WrittenPassportId, employee.PassportId);
                break;
            case Route.FullProfile:
                Assert.Equal(WrittenFirstName, employee.User!.FirstName);
                Assert.Equal(WrittenStreet, employee.Address!.Street);
                break;
        }
    }

    private async Task AssertUntouchedAsync(Route route, string employeeId)
    {
        var employee = await ReadEmployeeAsync(employeeId);
        switch (route)
        {
            case Route.PersonalInfo:
                Assert.Equal(SeededFirstName, employee.User!.FirstName);
                break;
            case Route.AddressInfo:
                Assert.Equal(SeededStreet, employee.Address!.Street);
                break;
            case Route.EmergencyContact:
                Assert.Equal(SeededEmergencyName, employee.EmergencyContactName);
                break;
            case Route.Availability:
                Assert.DoesNotContain(WrittenAvailabilityDay, employee.Availability.Keys);
                break;
            case Route.BankDetails:
                Assert.Equal(SeededIban, employee.IBAN);
                break;
            case Route.IdentificationInfo:
                Assert.Equal(SeededPassportId, employee.PassportId);
                break;
            case Route.FullProfile:
                Assert.Equal(SeededFirstName, employee.User!.FirstName);
                Assert.Equal(SeededStreet, employee.Address!.Street);
                break;
        }
    }

    /// <summary>
    /// The defect this ticket exists for. A body with no <c>employeeId</c> member at all must reach the
    /// handler and write the caller's own row — today MVC answers
    /// <c>400 {"errors":{"EmployeeId":["The EmployeeId field is required."]}}</c> first.
    /// </summary>
    [Theory]
    [InlineData(Route.PersonalInfo)]
    [InlineData(Route.AddressInfo)]
    [InlineData(Route.EmergencyContact)]
    [InlineData(Route.Availability)]
    [InlineData(Route.BankDetails)]
    [InlineData(Route.IdentificationInfo)]
    [InlineData(Route.FullProfile)]
    public async Task Save_with_no_employee_id_at_all_updates_the_callers_own_row(Route route)
    {
        var caller = await SeedCleanerAsync($"esu-noid-{route}@hosttests.local");

        var response = await MobileClient(TokenFor(caller, MobileAudience))
            .PutAsync(Path(route), WithoutEmployeeId(route));

        HttpAssert.IsOk(response);
        await AssertWrittenAsync(route, caller.EmployeeId);
    }

    /// <summary>The mutation proof: a body naming ANOTHER cleaner's record writes the caller's row and
    /// leaves the victim's untouched (S1/S3) — the id is ignored, not merely refused.</summary>
    [Theory]
    [InlineData(Route.PersonalInfo)]
    [InlineData(Route.AddressInfo)]
    [InlineData(Route.EmergencyContact)]
    [InlineData(Route.Availability)]
    [InlineData(Route.BankDetails)]
    [InlineData(Route.IdentificationInfo)]
    [InlineData(Route.FullProfile)]
    public async Task Save_naming_another_cleaner_updates_the_caller_and_leaves_the_victim_untouched(Route route)
    {
        var caller = await SeedCleanerAsync($"esu-caller-{route}@hosttests.local");
        var victim = await SeedCleanerAsync($"esu-victim-{route}@hosttests.local");

        var response = await MobileClient(TokenFor(caller, MobileAudience))
            .PutAsync(Path(route), WithEmployeeId(route, victim.EmployeeId));

        HttpAssert.IsOk(response);
        await AssertUntouchedAsync(route, victim.EmployeeId);
        await AssertWrittenAsync(route, caller.EmployeeId);
    }

    /// <summary>Wire compatibility: the three shipped clients all echo the id back from the profile GET,
    /// so the member must keep being accepted.</summary>
    [Theory]
    [InlineData(Route.PersonalInfo)]
    [InlineData(Route.AddressInfo)]
    [InlineData(Route.EmergencyContact)]
    [InlineData(Route.Availability)]
    [InlineData(Route.BankDetails)]
    [InlineData(Route.IdentificationInfo)]
    [InlineData(Route.FullProfile)]
    public async Task Save_carrying_the_callers_own_employee_id_still_works(Route route)
    {
        var caller = await SeedCleanerAsync($"esu-ownid-{route}@hosttests.local");

        var response = await MobileClient(TokenFor(caller, MobileAudience))
            .PutAsync(Path(route), WithEmployeeId(route, caller.EmployeeId));

        HttpAssert.IsOk(response);
        await AssertWrittenAsync(route, caller.EmployeeId);
    }

    /// <summary>The Android client sends a blank id when its cached profile has not loaded — today that
    /// is what its client-side abort exists to avoid.</summary>
    [Theory]
    [InlineData(Route.PersonalInfo)]
    [InlineData(Route.BankDetails)]
    public async Task Save_carrying_a_blank_employee_id_updates_the_callers_own_row(Route route)
    {
        var caller = await SeedCleanerAsync($"esu-blank-{route}@hosttests.local");

        var response = await MobileClient(TokenFor(caller, MobileAudience))
            .PutAsync(Path(route), WithEmployeeId(route, string.Empty));

        HttpAssert.IsOk(response);
        await AssertWrittenAsync(route, caller.EmployeeId);
    }

    /// <summary>The ownership rule must hold on EVERY host that exposes the route (S3), so the two
    /// routes the Partner web host also carries are exercised through its own pipeline.</summary>
    [Theory]
    [InlineData(Route.FullProfile)]
    [InlineData(Route.BankDetails)]
    public async Task Partner_web_host_routes_are_callable_without_an_employee_id(Route route)
    {
        var caller = await SeedCleanerAsync($"esu-web-{route}@hosttests.local");

        var response = await PartnerClient(TokenFor(caller, PartnerAudience))
            .PutAsync(Path(route), WithoutEmployeeId(route));

        HttpAssert.IsOk(response);
        await AssertWrittenAsync(route, caller.EmployeeId);
    }

    [Theory]
    [InlineData(Route.FullProfile)]
    [InlineData(Route.BankDetails)]
    public async Task Partner_web_host_ignores_a_foreign_employee_id(Route route)
    {
        var caller = await SeedCleanerAsync($"esu-web-caller-{route}@hosttests.local");
        var victim = await SeedCleanerAsync($"esu-web-victim-{route}@hosttests.local");

        var response = await PartnerClient(TokenFor(caller, PartnerAudience))
            .PutAsync(Path(route), WithEmployeeId(route, victim.EmployeeId));

        HttpAssert.IsOk(response);
        await AssertUntouchedAsync(route, victim.EmployeeId);
        await AssertWrittenAsync(route, caller.EmployeeId);
    }

    [Fact]
    public async Task An_unauthenticated_caller_cannot_write_an_employee_profile()
    {
        var cleaner = await SeedCleanerAsync("esu-anon@hosttests.local");

        var response = await MobileHost.CreateClient()
            .PutAsync(Path(Route.PersonalInfo), WithoutEmployeeId(Route.PersonalInfo));

        HttpAssert.IsUnauthorized(response);
        await AssertUntouchedAsync(Route.PersonalInfo, cleaner.EmployeeId);
    }
}
