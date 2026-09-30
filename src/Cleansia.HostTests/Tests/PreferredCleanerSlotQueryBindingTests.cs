using System.Net;
using System.Net.Http.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Services;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0039 D5, over the REAL Customer host — the slot question travels on the query string, and the
/// tri-state survives the wire.
///
/// <para>Every other test of this feature calls the handler or the mediator, so none of them exercises
/// model binding. If <c>SelectedServiceIds</c> failed to bind, the server would derive a zero-length
/// window, decline to answer, and return <c>null</c> on every row — a silent, total no-op that looks
/// exactly like a client that has not been rebuilt. That is why the busy case is asserted through HTTP:
/// <c>false</c> can only be produced by a request whose collection parameter arrived.</para>
/// </summary>
public sealed class PreferredCleanerSlotQueryBindingTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string ServiceId = "svc-slot-binding";
    private const string SizedServiceId = "svc-slot-binding-sized";
    private const int ServiceMinutes = 180;

    private static readonly DateTime CleaningUtc = DateTime.UtcNow.AddDays(6).Date.AddHours(10);

    [Fact]
    public async Task The_Slot_Is_Answered_Only_When_The_Selection_Reaches_The_Server()
    {
        const string customerEmail = "slot-binding-cust@hosttests.local";
        var arranged = await ArrangeBusyFavouriteAsync(
            "slot-binding", ServiceId, minutesPerRoom: 0, commitmentOffsetMinutes: 60);
        var client = CustomerClient(TestJwtFactory.Mint(
            CustomerAudience, arranged.CustomerId, customerEmail, UserProfile.Customer));

        var withoutSlot = await GetServingCleanersAsync(client, "/api/Order/MyServingCleaners");
        var withSlot = await GetServingCleanersAsync(
            client,
            "/api/Order/MyServingCleaners"
            + $"?CleaningDateTimeUtc={Uri.EscapeDataString(CleaningUtc.ToString("O"))}"
            + $"&SelectedServiceIds={ServiceId}");

        Assert.Null(Assert.Single(withoutSlot).IsAvailableForRequestedSlot);

        var answered = Assert.Single(withSlot);
        Assert.Equal(arranged.EmployeeId, answered.EmployeeId);
        Assert.False(answered.IsAvailableForRequestedSlot);
    }

    /// <summary>
    /// The home's size and the dirtiness level lengthen the window, so they must bind too: the
    /// commitment starts 200 minutes in, clear of the 180-minute selection alone and inside it once two
    /// rooms and a bathroom at ten minutes each, or the increased level's 234 minutes, reach the server.
    /// </summary>
    [Fact]
    public async Task The_Home_Size_And_The_Level_Reach_The_Server()
    {
        const string customerEmail = "slot-binding-sized-cust@hosttests.local";
        var arranged = await ArrangeBusyFavouriteAsync(
            "slot-binding-sized", SizedServiceId, minutesPerRoom: 10, commitmentOffsetMinutes: 200);
        var client = CustomerClient(TestJwtFactory.Mint(
            CustomerAudience, arranged.CustomerId, customerEmail, UserProfile.Customer));
        var slot = "/api/Order/MyServingCleaners"
            + $"?CleaningDateTimeUtc={Uri.EscapeDataString(CleaningUtc.ToString("O"))}"
            + $"&SelectedServiceIds={SizedServiceId}";

        var selectionAlone = Assert.Single(await GetServingCleanersAsync(client, slot));
        var withRooms = Assert.Single(await GetServingCleanersAsync(client, slot + "&Rooms=2&Bathrooms=1"));
        var withLevel = Assert.Single(await GetServingCleanersAsync(client, slot + "&DirtinessLevel=1"));

        Assert.True(selectionAlone.IsAvailableForRequestedSlot);
        Assert.False(withRooms.IsAvailableForRequestedSlot);
        Assert.False(withLevel.IsAvailableForRequestedSlot);
    }

    private static async Task<IReadOnlyList<ServingCleaner>> GetServingCleanersAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<ServingCleaner>>())!;
    }

    private async Task<(string CustomerId, string EmployeeId)> ArrangeBusyFavouriteAsync(
        string prefix, string serviceId, int minutesPerRoom, int commitmentOffsetMinutes)
    {
        string customerId = "", employeeId = "";
        var customerEmail = $"{prefix}-cust@hosttests.local";
        var planCode = prefix.ToUpperInvariant();

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var category = ServiceCategory.Create(prefix, "Home", "Home cleaning");
            ctx.Add(category);
            var service = Service.Create(category.Id, "Deep clean", "Deep clean", ServiceMinutes, minutesPerRoom);
            service.Id = serviceId;
            ctx.Add(service);

            var customer = DomainSeed.Customer(customerEmail);
            ctx.Users.Add(customer);

            var plan = DomainSeed.MembershipPlan(planCode);
            ctx.Add(DomainSeed.MembershipPlanPrice(plan.Id, planCode));
            ctx.Add(plan);
            ctx.Add(DomainSeed.ActiveMembership(customer.Id, plan.Id));

            var cleanerUser = DomainSeed.EmployeeUser($"{prefix}-cleaner@hosttests.local");
            ctx.Users.Add(cleanerUser);
            var cleaner = DomainSeed.ApprovedEmployee(cleanerUser);
            ctx.Employees.Add(cleaner);

            // The completed job is what puts this cleaner in the customer's picker at all.
            var history = DomainSeed.NewOrder(customer.Id, customerEmail);
            history.AddAssignedEmployee(OrderEmployee.Create(history, cleaner));
            history.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, history));
            ctx.Orders.Add(history);

            // And this is the commitment that makes them unavailable for the slot being asked about.
            var commitment = DomainSeed.NewOrder(customer.Id, customerEmail);
            commitment.UpdateEstimatedTime(120);
            commitment.AddAssignedEmployee(OrderEmployee.Create(commitment, cleaner));
            commitment.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, commitment));
            SetCleaningTime(commitment, CleaningUtc.AddMinutes(commitmentOffsetMinutes));
            ctx.Orders.Add(commitment);

            customerId = customer.Id;
            employeeId = cleaner.Id;
        });

        return (customerId, employeeId);
    }

    /// <summary>The appointment instant is chosen by the booking path; the seed sets it directly.</summary>
    private static void SetCleaningTime(Order order, DateTime cleaningUtc) =>
        typeof(Order)
            .GetProperty(nameof(Order.CleaningDateTime))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(order, [cleaningUtc]);

    private sealed record ServingCleaner(
        string EmployeeId, string FullName, DateTime LastServedOn, bool? IsAvailableForRequestedSlot);
}
