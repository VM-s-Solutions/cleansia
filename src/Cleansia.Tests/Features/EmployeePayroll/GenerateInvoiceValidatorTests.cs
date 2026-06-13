using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// Every failure path of <see cref="GenerateInvoice.Validator"/> plus the clean pass:
/// required-field guards, existence guards, the InvoiceAlreadyExists idempotency guard
///, the NoUnpaidOrderPays guard, and a fully valid command. Asserts on the
/// BusinessErrorMessage constant, never a literal.
/// </summary>
public class GenerateInvoiceValidatorTests
{
    private const string EmployeeId = "emp-1";
    private const string PayPeriodId = "period-1";

    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();

    public GenerateInvoiceValidatorTests()
    {
        _employeeRepository
            .Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payPeriodRepository
            .Setup(r => r.ExistsAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _invoiceRepository
            .Setup(r => r.ExistsForPayPeriodAsync(EmployeeId, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderPayRepository
            .Setup(r => r.HasUnassignedForEmployeePeriodAsync(EmployeeId, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private GenerateInvoice.Validator CreateValidator() => new(
        _employeeRepository.Object,
        _payPeriodRepository.Object,
        _invoiceRepository.Object,
        _orderPayRepository.Object);

    private static GenerateInvoice.Command Valid() => new(EmployeeId, PayPeriodId);

    [Fact]
    public async Task Valid_Command_Passes()
    {
        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_EmployeeId_Fails_Required(string employeeId)
    {
        var result = await CreateValidator().ValidateAsync(Valid() with { EmployeeId = employeeId });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(GenerateInvoice.Command.EmployeeId)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_PayPeriodId_Fails_Required(string payPeriodId)
    {
        var result = await CreateValidator().ValidateAsync(Valid() with { PayPeriodId = payPeriodId });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(GenerateInvoice.Command.PayPeriodId)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task Unknown_Employee_Fails_EmployeeNotFound()
    {
        _employeeRepository
            .Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(GenerateInvoice.Command.EmployeeId)
            && e.ErrorMessage == BusinessErrorMessage.EmployeeNotFound);
    }

    [Fact]
    public async Task Unknown_PayPeriod_Fails_PayPeriodNotFound()
    {
        _payPeriodRepository
            .Setup(r => r.ExistsAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(GenerateInvoice.Command.PayPeriodId)
            && e.ErrorMessage == BusinessErrorMessage.PayPeriodNotFound);
    }

    [Fact]
    public async Task Existing_Invoice_For_Period_Fails_InvoiceAlreadyExists()
    {
        _invoiceRepository
            .Setup(r => r.ExistsForPayPeriodAsync(EmployeeId, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvoiceAlreadyExists);
    }

    [Fact]
    public async Task No_Unassigned_Order_Pays_Fails_NoUnpaidOrderPays()
    {
        _orderPayRepository
            .Setup(r => r.HasUnassignedForEmployeePeriodAsync(EmployeeId, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NoUnpaidOrderPays);
    }
}
