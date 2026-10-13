using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Functions.Core;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Orders;

[Collection("PostgresCollection")]
public sealed class NewJobsDigestPerCleanerIsolationPostgresTests(PostgresContainerFixture fixture)
    : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task A_Repeated_Employee_Token_Refusal_Does_Not_Hold_Back_Bs_Near_Start_Job()
    {
        var refusal = new RefuseCleanerSave(Fixture.GetConnectionString());
        await TestMethod<int>(
            setup: services =>
            {
                services.AddFunctionsProcessing();
                services.Replace(ServiceDescriptor.Scoped<IEmployeeRepository, OrderedEmployees>());
                services.AddDbContext<CleansiaDbContext>((_, options) => options.AddInterceptors(refusal));
                return Task.CompletedTask;
            },
            arrange: context => SeedAsync(context),
            act: async provider =>
            {
                await provider.GetRequiredService<INewJobsDigestService>().SendDigestsAsync();
                await using (var second = provider.CreateAsyncScope())
                {
                    await second.ServiceProvider.GetRequiredService<INewJobsDigestService>().SendDigestsAsync();
                }

                await using (var check = provider.CreateAsyncScope())
                {
                    var context = check.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                    var cleaners = await context.Employees.IgnoreQueryFilters().AsNoTracking().OrderBy(e => e.Id).ToListAsync();
                    var notices = await context.Set<UserNotification>().IgnoreQueryFilters().AsNoTracking().ToListAsync();
                    var outbox = await context.OutboxMessages.IgnoreQueryFilters().AsNoTracking().ToListAsync();
                    Console.WriteLine(JsonSerializer.Serialize(new { phase = "repeated-refusal", refusal.Attempts,
                        cleaners = cleaners.Select(e => new { e.Id, e.TenantId, e.RegistrationNumber, e.LastNewJobsDigestAt }),
                        notices = notices.Select(n => new { n.UserId, n.TenantId, n.EventKey }),
                        outbox = outbox.Select(o => new { o.MessageKey, o.TenantId }) }));
                    Assert.Null(cleaners.Single(e => e.Id == "digest-employee-a").LastNewJobsDigestAt);
                    Assert.NotNull(cleaners.Single(e => e.Id == "digest-employee-b").LastNewJobsDigestAt);
                    Assert.Equal("digest-user-b", Assert.Single(notices).UserId);
                    Assert.Equal(TestTenants.Second, notices.Single().TenantId);
                    Assert.Equal(TestTenants.Second, Assert.Single(outbox).TenantId);
                    Assert.True(refusal.Attempts >= 2);
                }

                refusal.Enabled = false;
                await using var retry = provider.CreateAsyncScope();
                await retry.ServiceProvider.GetRequiredService<INewJobsDigestService>().SendDigestsAsync();
                return refusal.Attempts;
            },
            assert: async (context, attempts) =>
            {
                var notices = await context.Set<UserNotification>().IgnoreQueryFilters().AsNoTracking().ToListAsync();
                var outbox = await context.OutboxMessages.IgnoreQueryFilters().AsNoTracking().ToListAsync();
                var cleaners = await context.Employees.IgnoreQueryFilters().AsNoTracking().ToListAsync();
                Console.WriteLine(JsonSerializer.Serialize(new { phase = "cleared-fault-retry", attempts,
                    cleaners = cleaners.Select(e => new { e.Id, e.TenantId, e.LastNewJobsDigestAt }),
                    notices = notices.Select(n => new { n.UserId, n.TenantId, n.EventKey }),
                    outbox = outbox.Select(o => new { o.MessageKey, o.TenantId }) }));
                Assert.Equal(2, notices.Count);
                Assert.Equal(2, outbox.Count);
                Assert.All(cleaners, e => Assert.NotNull(e.LastNewJobsDigestAt));
            }, transactional: false);
    }

    [Fact]
    public async Task Every_Child_Uses_One_Clock_While_Notices_Follow_Their_Recipient()
    {
        await TestMethod<bool>(
            setup: services => { services.AddFunctionsProcessing(); return Task.CompletedTask; },
            arrange: context => SeedAsync(context, crossRecipient: true),
            act: async provider =>
            {
                await provider.GetRequiredService<INewJobsDigestService>().SendDigestsAsync();
                return true;
            },
            assert: async (context, _) =>
            {
                var cleaners = await context.Employees.IgnoreQueryFilters().AsNoTracking().ToListAsync();
                var notices = await context.Set<UserNotification>().IgnoreQueryFilters().AsNoTracking().ToListAsync();
                var outbox = await context.OutboxMessages.IgnoreQueryFilters().AsNoTracking().ToListAsync();
                Assert.Equal(2, cleaners.Count);
                Assert.All(cleaners, e => Assert.NotNull(e.LastNewJobsDigestAt));
                var stamp = Assert.Single(cleaners.Select(e => e.LastNewJobsDigestAt).Distinct())!.Value;
                Assert.Equal(TestTenants.Default, cleaners.Single(e => e.Id == "digest-employee-a").TenantId);
                Assert.Equal(2, notices.Count);
                Assert.Equal(2, outbox.Count);
                var capturedTicks = Assert.Single(outbox
                    .Select(o => long.Parse(o.MessageKey[(o.MessageKey.LastIndexOf(':') + 1)..],
                        System.Globalization.CultureInfo.InvariantCulture))
                    .Distinct());
                Assert.Equal(capturedTicks / 10 * 10, stamp.UtcTicks);
                Assert.All(notices, n => Assert.Equal(TestTenants.Second, n.TenantId));
                Assert.All(outbox, o =>
                {
                    Assert.Equal(TestTenants.Second, o.TenantId);
                    Assert.EndsWith($":{capturedTicks}", o.MessageKey);
                });
                Console.WriteLine(JsonSerializer.Serialize(new { phase = "common-clock-recipient-ownership", stamp,
                    cleaners = cleaners.Select(e => new { e.Id, e.TenantId }),
                    notices = notices.Select(n => new { n.UserId, n.TenantId }),
                    outbox = outbox.Select(o => new { o.MessageKey, o.TenantId }) }));
            }, transactional: false);
    }

    private static async Task SeedAsync(CleansiaDbContext context, bool crossRecipient = false)
    {
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = "isolation-country";
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = "isolation-currency";
        context.Currencies.Add(currency);
        context.Languages.Add(Language.Create("en", "English"));
        foreach (var (id, tenant) in new[] { ("a", TestTenants.Default), ("b", TestTenants.Second) })
        {
            var user = User.CreateWithPassword($"{id}@digest.test", "Test-password-1!", id, "Cleaner", UserProfile.Employee);
            user.Id = $"digest-user-{id}";
            user.TenantId = crossRecipient ? TestTenants.Second : tenant;
            var employee = Employee.CreateWithUser(user);
            employee.Id = $"digest-employee-{id}";
            employee.TenantId = tenant;
            employee.Approve("admin");
            employee.AssignWorkCountry(country.Id);
            employee.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, "original", null);
            context.Add(employee);
        }

        var order = Order.Create("Customer", "customer@digest.test", "+420777444556",
            Address.Create("Job 1", "Prague", "11000", country.Id), 2, 1,
            DateTime.UtcNow.AddMinutes(10), PaymentType.Card, 1200m, currency.Id,
            paymentStatus: PaymentStatus.Paid, cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = "near-start-job";
        order.UpdateEstimatedTime(120);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        context.Add(order);
        await context.CommitAsync(CancellationToken.None);
    }

    private sealed class RefuseCleanerSave(string connectionString) : SaveChangesInterceptor
    {
        public bool Enabled { get; set; } = true;
        public int Attempts { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context!;
            var changed = context.ChangeTracker.Entries<Employee>().Any(e => e.Entity.Id == "digest-employee-a"
                && e.State == EntityState.Modified && e.Property(x => x.LastNewJobsDigestAt).IsModified);
            if (!Enabled || !changed) return result;
            Attempts++;
            var options = new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(connectionString).Options;
            await using var competing = new CleansiaDbContext(options);
            var employee = await competing.Employees.IgnoreQueryFilters().SingleAsync(e => e.Id == "digest-employee-a", cancellationToken);
            employee.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, $"competing-{Attempts}", null);
            await competing.SaveChangesAsync(cancellationToken);
            return result;
        }
    }

    private sealed class OrderedEmployees(CleansiaDbContext context) : EmployeeRepository(context)
    {
        public override IQueryable<Employee> GetQueryableIgnoringTenant()
            => base.GetQueryableIgnoringTenant().OrderBy(e => e.Id);
    }
}
