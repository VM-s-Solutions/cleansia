using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Core.AppServices.Tenancy;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Functions;

/// <summary>
/// AC5 — the ack-on-reject vs throw-on-infra split on CalculateOrderPayHandler. The envelope suite proves
/// the envelope read and the success path; these pin the classification: a validator failure
/// (already-calculated, missing pay config) is logged and ACKED — retrying never changes the verdict and
/// must not poison; an infra exception from EnsureOpenPeriodAsync or from mediator.Send propagates so the
/// queue redelivers.
/// </summary>
public class CalculateOrderPayHandlerClassificationTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPayPeriodBackgroundService> _payPeriod = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<ILogger<CalculateOrderPayHandler>> _logger = new();

    private CalculateOrderPayHandler CreateHandler() => new(
        _mediator.Object,
        _payPeriod.Object,
        _tenantProvider.Object,
        new ArchivedCompanyDeadLetter(Mock.Of<IServiceScopeFactory>(), NullLogger<ArchivedCompanyDeadLetter>.Instance),
        _logger.Object);

    private static string Enveloped(string orderId, string employeeId) =>
        JsonSerializer.Serialize(
            new QueueEnvelope<CalculateOrderPayMessage>(
                $"pay:{orderId}:{employeeId}", "TENANT-A", new CalculateOrderPayMessage(orderId, employeeId)),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public async Task Validator_Rejection_Is_Acked_Not_Thrown_And_No_Retry_Pressure()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<CalculateOrderPay.Response>(
                new Error("OrderId", BusinessErrorMessage.PayAlreadyCalculated)));

        var ex = await Record.ExceptionAsync(
            () => CreateHandler().HandleAsync(Enveloped("ORDER-1", "EMP-1"), CancellationToken.None));

        Assert.Null(ex);
        _mediator.Verify(
            m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Infra_Fault_From_EnsureOpenPeriod_Throws_So_The_Queue_Retries()
    {
        _payPeriod
            .Setup(p => p.EnsureOpenPeriodAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down opening period"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateHandler().HandleAsync(Enveloped("ORDER-1", "EMP-1"), CancellationToken.None));

        // The pay command never ran — the infra fault short-circuited before it.
        _mediator.Verify(
            m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Infra_Fault_From_Mediator_Send_Throws_So_The_Queue_Retries()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("pay command infra timeout"));

        await Assert.ThrowsAsync<TimeoutException>(
            () => CreateHandler().HandleAsync(Enveloped("ORDER-2", "EMP-2"), CancellationToken.None));
    }

    [Fact]
    public async Task A_Rejected_Pay_Calculation_Logs_An_Error_With_Order_Employee_And_Reason()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<CalculateOrderPay.Response>(
                new Error("OrderId", BusinessErrorMessage.NoPayConfiguration)));

        await CreateHandler().HandleAsync(Enveloped("ORDER-3", "EMP-3"), CancellationToken.None);

        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("ORDER-3")
                    && state.ToString()!.Contains("EMP-3")
                    && state.ToString()!.Contains(BusinessErrorMessage.NoPayConfiguration)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task A_Pay_Row_That_Already_Exists_Is_A_Redelivery_And_Logs_No_Error()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<CalculateOrderPay.Response>(
                new Error("OrderId", BusinessErrorMessage.PayAlreadyCalculated)));

        await CreateHandler().HandleAsync(Enveloped("ORDER-4", "EMP-4"), CancellationToken.None);

        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
