using System.Net;
using System.Text;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// T-0494 — recurring schedules are a paid Cleansia Plus perk, and every client gates the entry point.
/// A gate that only lives in the client is not a gate: <c>CanManageRecurringBookings</c> maps to
/// <c>CustomerOnly</c>, so every signed-in customer carries it and only the server can decide. These
/// drive the REAL route on the REAL host — the whole finding was that a unit test on the handler cannot
/// make that claim.
///
/// <para><b>The load-bearing legs are the positive ones.</b> "No membership → 400" passes for any 400 —
/// a renamed route, a malformed body, a rate-limit 429 that is not even a 400. What gives it meaning is
/// that the byte-identical request succeeds once an Active membership row exists
/// (<see cref="An_active_member_posting_the_same_bytes_is_served"/>), and that a TRIALING member is
/// served too (<see cref="A_trialing_member_is_served"/>) — Stripe's <c>trialing</c> collapses to
/// <c>Active</c> and only the metered express waiver is withheld during a trial, so that leg dies in the
/// opposite direction if anyone ever harmonizes this gate with the express resolver's trial conjunct.</para>
///
/// <para>Every leg posts the SAME request bytes, built once into <see cref="CreateBodyJson"/>; the only
/// thing that varies between them is the membership row.</para>
///
/// <para>Host coverage is <c>Web.Customer</c>. The enforcement point is the shared AppServices
/// handler/validator, so <c>Web.Mobile.Customer</c> — whose controller is a byte-identical sibling —
/// cannot diverge without the shared code diverging first; booting a fifth host would mean adding it to
/// the harness's project references and audience map for no additional claim.</para>
/// </summary>
public sealed class RecurringBookingMembershipGateRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string CustomerId = "recur-gate-customer";
    private const string CustomerEmail = "recur-gate@hosttests.local";
    private const string StrangerId = "recur-gate-stranger";
    private const string StrangerEmail = "recur-gate-stranger@hosttests.local";
    private const string SavedAddressId = "recur-gate-address";
    private const string TemplateId = "recur-gate-template";
    private const string ForeignTemplateId = "recur-gate-foreign";

    private const string SeededServiceId = "service-seeded";
    private const string ReAuthoredServiceId = "service-reauthored";

    private const string CreateRoute = "/api/RecurringBooking/Create";
    private const string UpdateRoute = "/api/RecurringBooking/Update";
    private const string SetActiveRoute = "/api/RecurringBooking/SetActive";
    private const string DeleteRoute = "/api/RecurringBooking/Delete";

    private static readonly DateTime StartsOn = DateTime.UtcNow.Date.AddDays(3);

    private static readonly string CreateBodyJson = JsonSerializer.Serialize(new
    {
        frequency = (int)RecurrenceFrequency.Biweekly,
        dayOfWeek = (int)System.DayOfWeek.Tuesday,
        timeOfDay = "09:00",
        rooms = 2,
        bathrooms = 1,
        savedAddressId = SavedAddressId,
        selectedServiceIds = new[] { SeededServiceId },
        selectedPackageIds = Array.Empty<string>(),
        paymentType = (int)PaymentType.Card,
        startsOn = StartsOn,
        endsOn = (DateTime?)null,
        preferredEmployeeId = (string?)null,
    });

    private static readonly string UpdateBodyJson = ReAuthorBodyFor(TemplateId);
    private static readonly string ForeignUpdateBodyJson = ReAuthorBodyFor(ForeignTemplateId);

    public enum Membership { None, Active, Trialing, Cancelled, PastDue, PeriodExpired }

    // ── L1 / L2 — the pair. Neither means anything without the other. ──────────────────────────────

    [Fact]
    public async Task A_customer_with_no_membership_is_refused_and_nothing_is_persisted()
    {
        await ArrangeAsync(Membership.None);

        var response = await PostAsync(CreateRoute, CreateBodyJson);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(
            response, BusinessErrorMessage.RecurringTemplateMembershipRequired);
        Assert.Equal(0, await CountTemplatesAsync(CustomerId));
    }

    [Fact]
    public async Task An_active_member_posting_the_same_bytes_is_served()
    {
        await ArrangeAsync(Membership.Active);

        var response = await PostAsync(CreateRoute, CreateBodyJson);

        HttpAssert.IsOk(response);
        Assert.Equal(1, await CountTemplatesAsync(CustomerId));
    }

    // ── L3 — status, not existence. Dies if the predicate is rewritten as "has any membership row". ──

    [Theory]
    [InlineData(Membership.Cancelled)]
    [InlineData(Membership.PastDue)]
    [InlineData(Membership.PeriodExpired)]
    public async Task A_membership_row_that_is_not_providing_benefits_is_refused(Membership membership)
    {
        await ArrangeAsync(membership);

        var response = await PostAsync(CreateRoute, CreateBodyJson);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(
            response, BusinessErrorMessage.RecurringTemplateMembershipRequired);
        Assert.Equal(0, await CountTemplatesAsync(CustomerId));
    }

    // ── L4 — the other direction. A trial withholds the METERED benefits only. ────────────────────

    [Fact]
    public async Task A_trialing_member_is_served()
    {
        await ArrangeAsync(Membership.Trialing);

        var response = await PostAsync(CreateRoute, CreateBodyJson);

        HttpAssert.IsOk(response);
        Assert.Equal(1, await CountTemplatesAsync(CustomerId));
    }

    // ── L5 — the escape hatch stays open. Gating these would make pause a one-way door. ───────────

    [Fact]
    public async Task A_lapsed_subscriber_can_still_pause_and_delete_a_template_that_is_generating()
    {
        await ArrangeAsync(Membership.Cancelled, withTemplate: true);
        var client = CustomerClient(TokenFor(CustomerId, CustomerEmail));

        var paused = await client.PostAsync(SetActiveRoute, Json(
            JsonSerializer.Serialize(new { templateId = TemplateId, isActive = false })));

        HttpAssert.IsOk(paused);
        Assert.False((await LoadTemplateAsync(TemplateId))!.IsActive);

        var deleted = await client.PostAsync(DeleteRoute, Json(
            JsonSerializer.Serialize(new { templateId = TemplateId })));

        HttpAssert.IsOk(deleted);
        Assert.Null(await LoadTemplateAsync(TemplateId));
    }

    // ── L6 — Update is Create wearing an old id. ──────────────────────────────────────────────────

    [Fact]
    public async Task A_lapsed_subscriber_cannot_re_author_a_template_and_the_row_is_untouched()
    {
        await ArrangeAsync(Membership.Cancelled, withTemplate: true);

        var response = await PostAsync(UpdateRoute, UpdateBodyJson);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(
            response, BusinessErrorMessage.RecurringTemplateMembershipRequired);

        // A 400 with the write already applied is the failure mode this asserts against.
        var template = (await LoadTemplateAsync(TemplateId))!;
        Assert.Equal(RecurrenceFrequency.Monthly, template.Frequency);
        Assert.Equal(SeededServiceId, Assert.Single(template.SelectedServiceIds));
        Assert.NotNull(template.LastMaterializedFor);
    }

    [Fact]
    public async Task An_active_member_re_authoring_the_same_template_is_served()
    {
        await ArrangeAsync(Membership.Active, withTemplate: true);

        var response = await PostAsync(UpdateRoute, UpdateBodyJson);

        HttpAssert.IsOk(response);
        var template = (await LoadTemplateAsync(TemplateId))!;
        Assert.Equal(RecurrenceFrequency.Weekly, template.Frequency);
        Assert.Equal(ReAuthoredServiceId, Assert.Single(template.SelectedServiceIds));
        Assert.Null(template.LastMaterializedFor);
    }

    /// <summary>
    /// The entitlement link is the LAST link of the ownership chain, so a template the caller does not
    /// own resolves as not-owned and never as "you need Plus" — otherwise the refusal is an oracle for
    /// whether the caller happens to be a subscriber on somebody else's id.
    /// </summary>
    [Fact]
    public async Task A_lapsed_subscriber_updating_someone_elses_template_is_told_not_owned_not_membership()
    {
        await ArrangeAsync(Membership.Cancelled, withForeignTemplate: true);

        var response = await PostAsync(UpdateRoute, ForeignUpdateBodyJson);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(
            response, BusinessErrorMessage.RecurringTemplateNotOwnedByUser);
        Assert.DoesNotContain(
            BusinessErrorMessage.RecurringTemplateMembershipRequired,
            await response.Content.ReadAsStringAsync());
    }

    // ── arrange / act helpers ─────────────────────────────────────────────────────────────────────

    private async Task ArrangeAsync(
        Membership membership,
        bool withTemplate = false,
        bool withForeignTemplate = false)
    {
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var customer = DomainSeed.Customer(CustomerEmail);
            customer.Id = CustomerId;
            ctx.Users.Add(customer);

            var (address, saved) = DomainSeed.SavedAddressFor(CustomerId);
            saved.Id = SavedAddressId;
            ctx.Addresses.Add(address);
            ctx.SavedAddresses.Add(saved);

            if (membership != Membership.None)
            {
                var plan = DomainSeed.MembershipPlan();
                ctx.MembershipPlans.Add(plan);
                ctx.UserMemberships.Add(BuildMembership(plan.Id, membership));
            }

            if (withTemplate)
            {
                ctx.RecurringBookingTemplates.Add(BuildTemplate(TemplateId, CustomerId));
            }

            if (withForeignTemplate)
            {
                var stranger = DomainSeed.Customer(StrangerEmail);
                stranger.Id = StrangerId;
                ctx.Users.Add(stranger);
                ctx.RecurringBookingTemplates.Add(BuildTemplate(ForeignTemplateId, StrangerId));
            }
        });
    }

    private static UserMembership BuildMembership(string planId, Membership membership)
    {
        var now = DateTime.UtcNow;

        if (membership == Membership.PeriodExpired)
        {
            // Status is still Active — only the paid period has run out. This is the case a naive
            // "row exists" or "status == Active" predicate lets through.
            return UserMembership.Create(
                userId: CustomerId,
                membershipPlanId: planId,
                stripeSubscriptionId: "sub_recur_gate",
                currentPeriodStart: now.AddDays(-60),
                currentPeriodEnd: now.AddDays(-1));
        }

        var row = UserMembership.Create(
            userId: CustomerId,
            membershipPlanId: planId,
            stripeSubscriptionId: "sub_recur_gate",
            currentPeriodStart: now.AddDays(-3),
            currentPeriodEnd: now.AddDays(27),
            trialEndsAtUtc: membership == Membership.Trialing ? now.AddDays(7) : null);

        return membership switch
        {
            Membership.Cancelled => row.UpdateFromStripeWebhook(
                "canceled", now.AddDays(-3), now.AddDays(27), null),
            Membership.PastDue => row.UpdateFromStripeWebhook(
                "past_due", now.AddDays(-3), now.AddDays(27), null),
            _ => row,
        };
    }

    private static RecurringBookingTemplate BuildTemplate(string id, string ownerUserId)
    {
        var template = RecurringBookingTemplate.Create(
            userId: ownerUserId,
            frequency: RecurrenceFrequency.Monthly,
            dayOfWeek: System.DayOfWeek.Monday,
            timeOfDay: new TimeOnly(8, 0),
            rooms: 1,
            bathrooms: 1,
            savedAddressId: SavedAddressId,
            selectedServiceIds: [SeededServiceId],
            selectedPackageIds: [],
            paymentType: PaymentType.Card,
            startsOn: DateTime.UtcNow.Date.AddDays(1));
        template.Id = id;
        template.MarkMaterializedFor(DateTime.UtcNow.Date.AddDays(1));
        return template;
    }

    private static string ReAuthorBodyFor(string templateId) => JsonSerializer.Serialize(new
    {
        templateId,
        frequency = (int)RecurrenceFrequency.Weekly,
        dayOfWeek = (int)System.DayOfWeek.Friday,
        timeOfDay = "18:30",
        rooms = 6,
        bathrooms = 3,
        savedAddressId = SavedAddressId,
        selectedServiceIds = new[] { ReAuthoredServiceId },
        selectedPackageIds = Array.Empty<string>(),
        paymentType = (int)PaymentType.Cash,
        startsOn = StartsOn,
        endsOn = (DateTime?)null,
    });

    private Task<HttpResponseMessage> PostAsync(string route, string body) =>
        CustomerClient(TokenFor(CustomerId, CustomerEmail)).PostAsync(route, Json(body));

    private static string TokenFor(string userId, string email) =>
        TestJwtFactory.Mint(CustomerAudience, userId, email, UserProfile.Customer);

    private static HttpContent Json(string body) =>
        new StringContent(body, Encoding.UTF8, "application/json");

    private Task<int> CountTemplatesAsync(string userId) =>
        QueryAsync(ctx => ctx.Set<RecurringBookingTemplate>()
            .IgnoreQueryFilters()
            .CountAsync(t => t.UserId == userId));

    private Task<RecurringBookingTemplate?> LoadTemplateAsync(string templateId) =>
        QueryAsync(ctx => ctx.Set<RecurringBookingTemplate>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId));
}
