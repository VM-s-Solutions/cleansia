using System.Text.Json;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Fcm;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.Memberships;

[Collection("PostgresCollection")]
public class RecurringPauseNotificationTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string UserA = "recurring-pause-a";
    private const string UserB = "recurring-pause-b";
    private const string MemberA = "membership-pause-a";
    private const string MemberB = "membership-pause-b";
    private const string TemplateA = "template-pause-a1";
    private const string TemplateA2 = "template-pause-a2";
    private const string TemplateB = "template-pause-b";

    private sealed class Run
    {
        public readonly Mock<IPushDispatcher> Push = new();
        public readonly List<(string Event, IReadOnlyList<string> Tokens)> Sent = [];
        public TaskCompletionSource<bool>? BothStaged;
        public int Staged;
        public int FailNextCommit;
        public Func<Task>? AfterStage;

        public Task Setup(IServiceCollection services)
        {
            services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider()));
            services.Replace(ServiceDescriptor.Scoped<INotificationProducer>(sp => new CoordinatedProducer(
                ActivatorUtilities.CreateInstance<NotificationProducer>(sp), sp.GetRequiredService<CleansiaDbContext>(), this)));
            Push.Setup(x => x.SendAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback((IReadOnlyList<string> tokens, string key, IReadOnlyDictionary<string, string> _, CancellationToken _) => Sent.Add((key, tokens.ToArray())))
                .ReturnsAsync(new PushDispatchResult(1, 0, []));
            services.Replace(ServiceDescriptor.Singleton(Push.Object));
            return Task.CompletedTask;
        }
    }

    private sealed class CoordinatedProducer(INotificationProducer inner, CleansiaDbContext db, Run run) : INotificationProducer
    {
        public async Task NotifyAsync(string userId, string eventKey, Dictionary<string, string> args,
            string? tenantId, string? subject, CancellationToken cancellationToken)
        {
            await inner.NotifyAsync(userId, eventKey, args, tenantId, subject, cancellationToken);
            if (eventKey != NotificationEventCatalog.RecurringPaused) return;
            if (Interlocked.Exchange(ref run.FailNextCommit, 0) == 1)
                db.Set<UserNotification>().Local.Single().TenantId = "missing-operator";
            if (run.BothStaged is not null)
            {
                if (Interlocked.Increment(ref run.Staged) == 2) run.BothStaged.TrySetResult(true);
                await run.BothStaged.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            if (run.AfterStage is not null) await run.AfterStage();
        }
    }

    private static async Task Seed(CleansiaDbContext db, bool muted = false, bool secondAccountTemplate = false,
        bool history = false, bool trialOnly = false, bool existingOccurrence = false)
    {
        var now = DateTime.UtcNow;
        db.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", true);
        country.Id = "pause-country";
        db.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Koruna");
        currency.Id = "pause-currency";
        currency.IsActive = true;
        db.Currencies.Add(currency);
        var plan = MembershipPlan.Create("PAUSE-PLUS", "Plus", 5m, 4, false);
        plan.Id = "pause-plan";
        db.MembershipPlans.Add(plan);
        foreach (var (userId, membershipId, tenant) in new[] { (UserA, MemberA, TestTenants.Default), (UserB, MemberB, TestTenants.Second) })
        {
            var user = User.CreateWithPassword($"{userId}@example.test", "Password123!", "Recurring", "Customer");
            user.Id = userId;
            user.TenantId = tenant;
            db.Users.Add(user);
            var device = Device.Create(userId, "ios", $"token-{userId}", $"device-{userId}");
            device.TenantId = tenant;
            db.Devices.Add(device);
            var preferences = UserNotificationPreferences.CreateDefaults(userId);
            preferences.TenantId = tenant;
            preferences.Set(NotificationCategory.RecurringScheduled, !muted);
            db.UserNotificationPreferences.Add(preferences);
            var membership = UserMembership.Create(userId, plan.Id, currency.Id, $"sub-{userId}", now.AddDays(-10), now.AddDays(20),
                trialOnly && userId == UserA ? now.AddDays(2) : null);
            membership.Id = membershipId;
            membership.TenantId = tenant;
            membership.Created("seed", DateTimeOffset.UtcNow.AddDays(-10));
            if (!trialOnly || userId != UserA)
                membership.RecordRecurringPauseState("active", now.AddDays(-10), now.AddDays(-10));
            membership.UpdateFromStripeWebhook(trialOnly && userId == UserA ? "trialing" : "past_due",
                membership.CurrentPeriodStart, membership.CurrentPeriodEnd, null);
            membership.RecordRecurringPauseState(trialOnly && userId == UserA ? "trialing" : "past_due",
                now.AddHours(-1), now);
            db.UserMemberships.Add(membership);
        }
        foreach (var (templateId, userId, tenant) in new[]
                 { (TemplateA, UserA, TestTenants.Second), (TemplateA2, UserA, TestTenants.Second), (TemplateB, UserB, TestTenants.Default) })
        {
            if (templateId == TemplateB && !secondAccountTemplate) continue;
            var template = RecurringBookingTemplate.Create(userId, RecurrenceFrequency.Weekly, now.AddDays(1).DayOfWeek,
                new TimeOnly(10, 0), 1, 1, "preserved-address", [], [], PaymentType.Cash, now.AddDays(-7));
            template.Id = templateId;
            template.TenantId = tenant;
            db.RecurringBookingTemplates.Add(template);
        }
        if (existingOccurrence)
        {
            var order = Order.Create("Recurring Customer", $"{UserA}@example.test", "+420777111222",
                Address.Create("Booked Street", "Prague", "11000", country.Id), 1, 1, now.AddDays(5),
                PaymentType.Cash, 1000m, currency.Id, PaymentStatus.Paid, userId: UserA, recurringTemplateId: TemplateA);
            order.Id = "existing-occurrence";
            order.TenantId = TestTenants.Second;
            order.CustomerAddress!.TenantId = TestTenants.Second;
            var track = OrderStatusTrack.Create(OrderStatus.New, order);
            track.TenantId = TestTenants.Second;
            order.AddOrderStatus(track);
            db.Orders.Add(order);
        }
        if (history)
        {
            var old = UserMembership.Create(UserA, plan.Id, currency.Id, "sub-old", now.AddYears(-1), now.AddYears(1));
            old.Id = "membership-old";
            old.TenantId = TestTenants.Default;
            old.Created("seed", DateTimeOffset.UtcNow.AddYears(-1));
            if (!trialOnly) old.RecordRecurringPauseState("active", now.AddYears(-1), now.AddYears(-1));
            old.UpdateFromStripeWebhook("canceled", old.CurrentPeriodStart, old.CurrentPeriodEnd, null);
            old.RecordRecurringPauseState("canceled", now.AddMonths(-6), now);
            db.UserMemberships.Add(old);
        }
        StampUnstampedAdded(db, TestTenants.Default);
        await db.CommitAsync(CancellationToken.None);
    }

    private static async Task Skip(IServiceProvider provider, string template = TemplateA)
    {
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
            new MaterializeRecurringBookingTemplate.Command(template, DateTime.UtcNow, 7));
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(0, result.Value.OrdersCreated);
    }

    private static async Task UpdateMembership(IServiceProvider provider, string status, string membershipId = MemberA, DateTime? occurredAt = null)
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Default);
        var db = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        var membership = await db.UserMemberships.SingleAsync(x => x.Id == membershipId);
        await scope.ServiceProvider.GetRequiredService<IStripeSubscriptionWebhookHandler>().HandleAsync(new Stripe.Event
        {
            Id = $"evt-{Guid.NewGuid():N}",
            Created = occurredAt ?? DateTime.UtcNow,
            Type = "customer.subscription.updated",
            Data = new Stripe.EventData
            {
                Object = new Stripe.Subscription
                {
                    Id = membership.StripeSubscriptionId,
                    Status = status,
                    Items = new Stripe.StripeList<Stripe.SubscriptionItem>
                    {
                        Data = [new Stripe.SubscriptionItem { CurrentPeriodStart = membership.CurrentPeriodStart, CurrentPeriodEnd = membership.CurrentPeriodEnd }],
                    },
                },
            },
        }, CancellationToken.None);
        await db.CommitAsync(CancellationToken.None);
    }

    private static async Task<string> CreateEnrollment(IServiceProvider provider, string status, DateTime occurredAt)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        const string subscriptionId = "sub-failed-reenrollment";
        await scope.ServiceProvider.GetRequiredService<IStripeSubscriptionWebhookHandler>().HandleAsync(new Stripe.Event
        {
            Id = $"evt-{Guid.NewGuid():N}",
            Type = "customer.subscription.created",
            Created = occurredAt,
            Data = new Stripe.EventData
            {
                Object = new Stripe.Subscription
                {
                    Id = subscriptionId,
                    Status = status,
                    Currency = "czk",
                    Metadata = new Dictionary<string, string> { ["UserId"] = UserA, ["MembershipPlanCode"] = "PAUSE-PLUS" },
                    Items = new Stripe.StripeList<Stripe.SubscriptionItem>
                    {
                        Data = [new Stripe.SubscriptionItem { CurrentPeriodStart = occurredAt, CurrentPeriodEnd = occurredAt.AddMonths(1) }],
                    },
                },
            },
        }, CancellationToken.None);
        await db.CommitAsync(CancellationToken.None);
        return (await db.UserMemberships.SingleAsync(x => x.StripeSubscriptionId == subscriptionId)).Id;
    }

    private static async Task Dispatch(IServiceProvider provider, string body)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        var guard = new DbIdempotencyGuard(new ProcessedMessageRepository(db));
        await ActivatorUtilities.CreateInstance<SendPushNotificationHandler>(scope.ServiceProvider, guard)
            .HandleAsync(body, CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Multiple_templates_and_repeated_sweeps_create_one_account_notice_and_muting_only_suppresses_push(bool muted)
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db, muted: muted, history: true, existingOccurrence: true),
            act: async (IServiceProvider provider) =>
            {
                await Skip(provider);
                await Skip(provider, TemplateA2);
                await Skip(provider);
                using var read = provider.CreateScope();
                read.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Default);
                var db = read.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                var notification = await db.Set<UserNotification>().SingleAsync();
                Assert.Equal(UserA, notification.UserId);
                Assert.Equal(NotificationEventCatalog.RecurringPaused, notification.EventKey);
                var outbox = await db.OutboxMessages.SingleAsync();
                using var body = JsonDocument.Parse(outbox.Body);
                Assert.Equal(TestTenants.Default, body.RootElement.GetProperty("tenantId").GetString());
                Assert.Equal(TestTenants.Default, body.RootElement.GetProperty("payload").GetProperty("tenantId").GetString());
                await Dispatch(provider, outbox.Body);
                await Dispatch(provider, outbox.Body);
                Assert.Equal(muted ? 0 : 1, run.Sent.Count);
                if (!muted) Assert.Equal($"token-{UserA}", Assert.Single(Assert.Single(run.Sent).Tokens));
                Assert.DoesNotContain(run.Sent.SelectMany(x => x.Tokens), x => x == $"token-{UserB}");
                return true;
            }, assert: async (CleansiaDbContext db, bool _) =>
            {
                var current = await db.UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == MemberA);
                Assert.NotNull(current.RecurringPauseNotificationSentAt);
                Assert.Equal(1, current.RecurringPauseNotificationSequence);
                Assert.Equal(0, (await db.UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == "membership-old")).RecurringPauseNotificationSequence);
                Assert.Equal(0, (await db.UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == MemberB)).RecurringPauseNotificationSequence);
                Assert.All(await db.RecurringBookingTemplates.IgnoreQueryFilters().ToListAsync(), x => { Assert.True(x.IsActive); Assert.Null(x.LastMaterializedFor); });
                var existing = await db.Orders.IgnoreQueryFilters().SingleAsync();
                Assert.Equal("existing-occurrence", existing.Id);
                Assert.Equal(OrderStatus.New, existing.CurrentStatus);
                Assert.Equal(PaymentStatus.Paid, existing.PaymentStatus);
                Assert.Equal(TestTenants.Default, (await db.Set<UserNotification>().IgnoreQueryFilters().SingleAsync()).TenantId);
            }, transactional: false);
    }

    [Fact]
    public async Task Feed_retention_does_not_rearm_but_paid_recovery_then_same_period_lapse_creates_a_new_dispatch()
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db), act: async (IServiceProvider provider) =>
        {
            await Skip(provider);
            using (var retention = provider.CreateScope())
                await retention.ServiceProvider.GetRequiredService<CleansiaDbContext>().Set<UserNotification>().IgnoreQueryFilters().ExecuteDeleteAsync();
            await Skip(provider, TemplateA2);
            using (var read = provider.CreateScope())
                Assert.Empty(await read.ServiceProvider.GetRequiredService<CleansiaDbContext>().Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
            await UpdateMembership(provider, "active");
            await UpdateMembership(provider, "past_due");
            await Skip(provider);
            using var scope = provider.CreateScope();
            var bodies = await scope.ServiceProvider.GetRequiredService<CleansiaDbContext>().OutboxMessages.IgnoreQueryFilters().Select(x => x.Body).ToListAsync();
            Assert.Equal(2, bodies.Count);
            foreach (var body in bodies) await Dispatch(provider, body);
            Assert.Equal(2, run.Sent.Count);
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            Assert.Equal(2, (await db.UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == MemberA)).RecurringPauseNotificationSequence);
            var keys = await db.OutboxMessages.IgnoreQueryFilters().Select(x => x.MessageKey).ToListAsync();
            Assert.Contains(MessageKeys.Push(UserA, NotificationEventCatalog.RecurringPaused, $"{MemberA}:1"), keys);
            Assert.Contains(MessageKeys.Push(UserA, NotificationEventCatalog.RecurringPaused, $"{MemberA}:2"), keys);
            Assert.Single(await db.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
        }, transactional: false);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Provider_state_chronology_distinguishes_delayed_recovery_from_pre_lapse_replay(bool genuineRecovery)
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db), act: async (IServiceProvider provider) =>
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).UtcDateTime;
            var priorLapseAt = now.AddMinutes(-10);
            await UpdateMembership(provider, "past_due", occurredAt: priorLapseAt);
            await Skip(provider);
            var activeAt = now.AddMinutes(genuineRecovery ? -5 : -15);
            await UpdateMembership(provider, "active", occurredAt: activeAt);
            await UpdateMembership(provider, "active", occurredAt: activeAt);
            using (var read = provider.CreateScope())
            {
                var membership = await read.ServiceProvider.GetRequiredService<CleansiaDbContext>()
                    .UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == MemberA);
                Assert.Equal(genuineRecovery, membership.RecurringPauseNotificationSentAt is null);
                Assert.Equal(genuineRecovery ? activeAt : priorLapseAt, membership.RecurringPauseStateObservedAt);
                Assert.Equal(1, membership.RecurringPauseNotificationSequence);
                if (genuineRecovery) Assert.Equal(activeAt, membership.PaidPeriodConfirmedAt);
                else Assert.True(membership.PaidPeriodConfirmedAt < activeAt);
            }
            await UpdateMembership(provider, "past_due");
            await Skip(provider);
            await Skip(provider, TemplateA2);
            using var dispatch = provider.CreateScope();
            var bodies = await dispatch.ServiceProvider.GetRequiredService<CleansiaDbContext>()
                .OutboxMessages.IgnoreQueryFilters().Select(x => x.Body).ToListAsync();
            foreach (var body in bodies) await Dispatch(provider, body);
            Assert.Equal(genuineRecovery ? 2 : 1, run.Sent.Count);
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            var expected = genuineRecovery ? 2 : 1;
            var membership = await db.UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == MemberA);
            Assert.Equal(expected, membership.RecurringPauseNotificationSequence);
            var feed = await db.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
            Assert.Equal(expected, feed.Count);
            Assert.All(feed, x => { Assert.Equal(UserA, x.UserId); Assert.Equal(TestTenants.Default, x.TenantId); });
            var keys = await db.OutboxMessages.IgnoreQueryFilters().Select(x => x.MessageKey).ToListAsync();
            Assert.Equal(expected, keys.Count);
            Assert.Contains(MessageKeys.Push(UserA, NotificationEventCatalog.RecurringPaused, $"{MemberA}:1"), keys);
            Assert.Equal(genuineRecovery, keys.Contains(MessageKeys.Push(UserA, NotificationEventCatalog.RecurringPaused, $"{MemberA}:2")));
        }, transactional: false);
    }

    [Theory]
    [InlineData("incomplete")]
    [InlineData("incomplete_expired")]
    public async Task Failed_reenrollment_keeps_the_account_lapse_until_authoritative_paid_recovery(string failedStatus)
    {
        var run = new Run();
        await TestMethod<string>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db), act: async (IServiceProvider provider) =>
        {
            await Skip(provider);
            var reenrollment = await CreateEnrollment(provider, failedStatus, DateTime.UtcNow);
            await Skip(provider, TemplateA2);
            await Skip(provider);
            using (var read = provider.CreateScope())
            {
                read.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Default);
                var db = read.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                var failed = await db.UserMemberships.SingleAsync(x => x.Id == reenrollment);
                Assert.Equal(MembershipStatus.Cancelled, failed.Status);
                Assert.Null(failed.PaidPeriodConfirmedAt);
                Assert.Equal(0, failed.RecurringPauseNotificationSequence);
                Assert.Equal(MemberA, (await read.ServiceProvider.GetRequiredService<IUserMembershipRepository>()
                    .GetLatestPaidForUserAsync(UserA, CancellationToken.None))!.Id);
                Assert.Single(await db.Set<UserNotification>().ToListAsync());
                Assert.Single(await db.OutboxMessages.ToListAsync());
            }
            await UpdateMembership(provider, "active", reenrollment);
            using (var read = provider.CreateScope())
            {
                read.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Default);
                var current = await read.ServiceProvider.GetRequiredService<IUserMembershipRepository>()
                    .GetLatestPaidForUserAsync(UserA, CancellationToken.None);
                Assert.Equal(reenrollment, current!.Id);
                Assert.NotNull(current.PaidPeriodConfirmedAt);
                Assert.Null(current.RecurringPauseNotificationSentAt);
            }
            await UpdateMembership(provider, "past_due", reenrollment);
            await Skip(provider);
            await Skip(provider, TemplateA2);
            return reenrollment;
        }, assert: async (CleansiaDbContext db, string reenrollment) =>
        {
            var notifications = await db.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
            Assert.Equal(2, notifications.Count);
            Assert.All(notifications, x => { Assert.Equal(UserA, x.UserId); Assert.Equal(TestTenants.Default, x.TenantId); });
            var keys = await db.OutboxMessages.IgnoreQueryFilters().Select(x => x.MessageKey).ToListAsync();
            Assert.Equal(2, keys.Count);
            Assert.Contains(MessageKeys.Push(UserA, NotificationEventCatalog.RecurringPaused, $"{MemberA}:1"), keys);
            Assert.Contains(MessageKeys.Push(UserA, NotificationEventCatalog.RecurringPaused, $"{reenrollment}:1"), keys);
            Assert.Equal(1, (await db.UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == MemberA)).RecurringPauseNotificationSequence);
            Assert.Equal(1, (await db.UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == reenrollment)).RecurringPauseNotificationSequence);
        }, transactional: false);
    }

    [Fact]
    public async Task Latest_paid_observation_beats_enrollment_age_and_a_delayed_historical_webhook()
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db, history: true), act: async (IServiceProvider provider) =>
        {
            await Skip(provider);
            await UpdateMembership(provider, "active", "membership-old", DateTime.UtcNow.AddDays(-20));
            using (var swap = provider.CreateScope())
            {
                swap.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Default);
                var db = swap.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                var yearly = MembershipPlan.Create("PAUSE-YEARLY", "Yearly", 5m, 4, false, BillingInterval.Yearly);
                db.MembershipPlans.Add(yearly);
                db.MembershipPlanPrices.Add(MembershipPlanPrice.Create(yearly.Id, "pause-currency", 1200m, "price-pause-yearly"));
                await db.CommitAsync(CancellationToken.None);
                var stripe = new Mock<IStripeClient>();
                stripe.Setup(x => x.SwapSubscriptionPriceAsync("sub-old", "price-pause-yearly", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new SubscriptionResult("sub-old", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), Status: "active"));
                var handler = ActivatorUtilities.CreateInstance<SwapMembershipPlan.Handler>(swap.ServiceProvider,
                    new TestUserSessionProvider(UserA, $"{UserA}@example.test"), stripe.Object);
                var result = await handler.Handle(new SwapMembershipPlan.Command(yearly.Code), CancellationToken.None);
                Assert.True(result.IsSuccess, result.Error?.Message);
                await db.CommitAsync(CancellationToken.None);
            }
            await UpdateMembership(provider, "past_due", "membership-old");
            await UpdateMembership(provider, "active", occurredAt: DateTime.UtcNow.AddDays(-2));
            await UpdateMembership(provider, "past_due");
            using (var read = provider.CreateScope())
            {
                read.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Default);
                var current = await read.ServiceProvider.GetRequiredService<IUserMembershipRepository>()
                    .GetLatestPaidForUserAsync(UserA, CancellationToken.None);
                Assert.Equal("membership-old", current!.Id);
                var recentEnrollment = await read.ServiceProvider.GetRequiredService<CleansiaDbContext>()
                    .UserMemberships.SingleAsync(x => x.Id == MemberA);
                Assert.True(current.CreatedOn < recentEnrollment.CreatedOn);
                Assert.True(current.PaidPeriodConfirmedAt > recentEnrollment.PaidPeriodConfirmedAt);
                Assert.NotNull(recentEnrollment.RecurringPauseNotificationSentAt);
            }
            await Skip(provider);
            await Skip(provider, TemplateA2);
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            Assert.Equal(2, await db.Set<UserNotification>().IgnoreQueryFilters().CountAsync());
            Assert.Equal(2, await db.OutboxMessages.IgnoreQueryFilters().CountAsync());
            Assert.Equal(1, (await db.UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == MemberA)).RecurringPauseNotificationSequence);
            Assert.Equal(1, (await db.UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == "membership-old")).RecurringPauseNotificationSequence);
        }, transactional: false);
    }

    [Fact]
    public async Task Failed_commit_rolls_back_latch_feed_and_outbox_and_a_fresh_retry_sends_once()
    {
        var run = new Run { FailNextCommit = 1 };
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db), act: async (IServiceProvider provider) =>
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => Skip(provider));
            using (var read = provider.CreateScope())
            {
                var db = read.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                Assert.Equal(0, (await db.UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == MemberA)).RecurringPauseNotificationSequence);
                Assert.Empty(await db.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
                Assert.Empty(await db.OutboxMessages.IgnoreQueryFilters().ToListAsync());
            }
            await Skip(provider);
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            Assert.Equal(1, (await db.UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == MemberA)).RecurringPauseNotificationSequence);
            Assert.Single(await db.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
            Assert.Single(await db.OutboxMessages.IgnoreQueryFilters().ToListAsync());
        }, transactional: false);
    }

    [Fact]
    public async Task Concurrent_templates_share_one_membership_latch_and_the_loser_commits_no_notification()
    {
        var run = new Run { BothStaged = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db), act: async (IServiceProvider provider) =>
        {
            var results = await Task.WhenAll(Record.ExceptionAsync(() => Skip(provider)), Record.ExceptionAsync(() => Skip(provider, TemplateA2)));
            Assert.Single(results, x => x is null);
            Assert.IsAssignableFrom<DbUpdateException>(Assert.Single(results, x => x is not null));
            run.BothStaged = null;
            await Skip(provider);
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            Assert.Equal(1, (await db.UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == MemberA)).RecurringPauseNotificationSequence);
            Assert.Single(await db.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
            Assert.Single(await db.OutboxMessages.IgnoreQueryFilters().ToListAsync());
        }, transactional: false);
    }

    [Fact]
    public async Task Paid_webhook_recovery_wins_over_a_stale_pause_attempt()
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db), act: async (IServiceProvider provider) =>
        {
            run.AfterStage = () => UpdateMembership(provider, "active");
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Skip(provider));
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            var membership = await db.UserMemberships.IgnoreQueryFilters().SingleAsync(x => x.Id == MemberA);
            Assert.Equal(MembershipStatus.Active, membership.Status);
            Assert.Null(membership.RecurringPauseNotificationSentAt);
            Assert.Equal(0, membership.RecurringPauseNotificationSequence);
            Assert.Empty(await db.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
            Assert.Empty(await db.OutboxMessages.IgnoreQueryFilters().ToListAsync());
        }, transactional: false);
    }

    [Fact]
    public async Task Whole_sweep_uses_each_owners_account_scope_across_operators()
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db, secondAccountTemplate: true),
            act: async (IServiceProvider provider) =>
            {
                var result = await provider.GetRequiredService<IMediator>().Send(new MaterializeRecurringBookings.Command());
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(0, result.Value.TemplatesFailed);
                Assert.Equal(3, result.Value.TemplatesProcessed);
                Assert.Equal(0, result.Value.OrdersCreated);
                return true;
            }, assert: async (CleansiaDbContext db, bool _) =>
            {
                var rows = await db.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
                Assert.Equal(2, rows.Count);
                Assert.Equal(TestTenants.Default, Assert.Single(rows, x => x.UserId == UserA).TenantId);
                Assert.Equal(TestTenants.Second, Assert.Single(rows, x => x.UserId == UserB).TenantId);
                Assert.All(await db.UserMemberships.IgnoreQueryFilters().ToListAsync(), x => Assert.Equal(1, x.RecurringPauseNotificationSequence));
            }, transactional: false);
    }

    [Fact]
    public async Task A_current_trial_does_not_notify_for_a_historical_cancelled_membership()
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db, history: true, trialOnly: true),
            act: async (IServiceProvider provider) => { await Skip(provider); return true; },
            assert: async (CleansiaDbContext db, bool _) =>
            {
                Assert.Empty(await db.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
                Assert.Empty(await db.OutboxMessages.IgnoreQueryFilters().ToListAsync());
                Assert.All(await db.UserMemberships.IgnoreQueryFilters().ToListAsync(), x => Assert.Equal(0, x.RecurringPauseNotificationSequence));
            }, transactional: false);
    }

    [Fact]
    public async Task Two_distinct_order_notifications_keep_feed_preferences_and_replay_identity()
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db), act: async (IServiceProvider provider) =>
        {
            using (var action = provider.CreateScope())
            {
                var producer = action.ServiceProvider.GetRequiredService<INotificationProducer>();
                foreach (var key in new[] { NotificationEventCatalog.OrderCleanerAssigned, NotificationEventCatalog.OrderPaymentConfirmed })
                    await producer.NotifyAsync(UserA, key, new Dictionary<string, string> { ["orderId"] = "old-order", ["orderNumber"] = "A-1234" },
                        TestTenants.Second, "old-order", CancellationToken.None);
                await action.ServiceProvider.GetRequiredService<CleansiaDbContext>().CommitAsync(CancellationToken.None);
            }
            using var read = provider.CreateScope();
            read.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Default);
            var db = read.ServiceProvider.GetRequiredService<CleansiaDbContext>();
            var feed = await db.Set<UserNotification>().Where(x => NotificationFeedEventKeys.Customer.Contains(x.EventKey)).ToListAsync();
            Assert.Equal(2, feed.Count);
            Assert.All(feed, x => Assert.Equal(UserA, x.UserId));
            var bodies = await db.OutboxMessages.Select(x => x.Body).ToListAsync();
            foreach (var body in bodies)
            {
                await Dispatch(provider, body);
                using var json = JsonDocument.Parse(body);
                await Dispatch(provider, json.RootElement.GetProperty("payload").GetRawText());
            }
            Assert.Equal(2, run.Sent.Count);
            Assert.Contains(run.Sent, x => x.Event == "order.cleaner_assigned");
            Assert.Contains(run.Sent, x => x.Event == "order.payment_confirmed");
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            var keys = await db.OutboxMessages.IgnoreQueryFilters().Select(x => x.MessageKey).ToListAsync();
            Assert.Contains($"push:{UserA}:order.cleaner_assigned:old-order", keys);
            Assert.Contains($"push:{UserA}:order.payment_confirmed:old-order", keys);
        }, transactional: false);
    }
}
