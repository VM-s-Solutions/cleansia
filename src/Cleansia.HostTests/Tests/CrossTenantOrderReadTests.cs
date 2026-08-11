using System.Net;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.HostTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The two browse-gated order READS — <c>GET /api/Order/GetById</c> and <c>GET /api/Order/GetPhotos</c> —
/// pinned across the tenant boundary. AC9 covers user-by-id, dispute-create, invoice-by-id and an order
/// WRITE; nothing covered these two, so their tenancy was held by the EF global query filter alone.
///
/// <para>The filter's middle disjunct (<c>currentTenantId == null &amp;&amp; e.TenantId == null</c>) matches
/// everything in single-tenant mode, which is production today — so a test seeding null-tenant rows
/// proves nothing here. Every row below carries a real, explicit tenant.</para>
///
/// <para>Each attacker is handed everything except the tenant: an approved, complete-profile cleaner
/// whose remaining gate would pass if the row were visible (a free seat and no preferred hold for the
/// detail's browse gate, an actual assignment row for the photos' strict gate). So the refusal can only
/// be the tenant boundary. Each has a positive twin that moves the caller into the order's tenant and
/// changes nothing else, which is what stops these from passing against a route that refuses everyone.</para>
///
/// <para>Two repository-seam tests follow the four route tests because the route alone cannot pin the
/// handler's read: the tenant-filtered <c>ExistsAsync</c> in each feature's <c>Validator</c> refuses
/// first, so a regression in <c>OrderRepository.GetByIdAsync</c> or
/// <c>OrderPhotoRepository.GetPhotosByOrderIdAsync</c> is invisible on the wire until the validator
/// breaks too.</para>
/// </summary>
public sealed class CrossTenantOrderReadTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";

    [Fact]
    public async Task Cross_tenant_get_order_by_id_is_refused_by_the_existence_validator()
    {
        var world = await SeedBrowsableOrderAsync(callerTenant: TenantB);

        var resp = await PartnerClient(world.CallerToken)
            .GetAsync($"/api/Order/GetById?OrderId={world.OrderId}");

        await AssertRefusedAsMissingOrderAsync(resp);
    }

    [Fact]
    public async Task Same_tenant_cleaner_reads_the_order_detail()
    {
        var world = await SeedBrowsableOrderAsync(callerTenant: TenantA);

        var resp = await PartnerClient(world.CallerToken)
            .GetAsync($"/api/Order/GetById?OrderId={world.OrderId}");

        HttpAssert.IsOk(resp);

        // Id only. A same-tenant cleaner who has not taken the job is admitted by the browse gate but
        // served a PII-redacted body; asserting on any customer field would pin the redaction rule
        // rather than reachability, and would flip the day entitlement changes.
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal(world.OrderId, doc.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Cross_tenant_get_order_photos_is_refused_and_leaks_no_photo()
    {
        var world = await SeedAssignedOrderWithPhotosAsync(callerTenant: TenantB);

        var resp = await PartnerClient(world.CallerToken)
            .GetAsync($"/api/Order/GetPhotos?OrderId={world.OrderId}");

        await AssertRefusedAsMissingOrderAsync(resp);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain(BeforePhotoFileName, body);
        Assert.DoesNotContain(AfterPhotoFileName, body);
    }

    [Fact]
    public async Task Same_tenant_assigned_cleaner_reads_the_order_photos()
    {
        var world = await SeedAssignedOrderWithPhotosAsync(callerTenant: TenantA);

        var resp = await PartnerClient(world.CallerToken)
            .GetAsync($"/api/Order/GetPhotos?OrderId={world.OrderId}");

        HttpAssert.IsOk(resp);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("beforePhotoCount").GetInt32());
        Assert.Equal(1, root.GetProperty("afterPhotoCount").GetInt32());

        var served = root.GetProperty("photos").EnumerateArray()
            .Select(p => p.GetProperty("fileName").GetString())
            .ToList();
        Assert.Contains(BeforePhotoFileName, served);
        Assert.Contains(AfterPhotoFileName, served);
    }

    /// <summary>
    /// The route tests above stop at the OUTERMOST gate: both handlers are fronted by a
    /// <c>Validator</c> whose <c>ExistsAsync</c> is itself tenant-filtered, so it short-circuits before
    /// the handler's own read ever runs. That is defence in depth, and it means the handler's
    /// <c>GetByIdAsync</c> could be pointed at <c>IgnoreQueryFilters()</c> without any route test
    /// noticing. These two pin the reads themselves — including the fact that the browse-gated read does
    /// NOT use the <c>…IgnoringTenant</c> sibling that sits right next to it — through the host's real
    /// DI scope, real <c>CleansiaDbContext</c> and real <c>TenantProvider</c>.
    /// </summary>
    [Fact]
    public async Task Order_by_id_read_is_tenant_filtered_and_is_not_the_ignoring_tenant_sibling()
    {
        var world = await SeedBrowsableOrderAsync(callerTenant: TenantA);

        await InTenantScopeAsync(TenantB, async sp =>
        {
            var repo = sp.GetRequiredService<IOrderRepository>();
            Assert.Null(await repo.GetByIdAsync(world.OrderId, CancellationToken.None));
            Assert.False(await repo.ExistsAsync(world.OrderId, CancellationToken.None));

            // The row is there; it is the filter that hides it. Without this the two asserts above pass
            // just as well against a seed that never wrote an order.
            Assert.NotNull(await repo.GetByIdIgnoringTenantAsync(world.OrderId, CancellationToken.None));
        });

        await InTenantScopeAsync(TenantA, async sp =>
        {
            var repo = sp.GetRequiredService<IOrderRepository>();
            Assert.NotNull(await repo.GetByIdAsync(world.OrderId, CancellationToken.None));
            Assert.True(await repo.ExistsAsync(world.OrderId, CancellationToken.None));
        });
    }

    [Fact]
    public async Task Order_photo_reads_are_tenant_filtered()
    {
        var world = await SeedAssignedOrderWithPhotosAsync(callerTenant: TenantA);

        await InTenantScopeAsync(TenantB, async sp =>
        {
            var repo = sp.GetRequiredService<IOrderPhotoRepository>();
            Assert.Empty(await repo.GetPhotosByOrderIdAsync(world.OrderId, CancellationToken.None));
            Assert.Equal(0, await repo.GetPhotoCountByOrderIdAndTypeAsync(
                world.OrderId, PhotoType.Before, CancellationToken.None));
        });

        await InTenantScopeAsync(TenantA, async sp =>
        {
            var repo = sp.GetRequiredService<IOrderPhotoRepository>();
            Assert.Equal(2, (await repo.GetPhotosByOrderIdAsync(world.OrderId, CancellationToken.None)).Count);
            Assert.Equal(1, await repo.GetPhotoCountByOrderIdAndTypeAsync(
                world.OrderId, PhotoType.Before, CancellationToken.None));
        });
    }

    private async Task InTenantScopeAsync(string tenantId, Func<IServiceProvider, Task> body)
    {
        using var scope = PartnerHost.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(tenantId);
        await body(scope.ServiceProvider);
    }

    /// <summary>
    /// A cross-tenant read of an order is 400 + <c>order.not_found</c>, and it is the <c>Validator</c>'s
    /// tenant-filtered <c>ExistsAsync</c> that produces it, not the handler's access gate. The two are
    /// distinguishable on the wire — the validator's failure is a <c>ValidationResult</c>, which
    /// <c>CleansiaApiController.HandleFailure</c> titles "Validation Error" and keys under the
    /// FluentValidation error code, whereas the handler's <c>BusinessResult.Failure</c> is titled
    /// "Bad Request" with <c>type == "OrderId"</c>. Pinning the title records which layer holds the line,
    /// so a change that moves the refusal to the other layer is visible rather than silent.
    /// </summary>
    private static async Task AssertRefusedAsMissingOrderAsync(HttpResponseMessage resp)
    {
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(resp, BusinessErrorMessage.OrderNotFound);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("Validation Error", doc.RootElement.GetProperty("title").GetString());
    }

    private const string BeforePhotoFileName = "xt-before.jpg";
    private const string AfterPhotoFileName = "xt-after.jpg";

    private sealed record SeededWorld(string OrderId, string CallerToken);

    /// <summary>Order in tenant A, fully browsable (New + Cash, one free seat, no preferred hold); the
    /// caller is an approved, complete-profile cleaner in <paramref name="callerTenant"/>.</summary>
    private async Task<SeededWorld> SeedBrowsableOrderAsync(string callerTenant)
    {
        const string callerEmail = "xt-detail-cleaner@hosttests.local";
        string orderId = "", callerEmployeeId = "";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var customer = DomainSeed.Customer("xt-detail-cust@hosttests.local", tenantId: TenantA);
            ctx.Users.Add(customer);
            var order = DomainSeed.NewOrder(customer.Id, "xt-detail-cust@hosttests.local", tenantId: TenantA);
            ctx.Orders.Add(order);

            var callerUser = DomainSeed.EmployeeUser(callerEmail, tenantId: callerTenant);
            ctx.Users.Add(callerUser);
            var caller = DomainSeed.ApprovedEmployee(callerUser, tenantId: callerTenant);
            ctx.Employees.Add(caller);
            ctx.EmployeeDocuments.Add(DomainSeed.ActiveDocument(caller.Id, tenantId: callerTenant));

            orderId = order.Id;
            callerEmployeeId = caller.Id;
        });

        return new SeededWorld(orderId, TestJwtFactory.Mint(
            PartnerAudience, "u-xt-detail", callerEmail, UserProfile.Employee,
            employeeId: callerEmployeeId, tenantId: callerTenant));
    }

    /// <summary>Order in tenant A with a before and an after photo, and the caller ASSIGNED to it. The
    /// photos route gates on the strict <c>CanAccessOrderAsync</c>, so the assignment is what makes the
    /// cross-tenant caller's every non-tenant gate pass — <c>OrderEmployee</c> is not an
    /// <c>ITenantEntity</c>, so the assignment row itself crosses the boundary and only the order's own
    /// filter refuses. It is also what the positive twin needs to be served at all.</summary>
    private async Task<SeededWorld> SeedAssignedOrderWithPhotosAsync(string callerTenant)
    {
        const string callerEmail = "xt-photos-cleaner@hosttests.local";
        string orderId = "", callerEmployeeId = "";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var customer = DomainSeed.Customer("xt-photos-cust@hosttests.local", tenantId: TenantA);
            ctx.Users.Add(customer);
            var order = DomainSeed.NewOrder(customer.Id, "xt-photos-cust@hosttests.local", tenantId: TenantA);
            ctx.Orders.Add(order);

            var callerUser = DomainSeed.EmployeeUser(callerEmail, tenantId: callerTenant);
            ctx.Users.Add(callerUser);
            var caller = DomainSeed.ApprovedEmployee(callerUser, tenantId: callerTenant);
            ctx.Employees.Add(caller);
            ctx.EmployeeDocuments.Add(DomainSeed.ActiveDocument(caller.Id, tenantId: callerTenant));

            DomainSeed.ConfirmAndAssign(order, caller);

            ctx.Set<OrderPhoto>().Add(DomainSeed.OrderPhoto(
                order.Id, caller.Id, PhotoType.Before, BeforePhotoFileName, tenantId: TenantA));
            ctx.Set<OrderPhoto>().Add(DomainSeed.OrderPhoto(
                order.Id, caller.Id, PhotoType.After, AfterPhotoFileName, tenantId: TenantA));

            orderId = order.Id;
            callerEmployeeId = caller.Id;
        });

        return new SeededWorld(orderId, TestJwtFactory.Mint(
            PartnerAudience, "u-xt-photos", callerEmail, UserProfile.Employee,
            employeeId: callerEmployeeId, tenantId: callerTenant));
    }
}
