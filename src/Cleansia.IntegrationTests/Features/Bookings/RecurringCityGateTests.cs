using System.Security.Claims;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Bookings.DTOs;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Service = Cleansia.Core.Domain.Services.Service;

namespace Cleansia.IntegrationTests.Features.Bookings;

/// <summary>
/// Owner ruling 2026-10-25 (decision 9) on real Postgres through the real pipeline: a recurring schedule is
/// refused <c>city.not_serviced</c> at a saved address no service city serves, on create and on edit, as a
/// one-off booking is. The Prague row answers by the postcode, so a saved address written "Praha" with a
/// Brno postcode is refused while one with a Prague postcode is accepted.
/// </summary>
[Collection("PostgresCollection")]
public class RecurringCityGateTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Czk = "currency-czk-rcity";
    private const string Czechia = "country-cz-rcity";
    private const string CategoryId = "category-rcity";
    private const string ServiceId = "service-rcity";
    private const string CustomerUserId = "user-rcity";
    private const string CustomerEmail = "recurring-city@cleansia.test";
    private const string PragueAddressId = "saved-rcity-prague";
    private const string BrnoPostcodeAddressId = "saved-rcity-brno-postcode";
    private const string TemplateId = "template-rcity";

    [Fact]
    public async Task A_Schedule_At_An_Address_No_City_Serves_Is_Refused_On_Create_And_On_Edit()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: SeedAsync,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var refusedCreate = await mediator.Send(Create(BrnoPostcodeAddressId));
                var refusedEdit = await mediator.Send(Edit(BrnoPostcodeAddressId));
                var acceptedCreate = await mediator.Send(Create(PragueAddressId));
                return (refusedCreate, refusedEdit, acceptedCreate);
            },
            assert: async (context, outcome) =>
            {
                var (refusedCreate, refusedEdit, acceptedCreate) = outcome;
                AssertRefusedAsNotServiced(refusedCreate);
                AssertRefusedAsNotServiced(refusedEdit);
                Assert.True(acceptedCreate.IsSuccess, acceptedCreate.Error?.Message);

                var templates = await context.RecurringBookingTemplates.IgnoreQueryFilters()
                    .Where(t => t.UserId == CustomerUserId).ToListAsync();
                Assert.Equal(2, templates.Count);
                Assert.All(templates, t => Assert.Equal(PragueAddressId, t.SavedAddressId));
            },
            transactional: false);
    }

    private static void AssertRefusedAsNotServiced(BusinessResult<RecurringBookingTemplateDto> result)
    {
        Assert.True(result.IsFailure);
        var refusal = Assert.Single(Assert.IsAssignableFrom<IValidationResult>(result).Errors);
        Assert.Equal(BusinessErrorMessage.CityNotServiced, refusal.Message);
    }

    private static CreateRecurringBooking.Command Create(string savedAddressId) => new(
        Frequency: (int)RecurrenceFrequency.Weekly,
        DayOfWeek: (int)DayOfWeek.Tuesday,
        TimeOfDay: "10:00",
        Rooms: 2,
        Bathrooms: 1,
        SavedAddressId: savedAddressId,
        SelectedServiceIds: [ServiceId],
        SelectedPackageIds: [],
        PaymentType: (int)PaymentType.Card,
        StartsOn: DateTime.UtcNow.Date.AddDays(3),
        TermsAccepted: true,
        EarlyPerformanceRequested: true);

    private static UpdateRecurringBooking.Command Edit(string savedAddressId) => new(
        TemplateId: TemplateId,
        Frequency: (int)RecurrenceFrequency.Weekly,
        DayOfWeek: (int)DayOfWeek.Tuesday,
        TimeOfDay: "10:00",
        Rooms: 2,
        Bathrooms: 1,
        SavedAddressId: savedAddressId,
        SelectedServiceIds: [ServiceId],
        SelectedPackageIds: [],
        PaymentType: (int)PaymentType.Card,
        StartsOn: DateTime.UtcNow.Date.AddDays(3));

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        TestLegalDocuments.Add(context);

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = Czechia;
        context.Countries.Add(country);
        context.CountryConfigurations.Add(CountryConfiguration.Create(Czechia, "CZK", "cs", 0.21m).AssignOperator(TestTenants.Default));
        context.Add(ServiceCity.Create(Czechia, "Praha", "1"));
        context.Add(ServiceCity.Create(Czechia, "Brno"));

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = Czk;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var category = ServiceCategory.Create("recurring-city", "Recurring city", "Category under test");
        category.Id = CategoryId;
        context.Add(category);
        var service = Service.Create(CategoryId, ServiceId, "Under test", 120);
        service.Id = ServiceId;
        context.Add(service);
        context.EmployeePayConfigs.Add(EmployeePayConfig.CreateForService(ServiceId, 100m, Czk));
        context.ServicePrices.Add(ServicePrice.Create(ServiceId, Czk, 900m, 0m));

        var user = User.CreateWithPassword(
            CustomerEmail, TestUtilities.Constants.TestUserSession.TestUserPassword, "Recurring", "Customer", UserProfile.Customer);
        user.Id = CustomerUserId;
        user.ConfirmEmail();
        context.Add(user);

        var plan = MembershipPlan.Create("PLUS", "Plus", 0m, true);
        context.MembershipPlans.Add(plan);
        context.UserMemberships.Add(UserMembership.Create(
            CustomerUserId, plan.Id, Czk, "sub_rcity", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMonths(1)));

        foreach (var (id, zipCode) in new[] { (PragueAddressId, "110 00"), (BrnoPostcodeAddressId, "602 00") })
        {
            var address = Address.Create("Opakovana 3", "Praha", zipCode, Czechia);
            context.Addresses.Add(address);
            var saved = SavedAddress.Create(CustomerUserId, address.Id, id, isDefault: false);
            saved.Id = id;
            context.SavedAddresses.Add(saved);
        }

        var template = RecurringBookingTemplate.Create(
            userId: CustomerUserId,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: DayOfWeek.Tuesday,
            timeOfDay: new TimeOnly(10, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: PragueAddressId,
            selectedServiceIds: [ServiceId],
            selectedPackageIds: [],
            paymentType: PaymentType.Card,
            startsOn: DateTime.UtcNow.Date.AddDays(3));
        template.Id = TemplateId;
        context.RecurringBookingTemplates.Add(template);

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static Task CustomerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CustomerUserId,
            CustomerEmail,
            [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
        return Task.CompletedTask;
    }
}
