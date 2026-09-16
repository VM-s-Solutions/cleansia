using System.Data.Common;
using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using CustomerActionAuditRow = Cleansia.Core.Domain.Auditing.CustomerActionAudit;

namespace Cleansia.IntegrationTests.Features.Tenancy;

/// <summary>
/// ADR-0064 D3 on Postgres with two operating companies (TC-LC-ARCH-2): once B is frozen for archive
/// the commit refuses every write against its books — an added review, a modified receipt, a deleted
/// photo, a modified credit account — naming B and writing nothing, while the account surface keeps
/// committing (a refresh token, a consent, a customer audit row, a modified user, a modified Plus), A's
/// rows commit untouched, and the law's two writers — an erasure and the retention sweep — write
/// through the gate. The guard's cost is pinned through a command interceptor: a commit touching
/// nothing stamped or only the account surface asks the registry nothing, and two commits on one
/// context ask once.
/// </summary>
[Collection("PostgresCollection")]
public sealed class ArchivedCompanyWriteGuardTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string A = TestTenants.Default;
    private const string B = TestTenants.Second;
    private const string AdminBId = "01ARCH-ADMIN-B-00000000000";
    private const string SvkId = "country-svk-archguard";
    private const string CzeId = "country-cze-archguard";
    private const string EurId = "currency-eur-archguard";
    private const string CzkId = "currency-czk-archguard";
    private const string PlanId = "plan-archguard";
    private static readonly DateTimeOffset FrozenOn = new(2026, 9, 16, 8, 30, 0, TimeSpan.Zero);

    private sealed record Seeded(
        string CustomerBId,
        string CustomerAId,
        string OrderBId,
        string OldOrderBId,
        string ReceiptBId,
        string PhotoBId,
        string CreditAccountBId,
        string MembershipBId,
        string OrderAId);

    private Task SetupAsync(IServiceCollection services)
    {
        services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
        services.Replace(ServiceDescriptor.Singleton(_ => new Mock<IBlobContainerClientFactory>().Object));
        services.Replace(ServiceDescriptor.Singleton(_ => Mock.Of<IStripeClient>()));
        return Task.CompletedTask;
    }

    private static async Task<T> InScopeAsync<T>(IServiceProvider provider, string tenantId, Func<IServiceProvider, CleansiaDbContext, Task<T>> act)
    {
        using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(tenantId);
        return await act(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<CleansiaDbContext>());
    }

    private static Task<CompanyArchivedException> RefusedAsync(IServiceProvider provider, string tenantId, Func<CleansiaDbContext, Task> change) =>
        InScopeAsync(provider, tenantId, async (_, ctx) =>
        {
            await change(ctx);
            return await Assert.ThrowsAsync<CompanyArchivedException>(() => ctx.CommitAsync(CancellationToken.None));
        });

    private static Task<bool> CommitsAsync(IServiceProvider provider, string tenantId, Func<CleansiaDbContext, Task> change) =>
        InScopeAsync(provider, tenantId, async (_, ctx) =>
        {
            await change(ctx);
            await ctx.CommitAsync(CancellationToken.None);
            return true;
        });

    [Fact]
    public async Task A_Frozen_Companys_Books_Refuse_Every_Write_While_The_Account_Surface_The_Other_Company_And_The_Law_Still_Write()
    {
        Seeded seeded = default!;
        await TestMethod(
            setup: SetupAsync,
            arrange: async ctx =>
            {
                seeded = await SeedTwoCompaniesAsync(ctx);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var review = await RefusedAsync(provider, B, async ctx =>
                {
                    var order = await ctx.Orders.SingleAsync(o => o.Id == seeded.OrderBId);
                    order.AddReview(OrderReview.Create(order.Id, seeded.CustomerBId, 5, "spotless"));
                });
                var receipt = await RefusedAsync(provider, B, async ctx =>
                    (await ctx.OrderReceipts.SingleAsync(r => r.Id == seeded.ReceiptBId)).MarkEmailSent("msg-1"));
                var photo = await RefusedAsync(provider, B, async ctx =>
                    ctx.Set<OrderPhoto>().Remove(await ctx.Set<OrderPhoto>().SingleAsync(p => p.Id == seeded.PhotoBId)));
                var credit = await RefusedAsync(provider, B, async ctx =>
                    (await ctx.CreditAccounts.SingleAsync(a => a.Id == seeded.CreditAccountBId))
                        .Issue(50m, CreditTransactionReason.Goodwill, "archguard:goodwill", AdminBId));

                var surface = new[]
                {
                    await CommitsAsync(provider, B, ctx =>
                    {
                        ctx.RefreshTokens.Add(RefreshToken.Create(seeded.CustomerBId, "hash-archguard", DateTimeOffset.UtcNow.AddDays(1), "cleansia.customer", null, null));
                        return Task.CompletedTask;
                    }),
                    await CommitsAsync(provider, B, ctx =>
                    {
                        ctx.UserConsents.Add(UserConsent.Grant(seeded.CustomerBId, ConsentType.MarketingEmails, null, null, "v1"));
                        return Task.CompletedTask;
                    }),
                    await CommitsAsync(provider, B, ctx =>
                    {
                        var failureRow = CustomerActionAuditRow.Create(
                            userId: seeded.CustomerBId, clientAudience: "cleansia.customer", ipAddress: null, deviceLabel: null, deviceId: null,
                            action: "customer.test.act", resourceType: "Order", resourceId: seeded.OrderBId, success: false,
                            errorCode: "tenant.archived", payloadJson: null, correlationId: null);
                        failureRow.TenantId = B;
                        ctx.CustomerActionAudits.Add(failureRow);
                        return Task.CompletedTask;
                    }),
                    await CommitsAsync(provider, B, async ctx =>
                        (await ctx.Users.SingleAsync(u => u.Id == seeded.CustomerBId)).UpdatePhoneNumber("+421900111222")),
                    await CommitsAsync(provider, B, async ctx =>
                        (await ctx.UserMemberships.SingleAsync(m => m.Id == seeded.MembershipBId)).MarkCancellationRequested()),
                    await CommitsAsync(provider, A, async ctx =>
                    {
                        var order = await ctx.Orders.SingleAsync(o => o.Id == seeded.OrderAId);
                        order.AddReview(OrderReview.Create(order.Id, seeded.CustomerAId, 4, "fine"));
                    }),
                };

                var erasure = await InScopeAsync(provider, B, async (services, ctx) =>
                {
                    var result = await services.GetRequiredService<IGdprDeletionService>().DeleteUserAccountAsync(
                        seeded.CustomerBId, "archguard-erasure", _ => ("test-actor", null), deferEmployeeErasure: false, CancellationToken.None);
                    await services.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
                    return result;
                });

                await InScopeAsync(provider, B, async (services, _) =>
                {
                    await services.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                    return true;
                });

                return (Review: review, Receipt: receipt, Photo: photo, Credit: credit, Surface: surface, Erasure: erasure);
            },
            assert: async (ctx, refusals) =>
            {
                foreach (var refusal in new[] { refusals.Review, refusals.Receipt, refusals.Photo, refusals.Credit })
                {
                    Assert.Equal(B, refusal.TenantId);
                }

                Assert.All(refusals.Surface, committed => Assert.True(committed));
                Assert.True(refusals.Erasure.IsSuccess);

                Assert.Equal(0, await ctx.OrderReviews.IgnoreQueryFilters().CountAsync(r => r.OrderId == seeded.OrderBId));
                Assert.False((await ctx.OrderReceipts.IgnoreQueryFilters().SingleAsync(r => r.Id == seeded.ReceiptBId)).EmailSent);
                Assert.Equal(1, await ctx.Set<OrderPhoto>().IgnoreQueryFilters().CountAsync(p => p.Id == seeded.PhotoBId));
                Assert.Equal(0m, (await ctx.CreditAccounts.IgnoreQueryFilters().SingleAsync(a => a.Id == seeded.CreditAccountBId)).Balance);

                Assert.Equal(1, await ctx.RefreshTokens.IgnoreQueryFilters().CountAsync(t => t.UserId == seeded.CustomerBId && t.TenantId == B));
                Assert.Equal(1, await ctx.UserConsents.IgnoreQueryFilters().CountAsync(c => c.UserId == seeded.CustomerBId && c.ConsentType == ConsentType.MarketingEmails));
                Assert.Equal(1, await ctx.CustomerActionAudits.IgnoreQueryFilters().CountAsync(a => a.ResourceId == seeded.OrderBId));
                Assert.NotNull((await ctx.UserMemberships.IgnoreQueryFilters().SingleAsync(m => m.Id == seeded.MembershipBId)).CancelledAt);
                Assert.Equal(1, await ctx.OrderReviews.IgnoreQueryFilters().CountAsync(r => r.OrderId == seeded.OrderAId));

                // The erasure pseudonymised B's customer and the B order they own; the sweep blanked the
                // two-year-old completed guest order under B's own one-year window and left A's alone.
                var customer = await ctx.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == seeded.CustomerBId);
                Assert.EndsWith("@anonymized.local", customer.Email);
                var orders = await ctx.Orders.IgnoreQueryFilters().ToDictionaryAsync(o => o.Id);
                Assert.Equal(AnonymizationMarker.Value, orders[seeded.OrderBId].CustomerName);
                Assert.Null(orders[seeded.OrderBId].UserId);
                Assert.Equal(AnonymizationMarker.Value, orders[seeded.OldOrderBId].CustomerName);
                Assert.Equal("Arch Guard", orders[seeded.OrderAId].CustomerName);
            },
            transactional: false);
    }

    /// <summary>
    /// The registry read is named here as the Postgres command interceptor counting statements that
    /// select from <c>"Tenants"</c>: none for a commit that touches nothing stamped or only the account
    /// surface, one for the first books commit on a context and none for the second.
    /// </summary>
    [Fact]
    public async Task The_Guard_Asks_The_Registry_Once_Per_Context_And_Never_For_The_Account_Surface()
    {
        Seeded seeded = default!;
        await TestMethod(
            arrange: async ctx =>
            {
                seeded = await SeedTwoCompaniesAsync(ctx);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async _ =>
            {
                var counter = new TenantsReadCounter();
                var options = new DbContextOptionsBuilder<CleansiaDbContext>()
                    .UseNpgsql(Fixture.GetConnectionString())
                    .AddInterceptors(counter)
                    .Options;
                var tenantProvider = new TenantProvider(new HttpContextAccessor());
                tenantProvider.SetTenantOverride(A);
                await using var context = new CleansiaDbContext(options, new TestUserSessionProvider("system", "system@cleansia.test"), tenantProvider, new ArchiveWriteGate());

                context.RefreshTokens.Add(RefreshToken.Create(seeded.CustomerAId, "hash-archguard-a", DateTimeOffset.UtcNow.AddDays(1), "cleansia.customer", null, null));
                await context.CommitAsync(CancellationToken.None);
                var afterAccountSurface = counter.TenantsReads;

                var order = await context.Orders.SingleAsync(o => o.Id == seeded.OrderAId);
                order.AddReview(OrderReview.Create(order.Id, seeded.CustomerAId, 5, "first"));
                await context.CommitAsync(CancellationToken.None);
                var afterFirstBooksCommit = counter.TenantsReads;

                order.AddReview(OrderReview.Create(order.Id, "someone-else", 3, "second"));
                await context.CommitAsync(CancellationToken.None);
                var afterSecondBooksCommit = counter.TenantsReads;

                return (afterAccountSurface, afterFirstBooksCommit, afterSecondBooksCommit);
            },
            assert: (_, reads) =>
            {
                Assert.Equal((0, 1, 1), reads);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    private static async Task<Seeded> SeedTwoCompaniesAsync(CleansiaDbContext ctx)
    {
        var english = Language.Create("en", "English");
        ctx.Languages.Add(english);
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = EurId;
        eur.IsActive = true;
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = CzkId;
        czk.IsActive = true;
        czk.SetAsDefault(true);
        ctx.Currencies.AddRange(eur, czk);
        var slovakia = Country.Create("Slovakia", "SVK", "SK", isServiced: true);
        slovakia.Id = SvkId;
        var czechia = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        czechia.Id = CzeId;
        ctx.Countries.AddRange(slovakia, czechia);
        ctx.CountryConfigurations.AddRange(
            CountryConfiguration.Create(SvkId, "EUR", "sk", 0.20m, timeZoneId: "Europe/Bratislava").AssignOperator(B),
            CountryConfiguration.Create(CzeId, "CZK", "cs", 0.21m, timeZoneId: "Europe/Prague").AssignOperator(A).SetAsDefaultMarket(true));
        var plan = MembershipPlan.Create(PlanId, "Archive plan", 10m, 24, allowsExpressUpgrade: false);
        plan.Id = PlanId;
        ctx.MembershipPlans.Add(plan);

        var registry = await ctx.Tenants.SingleAsync(t => t.Id == B);
        registry.RequestWindDown(new DateOnly(2026, 8, 1), AdminBId, FrozenOn.AddDays(-60));
        registry.Deactivate(AdminBId, FrozenOn.AddDays(-45));
        registry.RequestArchive(AdminBId, FrozenOn);

        var window = TenantConfiguration.Create(RetentionDefaults.OrderPiiYearsKey, "1");
        window.TenantId = B;
        ctx.TenantConfigurations.Add(window);

        var customerB = Stamped(NewUser("archguard-customer-b@cleansia.test"), B);
        var customerA = Stamped(NewUser("archguard-customer-a@cleansia.test"), A);
        var cleanerUserB = Stamped(NewUser("archguard-cleaner-b@cleansia.test", UserProfile.Employee), B);
        ctx.Users.AddRange(customerB, customerA, cleanerUserB);
        var cleanerB = Employee.CreateWithUser(cleanerUserB).Approve(AdminBId);
        cleanerB.TenantId = B;
        ctx.Add(cleanerB);

        var orderB = NewOrder("archguard-b-completed", SvkId, EurId, customerB.Id, DateTime.UtcNow.AddDays(-30), B);
        orderB.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, orderB));
        // A guest booking two years old: not the customer's, so the erasure leaves it and only the
        // sweep can blank it.
        var oldOrderB = NewOrder("archguard-b-two-years", SvkId, EurId, userId: null, DateTime.UtcNow.AddYears(-2), B);
        oldOrderB.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, oldOrderB));
        var orderA = NewOrder("archguard-a-completed", CzeId, CzkId, customerA.Id, DateTime.UtcNow.AddDays(-30), A);
        orderA.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, orderA));
        ctx.Orders.AddRange(orderB, oldOrderB, orderA);

        var receipt = Stamped(OrderReceipt.Create(orderB.Id, "RCP-2026-0001", "RCP-2026-0001.pdf", "b/RCP-2026-0001.pdf", english.Id), B);
        ctx.OrderReceipts.Add(receipt);
        var photo = Stamped(OrderPhoto.Create(orderB.Id, PhotoType.After, "https://blobs.test/order-photos/after.jpg", "after.jpg", "after.jpg", 2048, "image/jpeg", cleanerB.Id), B);
        ctx.Set<OrderPhoto>().Add(photo);
        var credit = Stamped(CreditAccount.Create(customerB.Id, EurId, "seed"), B);
        ctx.CreditAccounts.Add(credit);
        var membership = Stamped(UserMembership.Create(customerB.Id, PlanId, EurId, "sub_archguard", DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(27)), B);
        ctx.UserMemberships.Add(membership);

        StampUnstampedAdded(ctx, B);
        return new Seeded(customerB.Id, customerA.Id, orderB.Id, oldOrderB.Id, receipt.Id, photo.Id, credit.Id, membership.Id, orderA.Id);
    }

    private static T Stamped<T>(T entity, string tenantId) where T : Core.Domain.Common.ITenantEntity
    {
        entity.TenantId = tenantId;
        return entity;
    }

    private static User NewUser(string email, UserProfile profile = UserProfile.Customer)
    {
        var user = User.CreateWithPassword(email, "12345678Test!", "Arch", "Guard", profile);
        user.ConfirmEmail();
        return user;
    }

    private static Order NewOrder(string id, string countryId, string currencyId, string? userId, DateTime cleaningAt, string tenantId)
    {
        var address = Address.Create("Hlavna 1", "Bratislava", "81101", countryId);
        address.TenantId = tenantId;
        var order = Order.Create(
            customerName: "Arch Guard",
            customerEmail: $"{id}@cleansia.test",
            customerPhone: "+421900000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: cleaningAt,
            paymentType: PaymentType.Cash,
            totalPrice: 100m,
            currencyId: currencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: userId);
        order.Id = id;
        order.TenantId = tenantId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }

    private sealed class TenantsReadCounter : DbCommandInterceptor
    {
        public int TenantsReads { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM \"Tenants\"", StringComparison.Ordinal))
            {
                TenantsReads++;
            }

            return ValueTask.FromResult(result);
        }
    }
}
