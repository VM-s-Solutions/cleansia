using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Database;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Receipts;

/// <summary>
/// THE CASH IS COLLECTED, AND THE RECEIPT ISSUED AT BOOKING STOPS SAYING IT IS UNPAID — end to end,
/// over Postgres: the receipt issued by the queue consumer, the collection recorded by the assigned
/// cleaner through the real pipeline, the restate read back out of the outbox row that collection
/// committed, and that row's own body handed to the consumer.
///
/// <para>The collection finds the receipt only through the <c>Include</c> on its load. With no lazy
/// loading in the solution the navigation is otherwise null, the restate is never staged, and the
/// unit fixtures — which attach the receipt in memory and serve the order from a mock queryable that
/// ignores <c>Include</c> — cannot tell.</para>
/// </summary>
[Collection("PostgresCollection")]
public class CashCollectionRestatesTheReceiptTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string OrderId = "01K5RCPTCASH0RESTATE000001";
    private const string CurrencyId = "currency-czk-cash-restate";
    private const string CountryId = "country-cz-cash-restate";
    private const string CleanerUserId = "user-cash-restate";
    private const string CleanerEmployeeId = "employee-cash-restate";
    private const string CleanerEmail = "cleaner-cash-restate@cleansia.test";

    [Fact]
    public async Task Collecting_The_Cash_Restates_The_Issued_Receipt_As_Paid_Under_Its_Number()
    {
        var rendered = new List<ReceiptPdfData>();
        await TestMethod(
            setup: services => AssignedCleanerWithCapturedReceipts(services, rendered),
            arrange: SeedCashOrderInProgressAsync,
            act: async provider =>
            {
                await HandleReceiptMessageAsync(provider, JsonSerializer.Serialize(
                    new GenerateReceiptMessage(OrderId, "en"),
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

                var collected = await provider.GetRequiredService<IMediator>()
                    .Send(new MarkCashCollected.Command(OrderId));
                Assert.True(collected.IsSuccess, collected.Error?.Message);

                var restate = await provider.GetRequiredService<CleansiaDbContext>().OutboxMessages
                    .IgnoreQueryFilters()
                    .SingleAsync(m => m.MessageKey == MessageKeys.ReceiptReissue(OrderId));
                await HandleReceiptMessageAsync(provider, restate.Body);

                return restate.QueueName;
            },
            assert: (_, queueName) =>
            {
                Assert.Equal(QueueNames.GenerateReceipt, queueName);
                Assert.Equal(2, rendered.Count);
                var (issued, restated) = (rendered[0], rendered[1]);
                Assert.Equal(PaymentStatus.Pending, issued.PaymentStatus);
                Assert.Equal(PaymentStatus.Paid, restated.PaymentStatus);
                Assert.Equal(issued.ReceiptNumber, restated.ReceiptNumber);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    /// <summary>A scope of its own, as the Functions host gives every queue message.</summary>
    private static async Task HandleReceiptMessageAsync(IServiceProvider provider, string body)
    {
        using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        await ActivatorUtilities.CreateInstance<GenerateReceiptHandler>(scope.ServiceProvider)
            .HandleAsync(body, CancellationToken.None);
    }

    private static Task AssignedCleanerWithCapturedReceipts(IServiceCollection services, List<ReceiptPdfData> rendered)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CleanerUserId,
            CleanerEmail,
            [
                new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()),
                new Claim(TestUserSessionProvider.EmployeeIdClaimType, CleanerEmployeeId),
            ])));

        var bytes = new byte[] { 37, 80, 68, 70 };
        var pdf = new Mock<IPdfService>();
        pdf.Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
            .Callback<ReceiptPdfData, string?>((data, _) => rendered.Add(data))
            .Returns(bytes);
        services.Replace(ServiceDescriptor.Singleton(pdf.Object));

        var blob = new Mock<IBlobContainerClient>();
        blob.Setup(b => b.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BlobFile(new MemoryStream(bytes), "application/pdf"));
        blob.Setup(b => b.CreateFileForWritingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream());
        var blobs = new Mock<IBlobContainerClientFactory>();
        blobs.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blob.Object);
        services.Replace(ServiceDescriptor.Singleton(blobs.Object));

        services.Replace(ServiceDescriptor.Scoped<IEmailService>(_ => Mock.Of<IEmailService>()));
        return Task.CompletedTask;
    }

    private static async Task SeedCashOrderInProgressAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        context.CountryConfigurations.Add(CountryConfiguration.Create(CountryId, "CZK", "cs", 0.21m));

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        context.Currencies.Add(currency);

        var issuer = CompanyInfo.Create("Cleansia s.r.o.", "Cleansia", "12345678", "Hlavní 1", "Praha", "11000", CountryId);
        issuer.TenantId = TestTenants.Default;
        context.CompanyInfo.Add(issuer);

        var cleanerUser = User.CreateWithPassword(
            CleanerEmail, TestConstants.TestUserSession.TestUserPassword, "Petra", "Svobodova", UserProfile.Employee);
        cleanerUser.Id = CleanerUserId;
        cleanerUser.ConfirmEmail();
        context.Users.Add(cleanerUser);
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = CleanerEmployeeId;
        cleaner.Approve(approvedByUserId: "admin-cash-restate");
        cleaner.AssignWorkCountry(CountryId);
        context.Employees.Add(cleaner);

        var order = Order.Create(
            customerName: "Jana Novakova",
            customerEmail: "guest-cash-restate@cleansia.test",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow,
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: null);
        order.Id = OrderId;
        order.UpdateEstimatedTime(120);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.InProgress, order));
        context.Orders.Add(order);

        await context.CommitAsync(CancellationToken.None);
    }
}
