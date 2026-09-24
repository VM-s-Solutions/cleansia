using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Bookings.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
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
/// Owner ruling 2026-09-24 for recurring cash, on real Postgres through the real sweep. A LEGACY cash
/// template — authored before the rule, whose selection needs two cleaners — creates no occurrence,
/// stays otherwise untouched and fails nothing in the batch; the customer's list says it needs a
/// payment-method change; and once the customer moves it to card or to a one-cleaner selection the next
/// sweep resumes it. Nothing is switched to card for them and no card is charged: a resumed card
/// occurrence is created unpaid, to be confirmed by the customer as every card occurrence is.
///
/// <para>The legacy rows are seeded directly, because the create and update commands now refuse them.</para>
/// </summary>
[Collection("PostgresCollection")]
public class RecurringCashEligibilityTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Czk = "currency-czk-rcash";
    private const string Czechia = "country-cz-rcash";
    private const string CategoryId = "category-rcash";
    private const string TwoHoursId = "service-rcash-120";
    private const string TwoHoursAndAMinuteId = "service-rcash-121";
    private const string CustomerUserId = "user-rcash";
    private const string CustomerEmail = "recurring-cash@cleansia.test";
    private const string SavedAddressId = "saved-rcash";
    private const string LegacyTemplateId = "template-rcash-legacy";
    private const string EligibleTemplateId = "template-rcash-eligible";
    private const string LegacyOccurrenceId = "order-rcash-legacy";

    private static readonly DateTime FirstOccurrence = DateTime.UtcNow.Date.AddDays(2).AddHours(10);

    [Fact]
    public async Task The_Sweep_Skips_A_Legacy_Cash_Template_Needing_Two_Cleaners_And_Keeps_The_Batch_Healthy()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: SeedAsync(),
            act: async provider =>
            {
                var sweep = await provider.GetRequiredService<IMediator>().Send(new MaterializeRecurringBookings.Command());
                var list = await provider.GetRequiredService<IMediator>().Send(new GetMyRecurringBookings.Query());
                return (sweep, list);
            },
            assert: async (context, outcome) =>
            {
                var (sweep, list) = outcome;
                Assert.True(sweep.IsSuccess);
                Assert.Equal(0, sweep.Value.TemplatesFailed);
                Assert.Equal(2, sweep.Value.TemplatesProcessed);
                Assert.Equal(1, sweep.Value.OrdersCreated);

                Assert.Empty(await OccurrencesOf(context, LegacyTemplateId));
                var legacy = await context.RecurringBookingTemplates.IgnoreQueryFilters().SingleAsync(t => t.Id == LegacyTemplateId);
                Assert.Equal(PaymentType.Cash, legacy.PaymentType);
                Assert.True(legacy.IsActive);
                Assert.Null(legacy.LastMaterializedFor);
                Assert.Equal([TwoHoursAndAMinuteId], legacy.SelectedServiceIds);

                var eligible = Assert.Single(await OccurrencesOf(context, EligibleTemplateId));
                Assert.Equal(PaymentType.Cash, eligible.PaymentType);
                Assert.Equal(1, eligible.RequiredEmployees);
                Assert.Equal(FirstOccurrence, eligible.CleaningDateTime);

                Assert.True(list.IsSuccess);
                var flags = list.Value!.ToDictionary(t => t.Id, t => t.RequiresPaymentMethodChange);
                Assert.True(flags[LegacyTemplateId]);
                Assert.False(flags[EligibleTemplateId]);
            },
            transactional: false);
    }

    [Fact]
    public async Task Moving_The_Template_To_Card_Resumes_It_Unpaid_Without_Charging_Anything()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: SeedAsync(),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                await mediator.Send(new MaterializeRecurringBookings.Command());
                var update = await mediator.Send(Update(PaymentType.Card, TwoHoursAndAMinuteId));
                var sweep = await mediator.Send(new MaterializeRecurringBookings.Command());
                var list = await mediator.Send(new GetMyRecurringBookings.Query());
                return (update, sweep, list);
            },
            assert: async (context, outcome) =>
            {
                var (update, sweep, list) = outcome;
                Assert.True(update.IsSuccess, update.Error?.Message);
                Assert.Equal(0, sweep.Value.TemplatesFailed);
                Assert.False(list.Value!.Single(t => t.Id == LegacyTemplateId).RequiresPaymentMethodChange);

                var resumed = Assert.Single(await OccurrencesOf(context, LegacyTemplateId));
                Assert.Equal(PaymentType.Card, resumed.PaymentType);
                Assert.Equal(PaymentStatus.Pending, resumed.PaymentStatus);
                Assert.Null(resumed.StripePaymentIntentId);
                Assert.Equal(2, resumed.RequiredEmployees);
            },
            transactional: false);
    }

    [Fact]
    public async Task Moving_The_Template_To_A_One_Cleaner_Selection_Resumes_It_As_Cash()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: SeedAsync(),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                await mediator.Send(new MaterializeRecurringBookings.Command());
                var update = await mediator.Send(Update(PaymentType.Cash, TwoHoursId));
                var sweep = await mediator.Send(new MaterializeRecurringBookings.Command());
                var list = await mediator.Send(new GetMyRecurringBookings.Query());
                return (update, sweep, list);
            },
            assert: async (context, outcome) =>
            {
                var (update, sweep, list) = outcome;
                Assert.True(update.IsSuccess, update.Error?.Message);
                Assert.Equal(0, sweep.Value.TemplatesFailed);
                Assert.False(list.Value!.Single(t => t.Id == LegacyTemplateId).RequiresPaymentMethodChange);

                var resumed = Assert.Single(await OccurrencesOf(context, LegacyTemplateId));
                Assert.Equal(PaymentType.Cash, resumed.PaymentType);
                Assert.Equal(1, resumed.RequiredEmployees);
            },
            transactional: false);
    }

    [Fact]
    public async Task Keeping_Cash_On_The_Two_Cleaner_Selection_Is_Refused_And_Leaves_The_Template_Alone()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: SeedAsync(),
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Update(PaymentType.Cash, TwoHoursAndAMinuteId) with { TimeOfDay = "11:30" }),
            assert: async (context, result) =>
            {
                Assert.True(result.IsFailure);
                var refusal = Assert.Single(Assert.IsAssignableFrom<IValidationResult>(result).Errors);
                Assert.Equal(BusinessErrorMessage.OrderCashNotAvailable, refusal.Message);

                var legacy = await context.RecurringBookingTemplates.IgnoreQueryFilters().SingleAsync(t => t.Id == LegacyTemplateId);
                Assert.Equal(new TimeOnly(10, 0), legacy.TimeOfDay);
                Assert.Equal(PaymentType.Cash, legacy.PaymentType);
            },
            transactional: false);
    }

    /// <summary>
    /// The recovery path for an occurrence materialized before the rule: confirming it as cash is
    /// refused and moves nothing; the customer cancels it free (nobody has taken it) and moves the
    /// template to card; the next sweep leaves the cancelled slot alone and creates the later one as an
    /// unpaid card occurrence.
    /// </summary>
    [Fact]
    public async Task A_Legacy_Cash_Occurrence_Cannot_Be_Confirmed_As_Cash_And_The_Customer_Can_Recover_By_Cancel_And_Card()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: SeedAsync(withLegacyOccurrence: true),
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var confirm = await mediator.Send(new ConfirmRecurringOrder.Command(LegacyOccurrenceId));
                var cancel = await mediator.Send(new CancelOrder.Command(LegacyOccurrenceId, Reason: "cash no longer available"));
                var update = await mediator.Send(Update(PaymentType.Card, TwoHoursAndAMinuteId));
                var sweep = await mediator.Send(new MaterializeRecurringBookings.Command(HorizonDays: 14));
                return (confirm, cancel, update, sweep);
            },
            assert: async (context, outcome) =>
            {
                var (confirm, cancel, update, sweep) = outcome;
                Assert.True(confirm.IsFailure);
                Assert.Equal(BusinessErrorMessage.OrderCashNotAvailable, confirm.Error!.Message);
                Assert.True(cancel.IsSuccess, cancel.Error?.Message);
                Assert.Equal(0m, cancel.Value.FeeRate);
                Assert.True(update.IsSuccess, update.Error?.Message);
                Assert.Equal(0, sweep.Value.TemplatesFailed);

                var occurrences = await OccurrencesOf(context, LegacyTemplateId);
                Assert.Equal(2, occurrences.Count);

                var legacy = occurrences.Single(o => o.Id == LegacyOccurrenceId);
                Assert.Equal(PaymentType.Cash, legacy.PaymentType);
                Assert.Equal(PaymentStatus.Pending, legacy.PaymentStatus);
                Assert.Equal(OrderStatus.Cancelled, legacy.CurrentStatus);

                var later = occurrences.Single(o => o.Id != LegacyOccurrenceId);
                Assert.Equal(FirstOccurrence.AddDays(7), later.CleaningDateTime);
                Assert.Equal(PaymentType.Card, later.PaymentType);
                Assert.Equal(PaymentStatus.Pending, later.PaymentStatus);
            },
            transactional: false);
    }

    private static async Task<List<Order>> OccurrencesOf(CleansiaDbContext context, string templateId) =>
        await context.Orders.IgnoreQueryFilters().Where(o => o.RecurringTemplateId == templateId).ToListAsync();

    private static UpdateRecurringBooking.Command Update(PaymentType paymentType, string serviceId) => new(
        TemplateId: LegacyTemplateId,
        Frequency: (int)RecurrenceFrequency.Weekly,
        DayOfWeek: (int)FirstOccurrence.DayOfWeek,
        TimeOfDay: "10:00",
        Rooms: 2,
        Bathrooms: 1,
        SavedAddressId: SavedAddressId,
        SelectedServiceIds: [serviceId],
        SelectedPackageIds: [],
        PaymentType: (int)paymentType,
        StartsOn: DateTime.UtcNow.Date);

    private static Task CustomerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CustomerUserId,
            CustomerEmail,
            [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
        services.Replace(ServiceDescriptor.Scoped<IRefundService>(_ => new NoRefunds()));
        return Task.CompletedTask;
    }

    private static Func<CleansiaDbContext, Task> SeedAsync(bool withLegacyOccurrence = false) => async context =>
    {
        context.Languages.Add(Language.Create("en", "English"));
        TestLegalDocuments.Add(context);

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = Czechia;
        context.Countries.Add(country);
        context.CountryConfigurations.Add(CountryConfiguration.Create(Czechia, "CZK", "cs", 0.21m).AssignOperator(TestTenants.Default));

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = Czk;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var category = ServiceCategory.Create("recurring-cash", "Recurring cash", "Category under test");
        category.Id = CategoryId;
        context.Add(category);
        foreach (var (id, minutes) in new[] { (TwoHoursId, 120), (TwoHoursAndAMinuteId, 121) })
        {
            var service = Service.Create(CategoryId, id, "Under test", minutes);
            service.Id = id;
            context.Add(service);
            context.EmployeePayConfigs.Add(EmployeePayConfig.CreateForService(id, 100m, Czk));
            context.ServicePrices.Add(ServicePrice.Create(id, Czk, 900m, 0m));
        }

        var user = User.CreateWithPassword(
            CustomerEmail, TestUtilities.Constants.TestUserSession.TestUserPassword, "Recurring", "Customer", UserProfile.Customer);
        user.Id = CustomerUserId;
        user.ConfirmEmail();
        context.Add(user);

        var plan = MembershipPlan.Create("PLUS", "Plus", 0m, 4, true);
        context.MembershipPlans.Add(plan);
        context.UserMemberships.Add(UserMembership.Create(
            CustomerUserId, plan.Id, Czk, "sub_rcash", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMonths(1)));

        var address = Address.Create("Opakovana 3", "Praha", "11000", Czechia);
        context.Addresses.Add(address);
        var saved = SavedAddress.Create(CustomerUserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        context.SavedAddresses.Add(saved);

        context.RecurringBookingTemplates.AddRange(
            Template(LegacyTemplateId, TwoHoursAndAMinuteId),
            Template(EligibleTemplateId, TwoHoursId));

        if (withLegacyOccurrence)
        {
            var occurrence = Order.Create(
                "Recurring Customer", CustomerEmail, "+420777444555",
                Address.Create("Opakovana 3", "Praha", "11000", Czechia),
                rooms: 2, bathrooms: 1, FirstOccurrence, PaymentType.Cash, 900m, Czk, PaymentStatus.Pending,
                userId: CustomerUserId, recurringTemplateId: LegacyTemplateId);
            occurrence.Id = LegacyOccurrenceId;
            occurrence.UpdateEstimatedTime(121);
            occurrence.CalculateRequiredEmployees(BookingPolicy.SpareSeatsPerOrder);
            occurrence.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, occurrence));
            context.Orders.Add(occurrence);
        }

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    };

    private static RecurringBookingTemplate Template(string id, string serviceId)
    {
        var template = RecurringBookingTemplate.Create(
            userId: CustomerUserId,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: FirstOccurrence.DayOfWeek,
            timeOfDay: new TimeOnly(10, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: SavedAddressId,
            selectedServiceIds: [serviceId],
            selectedPackageIds: [],
            paymentType: PaymentType.Cash,
            startsOn: DateTime.UtcNow.Date);
        template.Id = id;
        return template;
    }

    private sealed class NoRefunds : IRefundService
    {
        public Task<BusinessResult<RefundResult>> IssueRefundAsync(RefundRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("An unpaid cash occurrence has nothing to refund.");
    }
}
