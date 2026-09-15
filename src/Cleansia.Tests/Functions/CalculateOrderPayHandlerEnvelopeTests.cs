using System.Text.Json;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Cleansia.Functions.Core.Handlers;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// ADR-0002 D2.1a envelope read on <see cref="CalculateOrderPayHandler"/>, and the tenant the envelope
/// carries. CompleteOrder (the only producer) wraps the payload in QueueEnvelope&lt;T&gt; stamped with the
/// order's tenant; the consumer runs with no JWT, so that tenant is the ONLY way the filtered
/// PayPeriods read in EnsureOpenPeriodAsync and the command's validator see the company's rows. An
/// enveloped body must reach mediator.Send with the real ids under that tenant's override; a body
/// carrying no tenant can never succeed (the open-period insert would fail NOT NULL and poison the
/// queue), so it is logged and acked, never run.
/// </summary>
public class CalculateOrderPayHandlerEnvelopeTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPayPeriodBackgroundService> _payPeriod = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    private CalculateOrderPayHandler CreateHandler() => new(
        _mediator.Object,
        _payPeriod.Object,
        _tenantProvider.Object,
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

    private void ArrangeMediatorSuccess() =>
        _mediator
            .Setup(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new CalculateOrderPay.Response("PAY-1")));

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
    public async Task Envelope_Tenant_Is_The_Override_Before_The_Open_Period_Read_And_The_Command()
    {
        var overrideSet = false;
        _tenantProvider
            .Setup(t => t.SetTenantOverride("TENANT-A"))
            .Callback(() => overrideSet = true);
        _payPeriod
            .Setup(p => p.EnsureOpenPeriodAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Assert.True(overrideSet,
                "the open-period read is tenant-filtered, so the override must precede it"))
            .Returns(Task.CompletedTask);
        _mediator
            .Setup(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()))
            .Callback(() => Assert.True(overrideSet,
                "the pay row is stamped from the ambient tenant at commit, so the override must precede the command"))
            .ReturnsAsync(BusinessResult.Success(new CalculateOrderPay.Response("PAY-1")));

        await CreateHandler().HandleAsync(Enveloped("ORDER-1", "EMP-1", "TENANT-A"), CancellationToken.None);

        _tenantProvider.Verify(t => t.SetTenantOverride("TENANT-A"), Times.Once);
        _payPeriod.Verify(p => p.EnsureOpenPeriodAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Second_Companys_Envelope_Runs_Under_That_Company()
    {
        ArrangeMediatorSuccess();

        await CreateHandler().HandleAsync(Enveloped("ORDER-B", "EMP-B", "TENANT-B"), CancellationToken.None);

        _tenantProvider.Verify(t => t.SetTenantOverride("TENANT-B"), Times.Once);
        _tenantProvider.Verify(t => t.SetTenantOverride(It.Is<string>(s => s != "TENANT-B")), Times.Never);
        _payPeriod.Verify(p => p.EnsureOpenPeriodAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Envelope_Without_A_Tenant_Is_Acked_And_Never_Run(string? tenantId)
    {
        ArrangeMediatorSuccess();

        var ex = await Record.ExceptionAsync(
            () => CreateHandler().HandleAsync(Enveloped("ORDER-1", "EMP-1", tenantId), CancellationToken.None));

        Assert.Null(ex);
        _tenantProvider.Verify(t => t.SetTenantOverride(It.IsAny<string>()), Times.Never);
        _payPeriod.Verify(p => p.EnsureOpenPeriodAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Bare_Body_Carries_No_Tenant_So_It_Is_Acked_And_Never_Run()
    {
        ArrangeMediatorSuccess();

        var ex = await Record.ExceptionAsync(
            () => CreateHandler().HandleAsync(Bare("ORDER-2", "EMP-2"), CancellationToken.None));

        Assert.Null(ex);
        _tenantProvider.Verify(t => t.SetTenantOverride(It.IsAny<string>()), Times.Never);
        _payPeriod.Verify(p => p.EnsureOpenPeriodAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<CalculateOrderPay.Command>(), It.IsAny<CancellationToken>()), Times.Never);
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
