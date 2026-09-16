using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.Orders;

public partial class CreateOrderCallerCurrencyTests
{
    private static Task CrossMarketWithoutExternalTransports(IServiceCollection services)
    {
        ConfigureCustomerSession(services);
        var blobs = new Mock<IBlobContainerClientFactory>();
        blobs.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(Mock.Of<IBlobContainerClient>());
        services.Replace(ServiceDescriptor.Singleton(blobs.Object));
        services.Replace(ServiceDescriptor.Scoped<IEmailService>(_ => Mock.Of<IEmailService>()));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CrossMarket_Receipt_Uses_The_Operator_Issuer_And_Counter_And_The_Account_Can_Download_It()
    {
        ReceiptPdfData? rendered = null;
        var bytes = new byte[] { 37, 80, 68, 70 };
        await TestMethod(
            setup: services =>
            {
                CrossMarketWithoutExternalTransports(services);
                var pdf = new Mock<IPdfService>();
                pdf.Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
                    .Callback<ReceiptPdfData, string?>((data, _) => rendered = data).Returns(bytes);
                services.Replace(ServiceDescriptor.Singleton(pdf.Object));
                var blob = new Mock<IBlobContainerClient>();
                blob.Setup(b => b.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => new BlobFile(new MemoryStream(bytes), "application/pdf"));
                var factory = new Mock<IBlobContainerClientFactory>();
                factory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blob.Object);
                services.Replace(ServiceDescriptor.Singleton(factory.Object));
                return Task.CompletedTask;
            },
            arrange: async context =>
            {
                await SeedWithSlovakiaOperatedBySecondCompanyAsync(context);
                var czech = CompanyInfo.Create("Czech issuer", "CZ", "CZ123", "Street", "Praha", "11000", Czechia);
                czech.TenantId = TestTenants.Default;
                var slovak = CompanyInfo.Create("Slovak issuer", "SK", "SK123", "Street", "Bratislava", "11000", Slovakia);
                slovak.TenantId = TestTenants.Second;
                context.CompanyInfo.AddRange(czech, slovak);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var created = await mediator.Send(BuildCommand(Slovakia, null, 60m) with { PaymentType = PaymentType.Cash });
                Assert.True(created.IsSuccess, created.Error?.Message);
                AsAccount(provider);
                await ActivatorUtilities.CreateInstance<GenerateReceiptHandler>(provider)
                    .HandleAsync(JsonSerializer.Serialize(new GenerateReceiptMessage(created.Value.Id, "en"), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), CancellationToken.None);
                Assert.NotNull(rendered);
                Assert.Equal("Slovak issuer", rendered.Company!.LegalName);
                Assert.Equal("€", rendered.Currency);
                AsAccount(provider);
                var download = await mediator.Send(new DownloadOrderReceipt.Query(created.Value.Id));
                Assert.True(download.IsSuccess, download.Error?.Message);
                Assert.Equal(bytes, download.Value.PdfBytes);
                return created.Value.Id;
            },
            assert: async (context, id) =>
            {
                Assert.Equal(TestTenants.Second, (await context.OrderReceipts.IgnoreQueryFilters().SingleAsync(r => r.OrderId == id)).TenantId);
                Assert.Equal(TestTenants.Second, (await context.FiscalCounters.IgnoreQueryFilters().SingleAsync()).TenantId);
            }, transactional: false);
    }

