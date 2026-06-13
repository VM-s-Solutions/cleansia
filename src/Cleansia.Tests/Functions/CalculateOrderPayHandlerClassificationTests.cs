using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Cleansia.Functions.Core.Handlers;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// AC5 — the ack-on-reject vs throw-on-infra split on CalculateOrderPayHandler. The envelope suite proves
/// the dual-read and the success path; these pin the classification: a validator failure
/// (already-calculated, missing pay config) is logged and ACKED — retrying never changes the verdict and
/// must not poison; an infra exception from EnsureOpenPeriodAsync or from mediator.Send propagates so the
/// queue redelivers.
/// </summary>
public class CalculateOrderPayHandlerClassificationTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPayPeriodBackgroundService> _payPeriod = new();

    private CalculateOrderPayHandler CreateHandler() => new(
        _mediator.Object,
        _payPeriod.Object,
        NullLogger<CalculateOrderPayHandler>.Instance);

    private static string Bare(string orderId, string employeeId) =>
        JsonSerializer.Serialize(
            new CalculateOrderPayMessage(orderId, employeeId),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public async Task Validator_Rejection_Is_Acked_Not_Thrown_And_No_Retry_Pressure()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<CalculateOrderPay.Response>(
                new Error("OrderId", BusinessErrorMessage.PayAlreadyCalculated)));

        var ex = await Record.ExceptionAsync(
            () => CreateHandler().HandleAsync(Bare("ORDER-1", "EMP-1"), CancellationToken.None));

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
            () => CreateHandler().HandleAsync(Bare("ORDER-1", "EMP-1"), CancellationToken.None));

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
            () => CreateHandler().HandleAsync(Bare("ORDER-2", "EMP-2"), CancellationToken.None));
    }
}
