using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Orders;

public partial class CreateOrderCallerCurrencyTests
{
    [Fact]
    public async Task CrossMarket_Preferred_Cleaner_And_Second_Offer_Use_The_Operator_And_Account_Entitlement()
    {
        var cleanerIds = new[] { "cross-first-cleaner", "cross-second-cleaner" };
        await TestMethod<string>(setup: ConfigureCustomerSession,
            arrange: async (CleansiaDbContext context) =>
            {
                await SeedCrossMembershipAsync(context);
                foreach (var employeeId in cleanerIds)
                {
                    var user = User.CreateWithPassword($"{employeeId}@test.local", "Password123!", "Serving", "Cleaner", UserProfile.Employee);
                    user.TenantId = TestTenants.Second;
                    var employee = Employee.CreateWithUser(user);
                    employee.Id = employeeId;
                    employee.TenantId = TestTenants.Second;
                    employee.Approve("admin");
                    employee.AssignWorkCountry(Slovakia);
                    context.Employees.Add(employee);
                    var device = Device.Create(user.Id, "ios", $"token-{employeeId}", $"device-{employeeId}");
                    device.TenantId = TestTenants.Second;
                    context.Devices.Add(device);
                    var address = Address.Create("Work 1", "Bratislava", "11000", Slovakia);
                    address.TenantId = TestTenants.Second;
                    var history = Order.Create("Customer", CustomerEmail, "+420777111555", address, 2, 1,
                        DateTime.UtcNow.AddDays(-7), PaymentType.Cash, 60m, Eur, PaymentStatus.Paid, userId: CustomerUserId);
                    history.TenantId = TestTenants.Second;
                    history.AddAssignedEmployee(OrderEmployee.Create(history, employee));
                    var status = OrderStatusTrack.Create(OrderStatus.Completed, history);
                    status.TenantId = TestTenants.Second;
                    history.AddOrderStatus(status);
                    context.Orders.Add(history);
                }
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var command = BuildCommand(Slovakia, null, 60m) with { PaymentType = PaymentType.Cash, PreferredEmployeeId = cleanerIds[0] };
                var picker = await mediator.Send(new GetMyServingCleaners.Query(command.CleaningDate, [ServiceId], [PackageId]));
                Assert.True(picker.IsSuccess);
                Assert.Equal(2, picker.Value.Count);
                Assert.All(picker.Value, c => Assert.True(c.IsAvailableForRequestedSlot));
                var created = await mediator.Send(command);
                Assert.True(created.IsSuccess, created.Error?.Message);
                var orders = provider.GetRequiredService<IOrderRepository>();
                var order = await orders.GetByIdForOwnerAsync(created.Value.Id, CustomerUserId, CancellationToken.None);
                Assert.Equal(1, order!.PreferredOfferRound);
                Assert.Equal(cleanerIds[0], order.PreferredEmployeeId);
                order.EndPreferredHold(DateTime.UtcNow.AddSeconds(-1));
                await provider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
                AsAccount(provider);
                var chosen = await mediator.Send(new ChoosePreferredCleaner.Command(order.Id, cleanerIds[1]));
                Assert.True(chosen.IsSuccess, chosen.Error?.Message);
                Assert.Equal(2, chosen.Value.Round);
                Assert.Equal(TestTenants.Second, provider.GetRequiredService<ITenantProvider>().GetCurrentTenantId());
                AsAccount(provider);
                var starts = DateTime.UtcNow.AddDays(2).Date;
                var recurring = await mediator.Send(new CreateRecurringBooking.Command((int)RecurrenceFrequency.Weekly,
                    (int)starts.DayOfWeek, "10:00", 2, 1, SavedSlovakAddressId, [ServiceId], [PackageId], (int)PaymentType.Cash, starts,
                    PreferredEmployeeId: cleanerIds[0]));
                Assert.True(recurring.IsSuccess, recurring.Error?.Message);
                var update = await mediator.Send(new UpdateRecurringBooking.Command(recurring.Value.Id, (int)RecurrenceFrequency.Weekly,
                    (int)starts.DayOfWeek, "10:00", 2, 1, SavedSlovakAddressId, [ServiceId], [PackageId], (int)PaymentType.Cash, starts,
                    PreferredEmployeeId: cleanerIds[1]));
                Assert.True(update.IsSuccess, update.Error?.Message);
                var materialized = await ActivatorUtilities.CreateInstance<MaterializeRecurringBookingTemplate.Handler>(provider)
                    .Handle(new MaterializeRecurringBookingTemplate.Command(recurring.Value.Id, DateTime.UtcNow, 7), CancellationToken.None);
                Assert.True(materialized.IsSuccess, materialized.Error?.Message);
                Assert.True(materialized.Value.OrdersCreated > 0);
                AsAccount(provider);
                var occurrence = await orders.GetQueryableForOwner(CustomerUserId)
                    .Where(o => o.RecurringTemplateId == recurring.Value.Id).FirstAsync();
                var confirmed = await mediator.Send(new ConfirmRecurringOrder.Command(occurrence.Id));
                Assert.True(confirmed.IsSuccess, confirmed.Error?.Message);
                var notifications = provider.GetRequiredService<CleansiaDbContext>().Set<UserNotification>();
                var offerPayloads = await notifications.IgnoreQueryFilters()
                    .Where(n => n.EventKey == NotificationEventCatalog.PreferredOffer)
                    .Select(n => n.ArgsJson).ToListAsync();
                Assert.Contains(offerPayloads, payload =>
                    JsonSerializer.Deserialize<Dictionary<string, string>>(payload)!["orderId"] == occurrence.Id);
                return order.Id;
            },
            assert: async (CleansiaDbContext context, string id) =>
            {
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == id);
                Assert.Equal(TestTenants.Second, order.TenantId);
                Assert.Equal(cleanerIds[1], order.PreferredEmployeeId);
                Assert.Equal(2, order.PreferredOfferRound);
                Assert.Equal(TestTenants.Default, (await context.UserMemberships.IgnoreQueryFilters().SingleAsync()).TenantId);
            }, transactional: false);
    }
}
