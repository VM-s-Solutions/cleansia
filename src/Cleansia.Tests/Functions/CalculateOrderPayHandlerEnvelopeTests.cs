using System.Text.Json;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Cleansia.Functions.Core.Handlers;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// ADR-0002 D2.1a envelope DUAL-READ on <see cref="CalculateOrderPayHandler"/>.
/// CompleteOrder (the only producer) wraps the payload in QueueEnvelope&lt;T&gt;, but the consumer
/// deserialized the BARE type → OrderId/EmployeeId nested under "payload" came back EMPTY (not null,
/// so it passed the old `?? throw`) → the validator silently rejected → NO OrderEmployeePay row was
/// ever created for completed orders (cleaners unpaid), masked as a benign rejection with green CI.
/// These pin the dual-read: an enveloped body must reach mediator.Send with the real ids.
/// </summary>
public class CalculateOrderPayHandlerEnvelopeTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPayPeriodBackgroundService> _payPeriod = new();

    private CalculateOrderPayHandler CreateHandler() => new(
        _mediator.Object,
        _payPeriod.Object,
        NullLogger<CalculateOrderPayHandler>.Instance);

    private static readonly JsonSerializerOptions Json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string Bare(string orderId, string employeeId) =>
        JsonSerializer.Serialize(new CalculateOrderPayMessage(orderId, employeeId), Json);

    private static string Enveloped(string orderId, string employeeId, string? tenantId) =>
        JsonSerializer.Serialize(
            new QueueEnvelope<CalculateOrderPayMessage>(
                $"pay:{orderId}:{employeeId}", tenantId, new CalculateOrderPayMessage(orderId, employeeId)),
            Json);

    [Fact]
    public async Task Enveloped_Body_Is_Unwrapped_And_Sent_To_Mediator_With_Real_Ids()
    {
        var handler = CreateHandler();
        CalculateOrderPay.Command? captured = null;
        _mediator
            .Setup(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((cmd, _) => captured = (CalculateOrderPay.Command)cmd)
            .ReturnsAsync(BusinessResult.Success(new CalculateOrderPay.Response("PAY-1")));

        await handler.HandleAsync(Enveloped("ORDER-1", "EMP-1", "TENANT-A"), CancellationToken.None);

        _payPeriod.Verify(p => p.EnsureOpenPeriodAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal("ORDER-1", captured!.OrderId);
        Assert.Equal("EMP-1", captured.EmployeeId);
    }

    [Fact]
    public async Task Bare_Body_Is_Still_Processed_Backward_Compatible()
    {
        var handler = CreateHandler();
        CalculateOrderPay.Command? captured = null;
        _mediator
            .Setup(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((cmd, _) => captured = (CalculateOrderPay.Command)cmd)
            .ReturnsAsync(BusinessResult.Success(new CalculateOrderPay.Response("PAY-2")));

        await handler.HandleAsync(Bare("ORDER-2", "EMP-2"), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("ORDER-2", captured!.OrderId);
        Assert.Equal("EMP-2", captured.EmployeeId);
    }

    [Fact]
    public async Task Missing_Ids_Are_Acked_Not_Sent()
    {
        var handler = CreateHandler();

        // An empty-object body deserializes to empty ids — permanent, ack without invoking mediator.
        var ex = await Record.ExceptionAsync(() => handler.HandleAsync("{}", CancellationToken.None));

        Assert.Null(ex);
        _mediator.Verify(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