    [Fact]
    public async Task CrossMarket_Customer_Cancellation_Refund_Is_Stamped_With_The_Operator()
    {
        var stripe = new Mock<IStripeClient>();
        await TestMethod(setup: services =>
            {
                CrossMarketWithoutExternalTransports(services);
                var factory = new Mock<IStripeClientFactory>();
                factory.Setup(f => f.CreateClient()).Returns(stripe.Object);
                services.Replace(ServiceDescriptor.Singleton(factory.Object));
                return Task.CompletedTask;
            }, arrange: SeedWithSlovakiaOperatedBySecondCompanyAsync,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var created = await mediator.Send(BuildCommand(Slovakia, null, 60m));
                Assert.True(created.IsSuccess, created.Error?.Message);
                var order = await provider.GetRequiredService<IOrderRepository>().GetByIdAsync(created.Value.Id, CancellationToken.None);
                order!.UpdatePaymentStatus(PaymentStatus.Paid);
                order.AssignStripePaymentIntentId("pi_cross_market");
                await provider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
                AsAccount(provider);
                var cancel = await mediator.Send(new CancelOrder.Command(order.Id, null));
                Assert.True(cancel.IsSuccess, cancel.Error?.Message);
                Assert.True(cancel.Value.RefundInitiated);
                return order.Id;
            }, assert: async (context, id) =>
            {
                var refund = await context.Refunds.IgnoreQueryFilters().SingleAsync(r => r.OrderId == id);
                Assert.Equal(TestTenants.Second, refund.TenantId);
                Assert.Equal(60m, refund.Amount);
                Assert.Equal(RefundStatus.Succeeded, refund.Status);
                stripe.Verify(s => s.RefundPaymentIntentAsync("pi_cross_market", 60m, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            }, transactional: false);
    }

    [Fact]
    public async Task CrossMarket_Export_And_Erasure_Include_The_Operators_Order()
    {
        await TestMethod(setup: CrossMarketWithoutExternalTransports,
            arrange: async context =>
            {
                await SeedCrossOrderAsync(context);
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == CrossOrderId);
                var status = OrderStatusTrack.Create(OrderStatus.Cancelled, order);
                status.TenantId = TestTenants.Second;
                order.AddOrderStatus(status);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var export = await provider.GetRequiredService<IGdprExportService>().BuildAsync(CustomerUserId, "self", CancellationToken.None);
                Assert.Contains(export.Orders, order => order.Id == CrossOrderId);
                var erased = await provider.GetRequiredService<IGdprDeletionService>().DeleteUserAccountAsync(
                    CustomerUserId, "requested", _ => ("self", null), false, CancellationToken.None);
                Assert.True(erased.IsSuccess, erased.Error?.Message);
                await provider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
                return erased;
            },
            assert: async (context, _) =>
            {
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == CrossOrderId);
                Assert.Null(order.UserId);
                Assert.NotEqual(CustomerEmail, order.CustomerEmail);
                Assert.Equal(TestTenants.Second, order.TenantId);
            }, transactional: false);
    }
    [Fact]
    public async Task CrossMarket_Promo_Redemption_Remains_In_The_Account_And_Enforces_Its_Per_User_Cap()
    {
        const string code = "TRAVEL20";
        await TestMethod(setup: ConfigureCustomerSession,
            arrange: async context =>
            {
                await SeedWithSlovakiaOperatedBySecondCompanyAsync(context);
                context.PromoCodes.Add(PromoCode.CreatePercent(code, 0.20m, maxRedemptionsPerUser: 1));
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var command = BuildCommand(Slovakia, null, 60m) with { PaymentType = PaymentType.Cash, PromoCode = code };
                var created = await mediator.Send(command);
                Assert.True(created.IsSuccess, created.Error?.Message);
                AsAccount(provider);
                var redemptions = provider.GetRequiredService<IPromoCodeRedemptionRepository>();
                var redemption = await redemptions.GetByOrderIdAsync(created.Value.Id, CancellationToken.None);
                Assert.NotNull(redemption);
                Assert.Equal(TestTenants.Default, redemption.TenantId);
                Assert.Equal(1, await redemptions.CountForUserAndCodeAsync(CustomerUserId, redemption.PromoCodeId, CancellationToken.None));
                var repeated = await mediator.Send(command);
                Assert.True(repeated.IsFailure);
                return created.Value.Id;
            },
            assert: async (context, id) =>
            {
                var redemption = await context.PromoCodeRedemptions.IgnoreQueryFilters().SingleAsync();
                Assert.Equal(id, redemption.OrderId);
                Assert.Equal(TestTenants.Default, redemption.TenantId);
                Assert.Equal(12m, redemption.AppliedDiscount);
            }, transactional: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrossMarket_Referral_Completion_Uses_Account_History_And_Account_Ledgers(bool earlierAccountCompletion)
    {
        const string referrerId = "cross-market-referrer";
        await TestMethod(setup: ConfigureCustomerSession,
            arrange: async context =>
            {
                await SeedCrossOrderAsync(context);
                var referrer = User.CreateWithPassword("referrer@test.local", "Password123!", "Ref", "Errer", UserProfile.Customer);
                referrer.Id = referrerId;
                context.Users.Add(referrer);
                var code = ReferralCode.Generate(referrerId, "TRAVEL", "system");
                context.ReferralCodes.Add(code);
                context.Referrals.Add(Referral.CreateAccepted(referrerId, CustomerUserId, code.Id, "system"));
                if (earlierAccountCompletion)
                {
                    var address = Address.Create("Home 1", "Praha", "11000", Czechia);
                    var earlier = Order.Create("Caller Customer", CustomerEmail, "+420777111555", address, 2, 1,
                        DateTime.UtcNow.AddDays(-7), PaymentType.Cash, 60m, Czk, PaymentStatus.Paid, userId: CustomerUserId);
                    earlier.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, earlier));
                    context.Orders.Add(earlier);
                }
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                provider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Second);
                var completed = await ActivatorUtilities.CreateInstance<CompleteOrder.Handler>(provider)
                    .Handle(new CompleteOrder.Command(CrossOrderId, 60), CancellationToken.None);
                Assert.True(completed.IsSuccess, completed.Error?.Message);
                await provider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
                await provider.GetRequiredService<IReferralService>().ProcessOrderCompletedAsync(CrossOrderId, CustomerUserId, CancellationToken.None);
                await provider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
                return completed;
            },
            assert: async (context, _) =>
            {
                var referral = await context.Referrals.IgnoreQueryFilters().SingleAsync();
                Assert.Equal(TestTenants.Default, referral.TenantId);
                Assert.Equal(earlierAccountCompletion ? ReferralStatus.Accepted : ReferralStatus.Qualified, referral.Status);
                Assert.Equal(earlierAccountCompletion ? 0 : 1, (await context.ReferralCodes.IgnoreQueryFilters().SingleAsync()).TimesUsed);
                var ledger = await context.LoyaltyTransactions.IgnoreQueryFilters().Where(t => t.Source == LoyaltyEarnSource.Referral).ToListAsync();
                Assert.Equal(earlierAccountCompletion ? 0 : 2, ledger.Count);
                Assert.All(ledger, row => { Assert.Equal(TestTenants.Default, row.TenantId); Assert.Equal(150, row.Points); });
            }, transactional: false);
    }
}
