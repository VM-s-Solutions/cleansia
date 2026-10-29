using System.Text.Json;
using Cleansia.Core.AppServices.Features.Devices;
using Cleansia.Core.Clients.Abstractions.Apns;
using Cleansia.Core.Clients.Abstractions.Fcm;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
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

namespace Cleansia.IntegrationTests.Features.Devices;

/// <summary>
/// A shared phone whose previous account's sign-out never reached the server. The next account's
/// registration of the phone's device id and push token retires every other account's active row for
/// that install, in either company, and deletes that account's Live Activity tokens for it, so neither a
/// notification nor a Live Activity push to a previous account reaches the phone any more; rows that route
/// to another install are left alone.
/// </summary>
[Collection("PostgresCollection")]
public class SharedHandsetDeviceRegistrationTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Handset = "android-id-shared-handset";
    private const string InstallToken = "fcm-token-shared-install";

    private const string NextAccount = "user-next";
    private const string PreviousAccountOtherCompany = "user-prev-other-co";
    private const string PreviousAccountSameCompany = "user-prev-same-co";

    [Fact]
    public async Task Registering_A_Handset_Retires_Every_Other_Accounts_Active_Row_For_It_So_Their_Pushes_Stop_Reaching_It()
    {
        var harness = new Harness();
        await TestMethod<IReadOnlyList<(string OrderId, string[] Tokens)>>(
            setup: harness.Setup,
            arrange: async context =>
            {
                SeedAccounts(context,
                    (NextAccount, TestTenants.Default),
                    (PreviousAccountOtherCompany, TestTenants.Second),
                    (PreviousAccountSameCompany, TestTenants.Default));
                AddDevice(context, PreviousAccountOtherCompany, TestTenants.Second, Handset, InstallToken);
                AddDevice(context, PreviousAccountSameCompany, TestTenants.Default, Handset, InstallToken);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await harness.RegisterAsync(provider, NextAccount, TestTenants.Default, Handset, InstallToken);
                // Registering again, as a later sign-in does, finds the caller's own row in the database and must leave it.
                await harness.RegisterAsync(provider, NextAccount, TestTenants.Default, Handset, InstallToken);

                await harness.PushAsync(provider, PreviousAccountOtherCompany, TestTenants.Second);
                await harness.PushAsync(provider, PreviousAccountSameCompany, TestTenants.Default);
                await harness.PushAsync(provider, NextAccount, TestTenants.Default);
                return harness.Sent.ToList();
            },
            assert: async (context, sent) =>
            {
                var (orderId, tokens) = Assert.Single(sent);
                Assert.Equal(OrderOf(NextAccount), orderId);
                Assert.Equal(new[] { InstallToken }, tokens);

                var rows = await context.Devices.IgnoreQueryFilters().ToListAsync();
                var otherCompany = Assert.Single(rows, d => d.UserId == PreviousAccountOtherCompany);
                Assert.False(otherCompany.IsActive);
                Assert.NotNull(otherCompany.DeactivatedOn);
                Assert.Equal(TestTenants.Second, otherCompany.TenantId);

                var sameCompany = Assert.Single(rows, d => d.UserId == PreviousAccountSameCompany);
                Assert.False(sameCompany.IsActive);
                Assert.NotNull(sameCompany.DeactivatedOn);
                Assert.Equal(TestTenants.Default, sameCompany.TenantId);

                var next = Assert.Single(rows, d => d.UserId == NextAccount);
                Assert.True(next.IsActive);
                Assert.Equal(InstallToken, next.DeviceToken);
                Assert.Equal(TestTenants.Default, next.TenantId);
            },
            transactional: false);
    }

    [Fact]
    public async Task Registering_An_iPhone_Deletes_Each_Retired_Accounts_Live_Activity_Tokens_For_It_So_Their_Lock_Screen_Pushes_Stop_Reaching_It()
    {
        const string iPhone = "ios-keychain-id-shared-iphone";
        const string iPhoneApnsToken = "apns-token-shared-iphone";
        const string iPhonePushToStartToken = "apns-push-to-start-shared-iphone";
        const string otherIPhone = "ios-keychain-id-other-iphone";
        const string otherIPhonePushToStartToken = "apns-push-to-start-other-iphone";
        (string Account, string Company)[] previousAccounts =
        [
            (PreviousAccountOtherCompany, TestTenants.Second),
            (PreviousAccountSameCompany, TestTenants.Default),
        ];
        var harness = new Harness();
        await TestMethod<IReadOnlyList<string>>(
            setup: harness.Setup,
            arrange: async context =>
            {
                SeedAccounts(context,
                    (NextAccount, TestTenants.Default),
                    (PreviousAccountOtherCompany, TestTenants.Second),
                    (PreviousAccountSameCompany, TestTenants.Default));
                foreach (var (account, company) in previousAccounts)
                {
                    AddDevice(context, account, company, iPhone, iPhoneApnsToken, "ios");
                    AddActivityTokens(context, account, company, iPhone, iPhonePushToStartToken);
                }

                AddActivityTokens(context, PreviousAccountOtherCompany, TestTenants.Second, otherIPhone, otherIPhonePushToStartToken);
                AddActivityTokens(context, NextAccount, TestTenants.Default, iPhone, iPhonePushToStartToken);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await harness.RegisterAsync(provider, NextAccount, TestTenants.Default, iPhone, iPhoneApnsToken, "ios");

                foreach (var (account, company) in previousAccounts)
                {
                    await harness.SendLiveActivityAsync(provider, account, company, LiveActivityEventKeys.Start);
                    await harness.SendLiveActivityAsync(provider, account, company, LiveActivityEventKeys.Update);
                }

                return harness.ActivityPushes.ToList();
            },
            assert: async (context, activityPushes) =>
            {
                Assert.Equal(
                    new[] { otherIPhonePushToStartToken, UpdateTokenOf(PreviousAccountOtherCompany, otherIPhone) },
                    activityPushes);

                var rows = await context.LiveActivityTokens.IgnoreQueryFilters().ToListAsync();
                Assert.DoesNotContain(rows, t => t.DeviceId == iPhone && t.UserId != NextAccount);
                Assert.Equal(2, rows.Count(t =>
                    t.UserId == PreviousAccountOtherCompany && t.DeviceId == otherIPhone && t.TenantId == TestTenants.Second));
                Assert.Equal(2, rows.Count(t => t.UserId == NextAccount && t.DeviceId == iPhone));
            },
            transactional: false);
    }

    [Fact]
    public async Task Registering_A_Handset_Leaves_Rows_That_Route_To_Another_Install_And_Retired_Rows_As_They_Were()
    {
        const string siblingAppAccount = "user-sibling-app";
        const string otherHandsetAccount = "user-other-handset";
        const string loggedOutAccount = "user-logged-out";
        var loggedOutOn = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        var harness = new Harness();
        await TestMethod<bool>(
            setup: harness.Setup,
            arrange: async context =>
            {
                SeedAccounts(context,
                    (NextAccount, TestTenants.Default),
                    (siblingAppAccount, TestTenants.Second),
                    (otherHandsetAccount, TestTenants.Default),
                    (loggedOutAccount, TestTenants.Default));
                AddDevice(context, siblingAppAccount, TestTenants.Second, Handset, "fcm-token-sibling-app-install");
                AddDevice(context, otherHandsetAccount, TestTenants.Default, "android-id-other-handset", InstallToken);
                AddDevice(context, loggedOutAccount, TestTenants.Default, Handset, InstallToken)
                    .Deactivated(loggedOutAccount, loggedOutOn);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await harness.RegisterAsync(provider, NextAccount, TestTenants.Default, Handset, InstallToken);
                return true;
            },
            assert: async (context, _) =>
            {
                var rows = await context.Devices.IgnoreQueryFilters().ToListAsync();
                Assert.True(Assert.Single(rows, d => d.UserId == siblingAppAccount).IsActive);
                Assert.True(Assert.Single(rows, d => d.UserId == otherHandsetAccount).IsActive);

                var loggedOut = Assert.Single(rows, d => d.UserId == loggedOutAccount);
                Assert.False(loggedOut.IsActive);
                Assert.Equal(loggedOutOn, loggedOut.DeactivatedOn);
                Assert.Equal(loggedOutAccount, loggedOut.DeactivatedBy);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_TokenLess_Registration_Retires_No_Other_Accounts_Row()
    {
        var harness = new Harness();
        await TestMethod<bool>(
            setup: harness.Setup,
            arrange: async context =>
            {
                SeedAccounts(context,
                    (NextAccount, TestTenants.Default),
                    (PreviousAccountOtherCompany, TestTenants.Second));
                AddDevice(context, PreviousAccountOtherCompany, TestTenants.Second, Handset, InstallToken);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await harness.RegisterAsync(provider, NextAccount, TestTenants.Default, Handset, token: null);
                return true;
            },
            assert: async (context, _) =>
            {
                var rows = await context.Devices.IgnoreQueryFilters().ToListAsync();
                var previous = Assert.Single(rows, d => d.UserId == PreviousAccountOtherCompany);
                Assert.True(previous.IsActive);
                Assert.Null(previous.DeactivatedOn);

                var next = Assert.Single(rows, d => d.UserId == NextAccount);
                Assert.True(next.IsActive);
                Assert.Equal(string.Empty, next.DeviceToken);
            },
            transactional: false);
    }

    private static string OrderOf(string userId) => $"order-{userId}";

    private static string UpdateTokenOf(string userId, string deviceId) => $"apns-update-{userId}-{deviceId}";

    private static void SeedAccounts(CleansiaDbContext context, params (string UserId, string TenantId)[] accounts)
    {
        context.Languages.Add(Language.Create("en", "English"));
        foreach (var (userId, tenantId) in accounts)
        {
            var user = User.CreateWithPassword($"{userId}@cleansia.test", "Seed-Password-123", "Handset", "Owner");
            user.Id = userId;
            user.TenantId = tenantId;
            context.Users.Add(user);
        }
    }

    private static Device AddDevice(
        CleansiaDbContext context, string userId, string tenantId, string deviceId, string token, string platform = "android")
    {
        var device = Device.Create(userId, platform, token, deviceId);
        device.TenantId = tenantId;
        context.Devices.Add(device);
        return device;
    }

    // ActivityKit issues the push-to-start token per install, so every account signed in on one iPhone registers the same one.
    private static void AddActivityTokens(
        CleansiaDbContext context, string userId, string tenantId, string deviceId, string pushToStartToken)
    {
        context.LiveActivityTokens.Add(LiveActivityToken.Create(userId, deviceId, orderId: null, pushToStartToken, tenantId, false));
        context.LiveActivityTokens.Add(
            LiveActivityToken.Create(userId, deviceId, OrderOf(userId), UpdateTokenOf(userId, deviceId), tenantId, true));
    }

    private sealed class Harness
    {
        private static readonly JsonSerializerOptions WireJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private readonly Mock<IPushDispatcher> _dispatcher = new();
        private readonly Mock<ILiveActivityPushClient> _activityPushClient = new();
        private IUserSessionProvider _session = new TestUserSessionProvider();

        public List<(string OrderId, string[] Tokens)> Sent { get; } = [];

        public List<string> ActivityPushes { get; } = [];

        public Task Setup(IServiceCollection services)
        {
            services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => _session));
            _dispatcher
                .Setup(d => d.SendAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback((IReadOnlyList<string> tokens, string _, IReadOnlyDictionary<string, string> data, CancellationToken _) =>
                    Sent.Add((data["orderId"], tokens.ToArray())))
                .ReturnsAsync(new PushDispatchResult(1, 0, []));
            services.Replace(ServiceDescriptor.Singleton(_dispatcher.Object));
            _activityPushClient
                .Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<LiveActivityPush>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
                .Callback((string token, LiveActivityPush _, bool? _, CancellationToken _) => ActivityPushes.Add(token))
                .ReturnsAsync(LiveActivityPushResult.Sent());
            services.Replace(ServiceDescriptor.Singleton(_activityPushClient.Object));
            return Task.CompletedTask;
        }

        public async Task RegisterAsync(
            IServiceProvider provider, string userId, string tenantId, string deviceId, string? token, string platform = "android")
        {
            _session = new TestUserSessionProvider(userId, $"{userId}@cleansia.test");
            await using var scope = provider.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(tenantId);

            var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new RegisterDevice.Command(deviceId, token, platform));

            Assert.True(result.IsSuccess, result.Error?.Message);
        }

        public async Task SendLiveActivityAsync(IServiceProvider provider, string userId, string tenantId, string eventKey)
        {
            _session = new TestUserSessionProvider();
            await using var scope = provider.CreateAsyncScope();

            var orderId = OrderOf(userId);
            var now = DateTimeOffset.UtcNow;
            var message = new SendLiveActivityUpdateMessage(
                userId, orderId, eventKey, "CL-1001", now, now.AddHours(2), now, tenantId);
            var body = JsonSerializer.Serialize(
                new QueueEnvelope<SendLiveActivityUpdateMessage>(MessageKeys.LiveActivity(orderId, eventKey, 1), tenantId, message),
                WireJson);

            var guard = new DbIdempotencyGuard(new ProcessedMessageRepository(scope.ServiceProvider.GetRequiredService<CleansiaDbContext>()));
            await ActivatorUtilities.CreateInstance<SendLiveActivityUpdateHandler>(scope.ServiceProvider, guard)
                .HandleAsync(body, CancellationToken.None);
        }

        public async Task PushAsync(IServiceProvider provider, string userId, string tenantId)
        {
            _session = new TestUserSessionProvider();
            await using var scope = provider.CreateAsyncScope();

            var orderId = OrderOf(userId);
            var message = new SendPushNotificationMessage(userId, NotificationEventCatalog.OrderCompleted,
                new Dictionary<string, string> { ["orderId"] = orderId, ["orderNumber"] = "CL-1001" }, tenantId);
            var body = JsonSerializer.Serialize(
                new QueueEnvelope<SendPushNotificationMessage>(MessageKeys.Push(userId, message.EventKey, orderId), tenantId, message),
                WireJson);

            var guard = new DbIdempotencyGuard(new ProcessedMessageRepository(scope.ServiceProvider.GetRequiredService<CleansiaDbContext>()));
            await ActivatorUtilities.CreateInstance<SendPushNotificationHandler>(scope.ServiceProvider, guard)
                .HandleAsync(body, CancellationToken.None);
        }
    }
}
