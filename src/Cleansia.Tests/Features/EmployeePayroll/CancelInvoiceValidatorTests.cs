using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

public class CancelInvoiceValidatorTests
{
    private readonly CancelInvoice.Validator _validator;

    public CancelInvoiceValidatorTests()
    {
        var invoices = new Mock<IEmployeeInvoiceRepository>();
        invoices.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _validator = new CancelInvoice.Validator(invoices.Object);
    }

    [Fact]
    public async Task Reason_Over_1000_Characters_Fails_With_MaxLength()
    {
        var result = await _validator.ValidateAsync(new CancelInvoice.Command("invoice-1", new string('x', 1001)));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MaxLength);
    }

    [Fact]
    public async Task Reason_Of_1000_Characters_Passes()
    {
        var result = await _validator.ValidateAsync(new CancelInvoice.Command("invoice-1", new string('x', 1000)));

        Assert.True(result.IsValid);
    }
}
