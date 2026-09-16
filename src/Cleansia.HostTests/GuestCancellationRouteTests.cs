using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.HostTests.Infrastructure;
using Cleansia.Infra.Common.Validations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests;

public class GuestCancellationRouteTests(HostTestPostgresFixture fixture) : AuthzHostTestBase(fixture)
{
    private const string Email = "guest-route@example.test";
    private const string CancelPath = "/api/Order/CancelGuest";
    private const string PreviewPath = "/api/Order/GuestCancellationPreview";

    private async Task<CancelGuestOrder.Command> SeedGuest(bool owned = false, OrderStatus status = OrderStatus.New)
    {
        CancelGuestOrder.Command command = null!;
        await SeedAsync(async db =>
        {
            await DomainSeed.EnsureReferenceDataAsync(db);
            var customer = owned ? DomainSeed.Customer(Email) : null;
            if (customer is not null) db.Users.Add(customer);
            var order = Order.Create("Guest", Email, "+421900123456",
                Address.Create("Route Street", "Bratislava", "81101", DomainSeed.CountryId), 1, 1,
                DateTime.UtcNow.AddDays(2), PaymentType.Cash, 100m, DomainSeed.CurrencyId,
                PaymentStatus.Pending, userId: customer?.Id);
            order.TenantId = HostTestTenants.B;
            order.CustomerAddress!.TenantId = HostTestTenants.B;
            var track = OrderStatusTrack.Create(status, order);
            track.TenantId = HostTestTenants.B;
            order.AddOrderStatus(track);
            db.Orders.Add(order);
            command = new(order.DisplayOrderNumber, Email, order.ConfirmationCode);
        });
        return command;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Both_customer_hosts_accept_lookup_secrets_in_the_body_and_preserve_get(bool mobile)
    {
        var command = await SeedGuest();
        using var mobileHost = new HostTestApplicationFactory<Cleansia.Web.Mobile.Customer.Program>(Db.ConnectionString);
        using var client = mobile ? mobileHost.CreateClient() : CustomerClientAnonymous();
        var query = new LookupOrder.Query(command.DisplayOrderNumber, command.Email, command.ConfirmationCode);
        var response = await client.PostAsJsonAsync("/api/Order/Lookup", query);
        HttpAssert.IsOk(response);
        var body = await response.Content.ReadFromJsonAsync<LookupOrder.Response>();
        Assert.Equal(command.DisplayOrderNumber, body!.DisplayOrderNumber);
        Assert.Equal(command.ConfirmationCode, body.ConfirmationCode);
        foreach (var wrong in new[] {
            query with { Email = "wrong@example.test" },
            query with { ConfirmationCode = "wrong" },
            query with { DisplayOrderNumber = "missing" }
        })
        {
            var refused = await client.PostAsJsonAsync("/api/Order/Lookup", wrong);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            await HttpAssert.AssertBusinessErrorAsync(refused, BusinessErrorMessage.OrderNotFound);
        }
        var legacy = await client.GetAsync($"/api/Order/Lookup?orderNumber={Uri.EscapeDataString(query.DisplayOrderNumber)}&email={Uri.EscapeDataString(query.Email)}&confirmationCode={Uri.EscapeDataString(query.ConfirmationCode)}");
        HttpAssert.IsOk(legacy);
        Assert.Equal(0, await QueryAsync(db => db.OutboxMessages.IgnoreQueryFilters().CountAsync()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Both_customer_hosts_allow_anonymous_preview_and_cancel_in_the_orders_operator(bool mobile)
    {
        var command = await SeedGuest();
        using var mobileHost = new HostTestApplicationFactory<Cleansia.Web.Mobile.Customer.Program>(Db.ConnectionString);
        using var client = mobile ? mobileHost.CreateClient() : CustomerClientAnonymous();
        var preview = await client.PostAsJsonAsync(PreviewPath, new GetGuestCancellationFeePreview.Query(
            command.DisplayOrderNumber, command.Email, command.ConfirmationCode));
        HttpAssert.IsOk(preview);
        Assert.Equal(OrderStatus.New, await QueryAsync(db => db.Orders.IgnoreQueryFilters().Select(x => x.CurrentStatus).SingleAsync()));
        Assert.Equal(0, await QueryAsync(db => db.OutboxMessages.IgnoreQueryFilters().CountAsync()));
        var cancelled = await client.PostAsJsonAsync(CancelPath, command);
        HttpAssert.IsOk(cancelled);
        var state = await QueryAsync(db => db.Orders.IgnoreQueryFilters().SingleAsync());
        Assert.Equal(OrderStatus.Cancelled, state.CurrentStatus);
        Assert.Equal(CancelledBy.Customer, state.CancelledBy);
        var audit = Assert.Single(await QueryAsync(db => db.CustomerActionAudits.IgnoreQueryFilters().ToListAsync()));
        Assert.True(audit.Success);
        Assert.Null(audit.UserId);
        Assert.Equal(state.Id, audit.ResourceId);
        Assert.Equal(HostTestTenants.B, audit.TenantId);
        Assert.NotNull(audit.PayloadJson);
        Assert.All(await QueryAsync(db => db.OrderStatusHistory.IgnoreQueryFilters().ToListAsync()), x => Assert.Equal(HostTestTenants.B, x.TenantId));
        var email = Assert.Single(await QueryAsync(db => db.OutboxMessages.IgnoreQueryFilters().Where(x => x.QueueName == QueueNames.SendEmail).ToListAsync()));
        Assert.Equal(HostTestTenants.B, email.TenantId);
        Assert.DoesNotContain(Email, email.Body);
        Assert.Equal(0, await QueryAsync(db => db.Set<UserNotification>().IgnoreQueryFilters().CountAsync()));
    }

    [Theory]
    [InlineData(false, "email")]
    [InlineData(false, "code")]
    [InlineData(false, "number")]
    [InlineData(false, "account")]
    [InlineData(true, "email")]
    [InlineData(true, "code")]
    [InlineData(true, "number")]
    [InlineData(true, "account")]
    public async Task Wrong_keys_and_account_orders_are_uniformly_refused_without_mutation(bool mobile, string mismatch)
    {
        var command = await SeedGuest(owned: mismatch == "account");
        command = mismatch switch
        {
            "email" => command with { Email = "other@example.test" },
            "code" => command with { ConfirmationCode = "wrong" },
            "number" => command with { DisplayOrderNumber = "missing" },
            _ => command
        };
        using var mobileHost = new HostTestApplicationFactory<Cleansia.Web.Mobile.Customer.Program>(Db.ConnectionString);
        using var client = mobile ? mobileHost.CreateClient() : CustomerClientAnonymous();
        foreach (var path in new[] { PreviewPath, CancelPath })
        {
            var response = await client.PostAsJsonAsync(path, command);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await HttpAssert.AssertBusinessErrorAsync(response, BusinessErrorMessage.OrderNotFound);
        }
        Assert.Equal(OrderStatus.New, await QueryAsync(db => db.Orders.IgnoreQueryFilters().Select(x => x.CurrentStatus).SingleAsync()));
        Assert.Equal(0, await QueryAsync(db => db.OutboxMessages.IgnoreQueryFilters().CountAsync()));
        var audit = Assert.Single(await QueryAsync(db => db.CustomerActionAudits.IgnoreQueryFilters().ToListAsync()));
        Assert.False(audit.Success);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, audit.ErrorCode);
        Assert.Null(audit.ResourceId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Started_guest_order_is_refused_with_the_same_business_key_on_both_hosts(bool mobile)
    {
        var command = await SeedGuest(status: OrderStatus.InProgress);
        using var mobileHost = new HostTestApplicationFactory<Cleansia.Web.Mobile.Customer.Program>(Db.ConnectionString);
        using var client = mobile ? mobileHost.CreateClient() : CustomerClientAnonymous();
        foreach (var path in new[] { PreviewPath, CancelPath })
        {
            var response = await client.PostAsJsonAsync(path, command);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await HttpAssert.AssertBusinessErrorAsync(response, BusinessErrorMessage.OrderInProgressCannotCancel);
        }
        var audit = Assert.Single(await QueryAsync(db => db.CustomerActionAudits.IgnoreQueryFilters().ToListAsync()));
        Assert.Equal(HostTestTenants.B, audit.TenantId);
        Assert.Null(audit.UserId);
        Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, audit.ErrorCode);
        Assert.Equal(OrderStatus.InProgress, await QueryAsync(db => db.Orders.IgnoreQueryFilters().Select(x => x.CurrentStatus).SingleAsync()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Customer_token_does_not_turn_a_secret_key_guest_action_into_an_account_action(bool refused)
    {
        var command = await SeedGuest(status: refused ? OrderStatus.InProgress : OrderStatus.New);
        using var client = CustomerClient(TestJwtFactory.Mint(CustomerAudience, "unrelated-customer", "account@example.test", UserProfile.Customer));
        var response = await client.PostAsJsonAsync(CancelPath, command);
        Assert.Equal(refused ? HttpStatusCode.BadRequest : HttpStatusCode.OK, response.StatusCode);
        var audit = Assert.Single(await QueryAsync(db => db.CustomerActionAudits.IgnoreQueryFilters().ToListAsync()));
        Assert.Null(audit.UserId);
        Assert.Equal(HostTestTenants.B, audit.TenantId);
        Assert.Equal(!refused, audit.Success);
        Assert.Equal(refused ? BusinessErrorMessage.OrderInProgressCannotCancel : null, audit.ErrorCode);
    }

    [Fact]
    public async Task Guest_lookup_preview_and_cancel_do_not_exist_on_admin_partner_or_mobile_partner()
    {
        var command = await SeedGuest();
        using var admin = AdminClientAnonymous();
        using var partner = PartnerClientAnonymous();
        using var mobilePartner = MobileClientAnonymous();
        foreach (var client in new[] { admin, partner, mobilePartner })
        foreach (var path in new[] { PreviewPath, CancelPath, "/api/Order/Lookup" })
            HttpAssert.IsNotFound(await client.PostAsJsonAsync(path, command));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Guest_preview_and_cancel_share_the_anonymous_auth_rate_window(bool mobile)
    {
        var command = await SeedGuest();
        using var mobileHost = new HostTestApplicationFactory<Cleansia.Web.Mobile.Customer.Program>(Db.ConnectionString);
        using var client = mobile ? mobileHost.CreateClient() : CustomerClientAnonymous();
        const int anonymousAuthAllowance = 10;
        for (var i = 0; i < anonymousAuthAllowance; i++)
            HttpAssert.IsOk(await client.PostAsJsonAsync(PreviewPath, command));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync(CancelPath, command)).StatusCode);
        Assert.Equal(OrderStatus.New, await QueryAsync(db => db.Orders.IgnoreQueryFilters().Select(x => x.CurrentStatus).SingleAsync()));
    }

    [Fact]
    public void Both_customer_route_pairs_explicitly_allow_anonymous_and_use_auth_policy()
    {
        foreach (var controller in new[] { typeof(Cleansia.Web.Customer.Controllers.OrderController), typeof(Cleansia.Web.Mobile.Customer.Controllers.OrderController) })
        foreach (var name in new[] { "CancelGuest", "GuestCancellationPreview" })
        {
            var method = controller.GetMethod(name)!;
            Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Equal("auth", method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        }
    }

    [Theory]
    [InlineData(false, "interactive")]
    [InlineData(true, "auth")]
    public void Both_lookup_methods_keep_the_existing_per_host_anonymous_rate_window(bool mobile, string policy)
    {
        var controller = mobile
            ? typeof(Cleansia.Web.Mobile.Customer.Controllers.OrderController)
            : typeof(Cleansia.Web.Customer.Controllers.OrderController);
        foreach (var name in new[] { "Lookup", "LookupOrder" })
        {
            var method = controller.GetMethod(name)!;
            Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Equal(policy, method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        }
    }
}
