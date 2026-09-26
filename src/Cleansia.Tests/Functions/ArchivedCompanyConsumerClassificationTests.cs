using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Tests.Infrastructure;

namespace Cleansia.Tests.Functions;

/// <summary>
/// ADR-0064 D3 — a books-writing consumer that can legitimately arrive late classifies the frozen
/// company's refusal as permanent: the verbatim body is dead-lettered under the frozen company from a
/// scope of its own, the refusal is logged, and the message is acked on the first delivery rather
/// than retried for days. Anything else keeps its classification.
/// </summary>
public sealed class ArchivedCompanyConsumerClassificationTests
{
    private const string TenantId = "cleansia-sk";
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly Mock<IDeadLetterStore> _deadLetters = new();
    private readonly Mock<ITenantProvider> _scopedTenantProvider = new();

    private ArchivedCompanyDeadLetter DeadLetter()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _deadLetters.Object);
        services.AddScoped(_ => _scopedTenantProvider.Object);
        var provider = services.BuildServiceProvider();
        return new ArchivedCompanyDeadLetter(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ArchivedCompanyDeadLetter>.Instance);
    }

    [Fact]
    public async Task A_Late_Pay_Calculation_For_A_Frozen_Company_Is_Dead_Lettered_Under_It_And_Acked()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CompanyArchivedException(TenantId));
        var handler = new CalculateOrderPayHandler(
            mediator.Object, Mock.Of<IPayPeriodBackgroundService>(), Mock.Of<ITenantProvider>(), DeadLetter(), NullLogger<CalculateOrderPayHandler>.Instance);
        var body = JsonSerializer.Serialize(
            new QueueEnvelope<CalculateOrderPayMessage>("pay:ORDER-1:EMP-1", TenantId, new CalculateOrderPayMessage("ORDER-1", "EMP-1")), CamelCase);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(body, CancellationToken.None));

        Assert.Null(ex);
        _scopedTenantProvider.Verify(p => p.SetTenantOverride(TenantId), Times.Once);
        _deadLetters.Verify(
            s => s.RecordAsync(QueueNames.CalculateOrderPay, body, $"{BusinessErrorMessage.TenantArchived}:{TenantId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_Late_Receipt_For_A_Frozen_Company_Is_Dead_Lettered_Under_It_And_Acked()
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            PaymentType = PaymentType.Card,
            PaymentStatus = PaymentStatus.Paid,
        });
        order.Id = "01J8ARCH1VEDRECE1PT0RDER00";
        var orders = new Mock<IOrderRepository>();
        orders.Setup(r => r.GetByIdIgnoringTenantAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(s => s.ReserveReceiptAsync(order, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cleansia.Core.Domain.Receipts.OrderReceipt.Create(order.Id, "RCP-2026-0001", "r.pdf", "r.pdf", "en"));
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>());
        // The claim commit is where the frozen company's guard fires: the receipt row is books.
        unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new CompanyArchivedException(TenantId));
        var handler = new GenerateReceiptHandler(
            orders.Object, receipts.Object, Mock.Of<IEmailService>(), TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
            Mock.Of<ICountryConfigurationRepository>(),
            unitOfWork.Object, Mock.Of<ITenantProvider>(), DeadLetter(), NullLogger<GenerateReceiptHandler>.Instance);
        var body = JsonSerializer.Serialize(
            new QueueEnvelope<GenerateReceiptMessage>(MessageKeys.Receipt(order.Id), TenantId, new GenerateReceiptMessage(order.Id, "en")), CamelCase);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(body, CancellationToken.None));

        Assert.Null(ex);
        _deadLetters.Verify(
            s => s.RecordAsync(QueueNames.GenerateReceipt, body, $"{BusinessErrorMessage.TenantArchived}:{TenantId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Any_Other_Failure_Still_Throws_For_Redelivery_And_Dead_Letters_Nothing()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("database unreachable"));
        var handler = new CalculateOrderPayHandler(
            mediator.Object, Mock.Of<IPayPeriodBackgroundService>(), Mock.Of<ITenantProvider>(), DeadLetter(), NullLogger<CalculateOrderPayHandler>.Instance);
        var body = JsonSerializer.Serialize(
            new QueueEnvelope<CalculateOrderPayMessage>("pay:ORDER-1:EMP-1", TenantId, new CalculateOrderPayMessage("ORDER-1", "EMP-1")), CamelCase);

        await Assert.ThrowsAsync<TimeoutException>(() => handler.HandleAsync(body, CancellationToken.None));

        _deadLetters.Verify(s => s.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
