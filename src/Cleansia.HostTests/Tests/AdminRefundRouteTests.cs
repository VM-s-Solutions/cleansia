using System.Text.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The refunds list on the Admin host: any administrator who reads the order detail reads their own company's
/// refunds, filtered by state; an accountant and a customer are refused.
/// </summary>
public sealed class AdminRefundRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string GetPagedRoute = "/api/AdminRefund/get-paged";
    private const string SupportId = "refund-support";
    private const string AccountantId = "refund-accountant";

    private sealed record Seeded(string NotSentId, string RefundedId);

    private async Task<Seeded> ArrangeAsync()
    {
        Seeded seeded = null!;
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var support = DomainSeed.Admin($"{SupportId}@hosttests.local", role: AdminRole.Support);
            support.Id = SupportId;
            var accountant = DomainSeed.Admin($"{AccountantId}@hosttests.local", role: AdminRole.Accountant);
            accountant.Id = AccountantId;
            var customer = DomainSeed.Customer("refunded@hosttests.local");
            ctx.Users.AddRange(support, accountant, customer);

            var order = DomainSeed.NewOrder(customer.Id, customer.Email, paymentType: PaymentType.Card, paymentStatus: PaymentStatus.Paid);
            ctx.Orders.Add(order);
            var notSent = Refund.Create(order.Id, $"refund:{order.Id}:admin:a", 300m, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund);
            var refunded = Refund.Create(order.Id, $"refund:{order.Id}:admin:b", 200m, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund)
                .MarkSucceeded("re_b", DateTimeOffset.UtcNow);
            ctx.Refunds.AddRange(notSent, refunded);
            seeded = new Seeded(notSent.Id, refunded.Id);
        });
        return seeded;
    }

    private HttpClient Admin(string userId, AdminRole role) =>
        AdminClient(TestJwtFactory.Mint(
            AdminAudience, userId, $"{userId}@hosttests.local", UserProfile.Administrator, adminRole: role));

    [Fact]
    public async Task Anonymous_Is_401_And_A_Customer_And_An_Accountant_Are_403()
    {
        await ArrangeAsync();
        var customer = AdminClient(TestJwtFactory.Mint(AdminAudience, "cust-1", "cust-1@hosttests.local", UserProfile.Customer));

        HttpAssert.IsUnauthorized(await AdminClientAnonymous().GetAsync(GetPagedRoute));
        HttpAssert.IsForbidden(await customer.GetAsync(GetPagedRoute));
        HttpAssert.IsForbidden(await Admin(AccountantId, AdminRole.Accountant).GetAsync(GetPagedRoute));
    }

    [Fact]
    public async Task Support_Lists_The_Refunds_Filtered_By_State()
    {
        var s = await ArrangeAsync();

        var response = await Admin(SupportId, AdminRole.Support).GetAsync(
            $"{GetPagedRoute}?Filter.States={(int)RefundState.NotSent}&Filter.States={(int)RefundState.Failed}");

        HttpAssert.IsOk(response);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var row = Assert.Single(body.GetProperty("data").EnumerateArray());
        Assert.Equal(s.NotSentId, row.GetProperty("id").GetString());
        Assert.Equal(nameof(RefundState.NotSent), row.GetProperty("state").GetProperty("name").GetString());
        Assert.True(row.GetProperty("canRetry").GetBoolean());
    }
}
