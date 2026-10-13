using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cleansia.Tests.Services;

public sealed class NewJobsDigestPerCleanerIsolationTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var context = NewContext();
        await context.Database.EnsureCreatedAsync();
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
    }

    public Task DisposeAsync() => _connection.DisposeAsync().AsTask();

    [Fact]
    public async Task An_Employee_Vanished_Before_Its_Child_Load_Stages_No_Notice()
    {
        await SeedAsync();
        var creations = 0;
        var producer = Producer();
        await using var provider = NewJobsDigestTestServices.Create(() =>
        {
            var context = NewContext();
            if (++creations == 2)
                context.Database.ExecuteSqlRaw("DELETE FROM \"Employees\"");
            return context;
        }, producer.Object);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<INewJobsDigestService>().SendDigestsAsync();
        producer.Verify(p => p.NotifyAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(2, creations);
    }

    [Fact]
    public async Task Active_Host_Cancellation_Leaves_The_Child_And_Stops_Before_The_Next_Cleaner()
    {
        await SeedAsync(twoCleaners: true);
        using var stop = new CancellationTokenSource();
        var producer = Producer();
        producer.Setup(p => p.NotifyAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, Dictionary<string, string>, string?, string?, CancellationToken>((_, _, _, _, _, ct) =>
            {
                stop.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });
        await using var provider = NewJobsDigestTestServices.Create(NewContext, producer.Object);
        await using var scope = provider.CreateAsyncScope();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<INewJobsDigestService>().SendDigestsAsync(stop.Token));
        producer.Verify(p => p.NotifyAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        await using var check = NewContext();
        Assert.All(await check.Employees.IgnoreQueryFilters().ToListAsync(), e => Assert.Null(e.LastNewJobsDigestAt));
    }

    private async Task SeedAsync(bool twoCleaners = false)
    {
        await using var context = NewContext();
        foreach (var id in twoCleaners ? new[] { "a", "b" } : new[] { "a" })
        {
            var user = User.CreateWithPassword($"{id}@digest.test", "Test-password-1!", id, "Cleaner", UserProfile.Employee);
            var employee = Employee.CreateWithUser(user);
            employee.Id = id;
            employee.Approve("admin");
            employee.AssignWorkCountry("country");
            employee.TenantId = TestTenants.Default;
            context.Add(employee);
        }
        var order = Order.Create("Customer", "customer@digest.test", "+420777444556",
            Address.Create("Job 1", "Prague", "11000", "country"), 2, 1,
            DateTime.UtcNow.AddMinutes(10), PaymentType.Card, 1200m, "currency",
            paymentStatus: PaymentStatus.Paid, cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.UpdateEstimatedTime(120);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        context.Add(order);
        await context.CommitAsync(CancellationToken.None);
    }

    private CleansiaDbContext NewContext() => new(
        new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
        new TestUserSessionProvider("system", "system@digest.test"),
        Mock.Of<ITenantProvider>(p => p.GetCurrentTenantId() == TestTenants.Default));

    private static Mock<INotificationProducer> Producer()
    {
        var producer = new Mock<INotificationProducer>();
        producer.Setup(p => p.NotifyAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return producer;
    }
}
