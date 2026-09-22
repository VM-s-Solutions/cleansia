using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Disputes.Filters;
using Cleansia.Core.AppServices.Features.Notifications;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Fcm;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.Orders;

public partial class CreateOrderCallerCurrencyTests
{
    private sealed class ReviewActor
    {
        public IUserSessionProvider Session { get; set; } = new TestUserSessionProvider(CustomerUserId, CustomerEmail,
            [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())]);
    }

    private static Task ConfigureReviewActor(IServiceCollection services, ReviewActor actor)
    {
        CrossMarketWithoutExternalTransports(services);
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => actor.Session));
        return Task.CompletedTask;
    }

    private static AsyncServiceScope ReviewScope(IServiceProvider provider, ReviewActor actor, string userId, UserProfile role, string tenantId)
    {
        actor.Session = new TestUserSessionProvider(userId, $"{userId}@test.local",
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role.ToString())]);
        var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(tenantId);
        return scope;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrossMarket_Dispute_Operator_Reads_All_Messages_Without_Current_Foreign_Account_Pii_And_Can_Resolve(bool crossMarket)
    {
        const string adminId = "review-operator-admin";
        var customerTenant = crossMarket ? TestTenants.Default : TestTenants.Second;
        var visibleName = crossMarket ? "Caller Customer" : "PrivateCurrentName PrivateSurname";
        var visibleEmail = crossMarket ? CustomerEmail : "private-current@test.local";
        var actor = new ReviewActor();
        await TestMethod<string>(setup: services => ConfigureReviewActor(services, actor),
            arrange: async (CleansiaDbContext context) =>
            {
                await SeedCrossOrderAsync(context);
                var customer = await context.Users.SingleAsync(u => u.Id == CustomerUserId);
                customer.TenantId = customerTenant;
                customer.Update("PrivateCurrentName", "PrivateSurname", "+420777999000", new DateOnly(1991, 2, 3));
                context.Entry(customer).Property(u => u.Email).CurrentValue = "private-current@test.local";
                var admin = User.CreateWithPassword("operator-admin@test.local", "Password123!", "Operator", "Support", UserProfile.Administrator, adminRole: AdminRole.Administrator);
                admin.Id = adminId;
                admin.TenantId = TestTenants.Second;
                context.Users.Add(admin);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                string disputeId;
                await using (var customer = ReviewScope(provider, actor, CustomerUserId, UserProfile.Customer, customerTenant))
                {
                    var mediator = customer.ServiceProvider.GetRequiredService<IMediator>();
                    var created = await mediator.Send(new CreateDispute.Command(CrossOrderId, DisputeReason.QualityIssue, "The bathroom was not cleaned completely."));
                    Assert.True(created.IsSuccess, created.Error?.Message);
                    disputeId = created.Value.DisputeId;
                    Assert.True((await mediator.Send(new AddDisputeMessage.Command(disputeId, "Customer follow-up", false))).IsSuccess);
                    var row = await customer.ServiceProvider.GetRequiredService<IDisputeRepository>()
                        .GetQueryableForOwner(CustomerUserId).SingleAsync(d => d.Id == disputeId);
                    row.AddEvidence("bathroom.png", "review/bathroom.png", CustomerUserId);
                    await customer.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
                }

                await using (var admin = ReviewScope(provider, actor, adminId, UserProfile.Administrator, TestTenants.Second))
                {
                    var mediator = admin.ServiceProvider.GetRequiredService<IMediator>();
                    var page = await mediator.Send(new GetPagedDisputes.Request());
                    Assert.Equal(1, page.Total);
                    var listed = Assert.Single(page.Data);
                    Assert.Equal(disputeId, listed.Id);
                    Assert.Equal(visibleName, listed.CustomerName);
                    Assert.Equal(visibleEmail, listed.CustomerEmail);
                    foreach (var filter in new[]
                    {
                        new DisputeFilter(null, null, visibleName.Split(' ')[0], null, null, null, null, null, null, null, null, null),
                        new DisputeFilter(null, null, null, visibleEmail, null, null, null, null, null, null, null, null)
                    })
                    {
                        var searched = await mediator.Send(new GetPagedDisputes.Request { Filter = filter });
                        Assert.Equal(1, searched.Total);
                        Assert.Equal(disputeId, Assert.Single(searched.Data).Id);
                    }
                    foreach (var filter in new[]
                    {
                        new DisputeFilter(null, null, crossMarket ? "PrivateCurrentName" : "Caller", null, null, null, null, null, null, null, null, null),
                        new DisputeFilter(null, null, null, crossMarket ? "private-current@test.local" : CustomerEmail, null, null, null, null, null, null, null, null)
                    })
                    {
                        var searched = await mediator.Send(new GetPagedDisputes.Request { Filter = filter });
                        Assert.Equal(0, searched.Total);
                        Assert.Empty(searched.Data);
                    }
                    Assert.True((await mediator.Send(new AddDisputeMessage.Command(disputeId, "Staff response", true))).IsSuccess);
                    Assert.True((await mediator.Send(new UpdateDisputeStatus.Command(disputeId, DisputeStatus.UnderReview))).IsSuccess);
                }

                await using (var admin = ReviewScope(provider, actor, adminId, UserProfile.Administrator, TestTenants.Second))
                {
                    var mediator = admin.ServiceProvider.GetRequiredService<IMediator>();
                    var detail = await mediator.Send(new GetDisputeDetails.Query(disputeId));
                    Assert.True(detail.IsSuccess, detail.Error?.Message);
                    Assert.Equal(visibleName, detail.Value.CustomerName);
                    Assert.Equal(visibleEmail, detail.Value.CustomerEmail);
                    Assert.Contains(detail.Value.Messages, m => m.Message == "Customer follow-up" && m.AuthorName == visibleName);
                    Assert.Contains(detail.Value.Messages, m => m.Message == "Staff response" && m.AuthorName == "Operator Support");
                    Assert.Single(detail.Value.Evidence);
                    var json = JsonSerializer.Serialize(detail.Value);
                    if (crossMarket)
                    {
                        Assert.DoesNotContain("PrivateCurrentName", json);
                        Assert.DoesNotContain("PrivateSurname", json);
                        Assert.DoesNotContain("private-current", json);
                    }
                    Assert.DoesNotContain("777999000", json);
                    Assert.DoesNotContain("1991", json);
                    Assert.True((await mediator.Send(new ResolveDispute.Command(disputeId, null, "Resolved with an agreed reclean."))).IsSuccess);
                }

                await using (var other = ReviewScope(provider, actor, "other-admin", UserProfile.Administrator, TestTenants.Default))
                {
                    var mediator = other.ServiceProvider.GetRequiredService<IMediator>();
                    Assert.Empty((await mediator.Send(new GetPagedDisputes.Request())).Data);
                    var searched = await mediator.Send(new GetPagedDisputes.Request
                    {
                        Filter = new DisputeFilter(null, null, visibleName.Split(' ')[0], visibleEmail, null, null, null, null, null, null, null, null)
                    });
                    Assert.Equal(0, searched.Total);
                    Assert.Empty(searched.Data);
                    Assert.True((await mediator.Send(new GetDisputeDetails.Query(disputeId))).IsFailure);
                    Assert.True((await mediator.Send(new UpdateDisputeStatus.Command(disputeId, DisputeStatus.UnderReview))).IsFailure);
                    Assert.True((await mediator.Send(new ResolveDispute.Command(disputeId, null, "Must not be accepted"))).IsFailure);
                }

                await using (var customer = ReviewScope(provider, actor, CustomerUserId, UserProfile.Customer, customerTenant))
                {
                    var detail = await customer.ServiceProvider.GetRequiredService<IMediator>().Send(new GetDisputeDetails.Query(disputeId));
                    Assert.True(detail.IsSuccess, detail.Error?.Message);
                    Assert.Equal(2, detail.Value.Messages.Count());
                    Assert.Single(detail.Value.Evidence);
                    Assert.Equal("Resolved with an agreed reclean.", detail.Value.ResolutionNotes);
                }
                return disputeId;
            },
            assert: async (CleansiaDbContext context, string id) =>
            {
                var dispute = await context.Disputes.IgnoreQueryFilters().SingleAsync(d => d.Id == id);
                Assert.Equal(TestTenants.Second, dispute.TenantId);
                Assert.Equal(DisputeStatus.Resolved, dispute.Status);
            }, transactional: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrossMarket_Notifications_Follow_Recipients_For_Feed_And_Dispatch_And_Replay_Respects_Account_Preferences(bool muted)
    {
        const string cleanerId = "review-cleaner-user";
        const string unrelatedId = "review-unrelated-user";
        var actor = new ReviewActor();
        var sent = new List<(string Event, IReadOnlyList<string> Tokens)>();
        var dispatcher = new Mock<IPushDispatcher>();
        dispatcher.Setup(d => d.SendAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, string, IReadOnlyDictionary<string, string>, CancellationToken>((tokens, key, _, _) => sent.Add((key, tokens)))
            .ReturnsAsync(new PushDispatchResult(1, 0, []));
        await TestMethod<string>(setup: services =>
            {
                ConfigureReviewActor(services, actor);
                services.Replace(ServiceDescriptor.Singleton(dispatcher.Object));
                return Task.CompletedTask;
            },
            arrange: async (CleansiaDbContext context) =>
            {
                await SeedCrossOrderAsync(context);
                foreach (var (id, tenant, profile) in new[] { (cleanerId, TestTenants.Second, UserProfile.Employee), (unrelatedId, TestTenants.Default, UserProfile.Customer) })
                {
                    var user = User.CreateWithPassword($"{id}@test.local", "Password123!", "Test", "Recipient", profile);
                    user.Id = id;
                    user.TenantId = tenant;
                    context.Users.Add(user);
                }
                foreach (var (id, tenant) in new[] { (CustomerUserId, TestTenants.Default), (cleanerId, TestTenants.Second), (unrelatedId, TestTenants.Default) })
                {
                    var device = Device.Create(id, "ios", $"token-{id}", $"device-{id}");
                    device.TenantId = tenant;
                    context.Devices.Add(device);
                }
                var preferences = UserNotificationPreferences.CreateDefaults(CustomerUserId);
                preferences.Set(NotificationCategory.OrderUpdates, !muted);
                context.UserNotificationPreferences.Add(preferences);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await using (var action = ReviewScope(provider, actor, cleanerId, UserProfile.Employee, TestTenants.Second))
                {
                    var producer = action.ServiceProvider.GetRequiredService<INotificationProducer>();
                    var args = new Dictionary<string, string> { ["orderId"] = CrossOrderId };
                    await producer.NotifyAsync(CustomerUserId, NotificationEventCatalog.OrderConfirmed, args, TestTenants.Second, CrossOrderId, CancellationToken.None);
                    await producer.NotifyAsync(cleanerId, NotificationEventCatalog.PreferredOffer, args, TestTenants.Default, CrossOrderId, CancellationToken.None);
                    Assert.Equal(TestTenants.Second, action.ServiceProvider.GetRequiredService<ITenantProvider>().GetCurrentTenantId());
                    await action.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
                }

                string notificationId;
                await using (var account = ReviewScope(provider, actor, CustomerUserId, UserProfile.Customer, TestTenants.Default))
                {
                    var mediator = account.ServiceProvider.GetRequiredService<IMediator>();
                    var page = await mediator.Send(new GetPagedUserNotifications.Request { Audience = NotificationFeedAudience.Customer });
                    var notification = Assert.Single(page.Data);
                    notificationId = notification.Id;
                    Assert.Equal(NotificationEventCatalog.OrderConfirmed, notification.EventKey);
                    Assert.Equal(1, (await mediator.Send(new GetUnreadNotificationCount.Query(NotificationFeedAudience.Customer))).Value.Count);
                }
                await using (var unrelated = ReviewScope(provider, actor, unrelatedId, UserProfile.Customer, TestTenants.Default))
                {
                    var mediator = unrelated.ServiceProvider.GetRequiredService<IMediator>();
                    Assert.Empty((await mediator.Send(new GetPagedUserNotifications.Request { Audience = NotificationFeedAudience.Customer })).Data);
                    Assert.Equal(0, (await mediator.Send(new GetUnreadNotificationCount.Query(NotificationFeedAudience.Customer))).Value.Count);
                    Assert.True((await mediator.Send(new MarkNotificationRead.Command(notificationId))).IsFailure);
                }
                await using (var account = ReviewScope(provider, actor, CustomerUserId, UserProfile.Customer, TestTenants.Default))
                {
                    var mediator = account.ServiceProvider.GetRequiredService<IMediator>();
                    Assert.True((await mediator.Send(new MarkNotificationRead.Command(notificationId))).IsSuccess);
                    Assert.Equal(0, (await mediator.Send(new GetUnreadNotificationCount.Query(NotificationFeedAudience.Customer))).Value.Count);
                }
                await using (var cleaner = ReviewScope(provider, actor, cleanerId, UserProfile.Employee, TestTenants.Second))
                {
                    var page = await cleaner.ServiceProvider.GetRequiredService<IMediator>()
                        .Send(new GetPagedUserNotifications.Request { Audience = NotificationFeedAudience.Partner });
                    Assert.Equal(NotificationEventCatalog.PreferredOffer, Assert.Single(page.Data).EventKey);
                }

                List<string> bodies;
                await using (var read = provider.CreateAsyncScope())
                {
                    var rows = await read.ServiceProvider.GetRequiredService<CleansiaDbContext>().OutboxMessages.IgnoreQueryFilters()
                        .Where(m => m.QueueName == QueueNames.NotificationsDispatch).ToListAsync();
                    Assert.Equal(2, rows.Count);
                    foreach (var row in rows)
                    {
                        using var body = JsonDocument.Parse(row.Body);
                        var payload = body.RootElement.GetProperty("payload");
                        var expected = payload.GetProperty("userId").GetString() == CustomerUserId ? TestTenants.Default : TestTenants.Second;
                        Assert.Equal(expected, row.TenantId);
                        Assert.Equal(expected, body.RootElement.GetProperty("tenantId").GetString());
                        Assert.Equal(expected, payload.GetProperty("tenantId").GetString());
                    }
                    bodies = rows.Select(r => r.Body).ToList();
                }
                foreach (var body in bodies)
                {
                    for (var attempt = 0; attempt < 2; attempt++)
                    {
                        await using var delivery = ReviewScope(provider, actor, "system", UserProfile.Administrator, TestTenants.Second);
                        var context = delivery.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                        var guard = new DbIdempotencyGuard(new ProcessedMessageRepository(context));
                        await ActivatorUtilities.CreateInstance<SendPushNotificationHandler>(delivery.ServiceProvider, guard)
                            .HandleAsync(body, CancellationToken.None);
                    }
                }
                Assert.Equal(muted ? 0 : 1, sent.Count(s => s.Event == NotificationEventCatalog.OrderConfirmed));
                Assert.Single(sent, s => s.Event == NotificationEventCatalog.PreferredOffer);
                Assert.DoesNotContain(sent.SelectMany(s => s.Tokens), t => t == $"token-{unrelatedId}");
                if (!muted) Assert.Equal($"token-{CustomerUserId}", Assert.Single(Assert.Single(sent, s => s.Event == NotificationEventCatalog.OrderConfirmed).Tokens));
                Assert.Equal($"token-{cleanerId}", Assert.Single(Assert.Single(sent, s => s.Event == NotificationEventCatalog.PreferredOffer).Tokens));
                return notificationId;
            },
            assert: async (CleansiaDbContext context, string id) =>
            {
                var feed = await context.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
                Assert.Equal(2, feed.Count);
                Assert.Equal(TestTenants.Default, Assert.Single(feed, n => n.Id == id).TenantId);
                Assert.Equal(TestTenants.Second, Assert.Single(feed, n => n.UserId == cleanerId).TenantId);
            }, transactional: false);
    }
}
