using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Employees;

/// <summary>
/// GDPR Art. 7(1) requires the controller to be able to DEMONSTRATE consent, so the only assertion
/// that discharges it is one that reads the row back out of the database.
///
/// <para><c>OnboardingConsentTests</c> (unit) asserts on a Moq callback on
/// <c>IUserConsentRepository.Add</c>. That is the repository call, not the persistence boundary: it
/// passes green over a command whose type name stops ending in <c>Command</c> (the unit-of-work
/// behavior keys the commit on exactly that, so the grant is added to a change tracker nothing ever
/// commits), and it cannot see the unique index at all, because a mocked repository has none. The
/// assertions below run in a FRESH scope and a FRESH <c>DbContext</c> after the act, so what is read
/// back is what Postgres holds.</para>
/// </summary>
[Collection("PostgresCollection")]
public class OnboardingConsentPersistenceTests(PostgresContainerFixture fixture)
    : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-consent";

    [Fact]
    public async Task Completing_Onboarding_Writes_A_Granted_DataProcessing_Consent_Row()
    {
        await TestMethod(
            setup: NoopGeocoder,
            arrange: SeedCleaner,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(Onboarding(consent: true));
            },
            assert: async (CleansiaDbContext context, BusinessResult<UpdateEmployee.Response> result) =>
            {
                Assert.True(result.IsSuccess, $"UpdateEmployee failed with: {result.Error?.Message}");

                var consent = await context.UserConsents.IgnoreQueryFilters().SingleAsync();

                Assert.Equal(TestConstants.TestUserSession.TestUserId, consent.UserId);
                Assert.Equal(ConsentType.DataProcessing, consent.ConsentType);
                Assert.True(consent.IsGranted);
                Assert.NotNull(consent.GrantedAt);
                Assert.Null(consent.WithdrawnAt);
            },
            transactional: false);
    }

    /// <summary>
    /// The anti-forgery direction, and the reason a gate without a record is worse than neither: a
    /// consent row may only exist because someone ticked the box. A refused submit must leave no
    /// evidence that it was ever given.
    /// </summary>
    [Fact]
    public async Task Onboarding_Refused_For_Missing_Consent_Writes_No_Consent_Row()
    {
        await TestMethod(
            setup: NoopGeocoder,
            arrange: SeedCleaner,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(Onboarding(consent: false));
            },
            assert: async (CleansiaDbContext context, BusinessResult<UpdateEmployee.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.Required);

                Assert.Empty(await context.UserConsents.IgnoreQueryFilters().ToListAsync());
            },
            transactional: false);
    }

    /// <summary>
    /// A cleaner edits their profile more than once, and <c>UserConsents</c> carries a unique index on
    /// <c>(UserId, ConsentType)</c>. A second unconditional insert surfaces as a <c>DbUpdateException</c>
    /// at the unit-of-work behavior — after the handler has returned, so no handler-level try/catch sees
    /// it (S7b). The mocked-repository unit suite has no index and cannot fail here.
    ///
    /// <para>The preserved <c>GrantedAt</c> is the second half: it is the demonstrable timestamp, so a
    /// re-save that moved it forward would quietly destroy the date actually being demonstrated.</para>
    /// </summary>
    [Fact]
    public async Task Re_Saving_The_Profile_Keeps_One_Row_And_Does_Not_Move_The_Grant_Timestamp()
    {
        await TestMethod(
            setup: NoopGeocoder,
            arrange: SeedCleaner,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();

                var first = await mediator.Send(Onboarding(consent: true));
                Assert.True(first.IsSuccess, $"first submit failed with: {first.Error?.Message}");

                var grantedAt = await GrantedAtAsync(provider);

                var second = await mediator.Send(Onboarding(consent: true));
                Assert.True(second.IsSuccess, $"second submit failed with: {second.Error?.Message}");

                return grantedAt;
            },
            assert: async (CleansiaDbContext context, DateTimeOffset? firstGrantedAt) =>
            {
                var consent = await context.UserConsents.IgnoreQueryFilters().SingleAsync();

                Assert.True(consent.IsGranted);
                Assert.Equal(firstGrantedAt, consent.GrantedAt);
            },
            transactional: false);
    }

    private static async Task<DateTimeOffset?> GrantedAtAsync(IServiceProvider provider)
    {
        var context = provider.GetRequiredService<CleansiaDbContext>();
        return await context.UserConsents
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(c => c.GrantedAt)
            .SingleAsync();
    }

    private static UpdateEmployee.Command Onboarding(bool consent) => new(
        EmployeeId: null,
        FirstName: "First",
        LastName: "Last",
        BirthDate: new DateOnly(1990, 1, 1),
        Street: "Main Street 10",
        City: "Prague",
        ZipCode: "11000",
        CountryId: CountryId,
        State: null,
        NationalityId: CountryId,
        Phone: "+420123456789",
        PassportId: "AB12345",
        EntityType: EmployeeEntityType.NaturalPerson,
        RegistrationNumber: "12345678",
        VatNumber: null,
        LegalEntityName: null,
        EmergencyName: null,
        EmergencyPhone: null,
        Consent: consent);

    private static Task NoopGeocoder(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IAddressGeocoder, NoopAddressGeocoder>());
        return Task.CompletedTask;
    }

    private static async Task SeedCleaner(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var user = User.CreateWithPassword(
            email: TestConstants.TestUserSession.TestUserEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName,
            UserProfile.Employee);
        user.Id = TestConstants.TestUserSession.TestUserId;
        user.ConfirmEmail();
        context.Users.Add(user);
        await context.CommitAsync(CancellationToken.None);

        context.Employees.Add(Employee.CreateWithUser(user));
        await context.CommitAsync(CancellationToken.None);
    }

    private sealed class NoopAddressGeocoder : IAddressGeocoder
    {
        public Task PopulateCoordinatesAsync(Address address, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
