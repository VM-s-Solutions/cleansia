using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Cleansia.Functions.Core.Handlers;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// TC-6 — the proof the <c>generate-invoice</c> queue is no longer dead. The consumer was a no-op stub
/// that logged "not yet implemented" and returned <c>Task.CompletedTask</c>; these pin the real flow:
/// dual-read the wire body (ADR-0002 D2.1a), establish tenant context from the trusted looked-up
/// employee, run <c>GenerateInvoice.Command</c> via <see cref="IMediator"/>, and classify the result —
/// validator rejection ⇒ ack (no poison loop), infra failure ⇒ throw (queue retries).
///
/// Written test-first (RED on the stub: it never calls mediator, never sets a tenant override, and
/// never looks up the employee).
/// </summary>
public class GenerateInvoiceHandlerTests
{
    private const string EmployeeId = "EMP-1";
    private const string PayPeriodId = "PERIOD-1";
    private const string TenantId = "TENANT-A";
    private const string LanguageCode = "en";

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    private GenerateInvoiceHandler CreateHandler() => new(
        _mediator.Object,
        _employeeRepository.Object,
        _tenantProvider.Object,
        NullLogger<GenerateInvoiceHandler>.Instance);

    private static readonly JsonSerializerOptions Json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string Bare(string employeeId, string payPeriodId) =>
        JsonSerializer.Serialize(new GenerateInvoiceMessage(employeeId, payPeriodId, LanguageCode), Json);

    private static string Enveloped(string employeeId, string payPeriodId, string? tenantId) =>
        JsonSerializer.Serialize(
            new QueueEnvelope<GenerateInvoiceMessage>(
                MessageKeys.Invoice(payPeriodId, employeeId),
                tenantId,
                new GenerateInvoiceMessage(employeeId, payPeriodId, LanguageCode)),
            Json);

    private Employee ArrangeTenantEmployee(string? tenantId = TenantId)
    {
        var employee = Employee.CreateWithUser(
            User.CreateWithPassword("emp@cleansia.test", "Password1", "F", "L"));
        employee.Id = EmployeeId;
        employee.TenantId = tenantId;
        _employeeRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        return employee;
    }

    private void ArrangeMediatorSuccess(string invoiceId = "INV-1") =>
        _mediator
            .Setup(m => m.Send(It.IsAny<GenerateInvoice.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new GenerateInvoice.Response(invoiceId)));

    [Fact]
    public async Task Wellformed_Message_Runs_GenerateInvoice_Command_Via_Mediator()
    {
        ArrangeTenantEmployee();
        GenerateInvoice.Command? captured = null;
        _mediator
            .Setup(m => m.Send(It.IsAny<GenerateInvoice.Command>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((cmd, _) => captured = (GenerateInvoice.Command)cmd)
            .ReturnsAsync(BusinessResult.Success(new GenerateInvoice.Response("INV-1")));

        await CreateHandler().HandleAsync(Enveloped(EmployeeId, PayPeriodId, TenantId), CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<GenerateInvoice.Command>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(EmployeeId, captured!.EmployeeId);
        Assert.Equal(PayPeriodId, captured.PayPeriodId);
    }

    [Fact]
    public async Task Bare_Body_Is_Still_Processed_Backward_Compatible()
    {
        ArrangeTenantEmployee();
        ArrangeMediatorSuccess();

        await CreateHandler().HandleAsync(Bare(EmployeeId, PayPeriodId), CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<GenerateInvoice.Command>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Tenant_Override_Set_From_Looked_Up_Employee_Before_Command()
    {
        ArrangeTenantEmployee(TenantId);
        var overrideSetBeforeSend = false;
        _tenantProvider
            .Setup(t => t.SetTenantOverride(TenantId))
            .Callback(() => overrideSetBeforeSend = true);
        _mediator
            .Setup(m => m.Send(It.IsAny<GenerateInvoice.Command>(), It.IsAny<CancellationToken>()))
            .Callback(() => Assert.True(overrideSetBeforeSend,
                "tenant override must be set before the command runs so child writes are stamped"))
            .ReturnsAsync(BusinessResult.Success(new GenerateInvoice.Response("INV-1")));

        await CreateHandler().HandleAsync(Enveloped(EmployeeId, PayPeriodId, TenantId), CancellationToken.None);

        _tenantProvider.Verify(t => t.SetTenantOverride(TenantId), Times.Once);
    }

    [Fact]
    public async Task Single_Tenant_Employee_Does_Not_Set_Override()
    {
        ArrangeTenantEmployee(tenantId: null);
        ArrangeMediatorSuccess();

        await CreateHandler().HandleAsync(Enveloped(EmployeeId, PayPeriodId, null), CancellationToken.None);

        _tenantProvider.Verify(t => t.SetTenantOverride(It.IsAny<string>()), Times.Never);
        _mediator.Verify(
            m => m.Send(It.IsAny<GenerateInvoice.Command>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Validator_Rejection_Is_Acked_Not_Thrown()
    {
        ArrangeTenantEmployee();
        _mediator
            .Setup(m => m.Send(It.IsAny<GenerateInvoice.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<GenerateInvoice.Response>(
                new Error("PayPeriodId", BusinessErrorMessage.InvoiceAlreadyExists)));

        var ex = await Record.ExceptionAsync(
            () => CreateHandler().HandleAsync(Enveloped(EmployeeId, PayPeriodId, TenantId), CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Infra_Failure_From_Command_Is_Rethrown_For_Retry()
    {
        ArrangeTenantEmployee();
        _mediator
            .Setup(m => m.Send(It.IsAny<GenerateInvoice.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateHandler().HandleAsync(Enveloped(EmployeeId, PayPeriodId, TenantId), CancellationToken.None));
    }

    [Fact]
    public async Task Employee_Not_Found_Stays_Transient_Throws()
    {
        _employeeRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateHandler().HandleAsync(Enveloped(EmployeeId, PayPeriodId, TenantId), CancellationToken.None));
        _mediator.Verify(
            m => m.Send(It.IsAny<GenerateInvoice.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Missing_Ids_Are_Acked_Not_Sent()
    {
        var ex = await Record.ExceptionAsync(
            () => CreateHandler().HandleAsync("{}", CancellationToken.None));

        Assert.Null(ex);
        _employeeRepository.Verify(
            r => r.GetByIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(
            m => m.Send(It.IsAny<GenerateInvoice.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Twice_With_Same_Message_Is_Safe_To_Run_Twice()
    {
        ArrangeTenantEmployee();
        // First pass: invoice created. Second pass: the validator's ExistsForPayPeriodAsync guard
        // rejects (InvoiceAlreadyExists) — the consumer acks as a no-op. The terminal effect happens once.
        _mediator
            .SetupSequence(m => m.Send(It.IsAny<GenerateInvoice.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new GenerateInvoice.Response("INV-1")))
            .ReturnsAsync(BusinessResult.Failure<GenerateInvoice.Response>(
                new Error("PayPeriodId", BusinessErrorMessage.InvoiceAlreadyExists)));

        var body = Enveloped(EmployeeId, PayPeriodId, TenantId);
        var first = await Record.ExceptionAsync(() => CreateHandler().HandleAsync(body, CancellationToken.None));
        var second = await Record.ExceptionAsync(() => CreateHandler().HandleAsync(body, CancellationToken.None));

        Assert.Null(first);
        Assert.Null(second);
    }
}
