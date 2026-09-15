using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// Owner ruling 2026-09-15 (ADR-0062 D5, A7 by e-mail): a GUEST booking placed with the subject's e-mail
/// address is the subject's order. Against real Postgres, through the real erasure walk and the real
/// export: the erasure anonymises such an order exactly as it does the account's own — name, e-mail,
/// phone, address, the photo row's free text — and blanks IP and device on the guest audit rows that
/// name it, while a guest booking under another address keeps every one of those; and the export lists
/// the same orders the erasure reaches, so what is erased is what is exported.
///
/// <para>The e-mail is matched case-insensitively — the guest order here was typed in capitals — and a
/// guest audit row that merely shares the order's identifier string under another resource type is not
/// the subject's.</para>
/// </summary>
[Collection("PostgresCollection")]
public class GuestOrderErasureTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string SubjectId = TestConstants.TestUserSession.TestUserId;
    private const string SubjectEmail = TestConstants.TestUserSession.TestUserEmail;
    private const string StrangerEmail = "tomas.svoboda@cleansia.test";
    private const string CleanerUserId = "user-cln-guest-erasure";
    private const string CleanerId = "emp-cln-guest-erasure";
    private const string CountryId = "country-cz-guest-erasure";
    private const string CurrencyId = "currency-czk-guest-erasure";
    private const string OwnOrderId = "order-guest-erasure-own";
    private const string GuestOrderId = "order-guest-erasure-subj";
    private const string StrangerOrderId = "order-guest-erasure-other";
    private const string GuestIp = "203.0.113.9";
    private const string GuestDevice = "iPhone 15 / iOS 17.4";
    private const string GuestDeviceId = "device-abc-123";
    private const string StrangerIp = "198.51.100.7";
    private const string StrangerDevice = "Pixel 8";
    private const string StrangerDeviceId = "device-keep-1";
    private const string Payload = "{\"isGuest\": true, \"totalPrice\": 1250}";
    private const string PhotoNotes = "Left the key with the neighbour at no. 14.";

    private static Task AsTheSubject(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            SubjectId, SubjectEmail, [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
        services.Replace(ServiceDescriptor.Scoped<IRequestMetadataProvider>(_ => new TestRequestMetadataProvider(GuestIp, GuestDevice)));
        return Task.CompletedTask;
    }

    // The photo blobs are the storage account's; here the rows are the question, and the real client
    // would spend the SDK's retry budget on an emulator that is not there.
    private static Task WithoutBlobStorage(IServiceCollection services)
    {
        var factory = new Mock<IBlobContainerClientFactory>();
        factory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(Mock.Of<IBlobContainerClient>());
        services.Replace(ServiceDescriptor.Singleton(_ => factory.Object));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_Guest_Order_Under_The_Subjects_Email_Is_Anonymised_With_Its_Photo_Address_And_Audit_Rows_And_A_Strangers_Keeps_Everything()
    {
        await TestMethod(
            setup: WithoutBlobStorage,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command()),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(GdprRequestStatus.Completed, request.Status);

                var orders = await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.CustomerAddress)
                    .ToDictionaryAsync(o => o.Id);
                var photos = await context.Set<OrderPhoto>().IgnoreQueryFilters().ToDictionaryAsync(p => p.OrderId);
                var guestRows = await context.CustomerActionAudits.IgnoreQueryFilters()
                    .Where(a => a.UserId == null)
                    .ToListAsync();

                foreach (var erasedId in new[] { OwnOrderId, GuestOrderId })
                {
                    var erased = orders[erasedId];
                    Assert.Null(erased.UserId);
                    Assert.Equal(AnonymizationMarker.Value, erased.CustomerName);
                    Assert.Equal(AnonymizationMarker.Value, erased.CustomerEmail);
                    Assert.Equal(AnonymizationMarker.Value, erased.CustomerPhone);
                    Assert.Equal(AnonymizationMarker.Value, erased.CustomerAddress.Street);
                    Assert.Equal(AnonymizationMarker.Value, erased.CustomerAddress.City);
                    Assert.Equal(AnonymizationMarker.Value, photos[erasedId].OriginalFileName);
                    Assert.Null(photos[erasedId].Notes);
                }

                var guestBooking = Assert.Single(guestRows, r => r.ResourceId == GuestOrderId && r.ResourceType == "Order");
                Assert.Null(guestBooking.IpAddress);
                Assert.Null(guestBooking.DeviceLabel);
                Assert.Null(guestBooking.DeviceId);
                Assert.Null(guestBooking.UserId);
                Assert.Contains("\"isGuest\": true", guestBooking.PayloadJson);

                var stranger = orders[StrangerOrderId];
                Assert.Null(stranger.UserId);
                Assert.Equal("Tomas Svoboda", stranger.CustomerName);
                Assert.Equal(StrangerEmail, stranger.CustomerEmail);
                Assert.Equal("+420777999888", stranger.CustomerPhone);
                Assert.Equal("Svobodova 7", stranger.CustomerAddress.Street);
                Assert.Equal("Tomas_Svoboda_hallway.jpg", photos[StrangerOrderId].OriginalFileName);
                Assert.Equal(PhotoNotes, photos[StrangerOrderId].Notes);

                var strangersBooking = Assert.Single(guestRows, r => r.ResourceId == StrangerOrderId);
                Assert.Equal(StrangerIp, strangersBooking.IpAddress);
                Assert.Equal(StrangerDevice, strangersBooking.DeviceLabel);
                Assert.Equal(StrangerDeviceId, strangersBooking.DeviceId);

                // Same id, different resource: the walk keys on the ORDER, and an unrelated row that merely
                // shares the identifier string under another type is not the subject's.
                var otherTypeSameId = Assert.Single(guestRows, r => r.ResourceId == GuestOrderId && r.ResourceType == "Dispute");
                Assert.Equal(StrangerIp, otherTypeSameId.IpAddress);
                Assert.Equal(StrangerDevice, otherTypeSameId.DeviceLabel);
            });
    }

    [Fact]
    public async Task The_Export_Lists_The_Guest_Order_Under_The_Subjects_Email_Beside_Their_Own_And_Not_A_Strangers()
    {
        await TestMethod(
            setup: AsTheSubject,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new ExportUserData.Command()),
            assert: async (CleansiaDbContext context, BusinessResult<GdprExportDto> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                Assert.Equal([OwnOrderId, GuestOrderId], result.Value.Orders.Select(o => o.Id).Order());
                var guest = Assert.Single(result.Value.Orders, o => o.Id == GuestOrderId);
                Assert.Equal(SubjectEmail.ToUpperInvariant(), guest.CustomerEmail);
                Assert.DoesNotContain(result.Value.Orders, o => o.Id == StrangerOrderId);

                var audit = Assert.Single(await context.CustomerActionAudits.IgnoreQueryFilters().ToListAsync(), a => a.Action == "customer.gdpr.export");
                Assert.Equal(2, JsonDocument.Parse(audit.PayloadJson!).RootElement.GetProperty("orderCount").GetInt32());
            },
            transactional: false);
    }

    private static async Task Seed(CleansiaDbContext context)
    {
        if (!await context.Languages.AnyAsync())
        {
            context.Languages.Add(Language.Create("en", "English"));
        }

        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var subject = User.CreateWithPassword(
            email: SubjectEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName);
        subject.Id = SubjectId;
        subject.ConfirmEmail();
        context.Users.Add(subject);

        var cleanerUser = User.CreateWithPassword("cleaner-guest-erasure@cleansia.test", "Seed-Password-123", "Clean", "Er", UserProfile.Employee);
        cleanerUser.Id = CleanerUserId;
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = CleanerId;
        context.Users.Add(cleanerUser);
        context.Employees.Add(cleaner);

        context.Orders.AddRange(
            NewOrder(OwnOrderId, SubjectId, SubjectEmail, $"{TestConstants.TestUserSession.TestFirstName} {TestConstants.TestUserSession.TestLastName}", "+420777111333", "Testovaci 12"),
            NewOrder(GuestOrderId, userId: null, SubjectEmail.ToUpperInvariant(), "Guest Who Is The Subject", "+420777111333", "Testovaci 12"),
            NewOrder(StrangerOrderId, userId: null, StrangerEmail, "Tomas Svoboda", "+420777999888", "Svobodova 7"));

        context.AddRange(
            NewPhoto(OwnOrderId, "Subject_kitchen.jpg"),
            NewPhoto(GuestOrderId, "Guest_kitchen.jpg"),
            NewPhoto(StrangerOrderId, "Tomas_Svoboda_hallway.jpg"));

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);

        context.CustomerActionAudits.AddRange(
            GuestRow("Order", GuestOrderId, GuestIp, GuestDevice, GuestDeviceId, Payload),
            GuestRow("Order", StrangerOrderId, StrangerIp, StrangerDevice, StrangerDeviceId, payloadJson: null),
            GuestRow("Dispute", GuestOrderId, StrangerIp, StrangerDevice, StrangerDeviceId, payloadJson: null));
        await context.SaveChangesAsync();
    }

    private static Order NewOrder(string id, string? userId, string customerEmail, string customerName, string customerPhone, string street)
    {
        var order = Order.Create(
            customerName: customerName,
            customerEmail: customerEmail,
            customerPhone: customerPhone,
            customerAddress: Address.Create(street, "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-30),
            paymentType: PaymentType.Card,
            totalPrice: 1250m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: userId);
        order.Id = id;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        return order;
    }

    private static OrderPhoto NewPhoto(string orderId, string originalFileName) =>
        OrderPhoto.Create(
            orderId, PhotoType.After, $"https://blobs.test/order-photos/{orderId}.jpg", $"{orderId}.jpg",
            originalFileName, 1024, "image/jpeg", CleanerId, PhotoNotes);

    private static CustomerActionAudit GuestRow(
        string resourceType, string resourceId, string ipAddress, string deviceLabel, string deviceId, string? payloadJson)
    {
        var row = CustomerActionAudit.Create(
            userId: null, clientAudience: JwtAudiences.Customer, ipAddress: ipAddress, deviceLabel: deviceLabel,
            deviceId: deviceId, action: "customer.order.create", resourceType: resourceType, resourceId: resourceId,
            success: true, errorCode: null, payloadJson: payloadJson, correlationId: null);
        row.TenantId = TestTenants.Default;
        return row;
    }
}
