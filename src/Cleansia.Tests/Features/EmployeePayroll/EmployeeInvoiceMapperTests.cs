using Cleansia.Core.AppServices.Mappers;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;

namespace Cleansia.Tests.Features.EmployeePayroll;

public class EmployeeInvoiceMapperTests
{
    [Fact]
    public void MapToDto_Carries_PdfGenerationFailed_And_Error_When_Failed()
    {
        var invoice = PayrollMockFactory.Invoice();
        invoice.SetPdfGenerationError("blob storage timeout");

        var dto = invoice.MapToDto();

        Assert.True(dto.PdfGenerationFailed);
        Assert.Equal("blob storage timeout", dto.PdfGenerationError);
    }

    [Fact]
    public void MapToDto_Defaults_PdfGenerationFailed_False_And_Error_Null_When_Pending()
    {
        var invoice = PayrollMockFactory.Invoice();

        var dto = invoice.MapToDto();

        Assert.False(dto.PdfGenerationFailed);
        Assert.Null(dto.PdfGenerationError);
    }

    [Fact]
    public void MapToDto_Clears_Failure_After_ClearPdfGenerationError()
    {
        var invoice = PayrollMockFactory.Invoice();
        invoice.SetPdfGenerationError("transient");
        invoice.ClearPdfGenerationError();

        var dto = invoice.MapToDto();

        Assert.False(dto.PdfGenerationFailed);
        Assert.Null(dto.PdfGenerationError);
    }

    [Fact]
    public void MapToDto_Carries_The_Invoice_CurrencyId_Beside_Its_Code()
    {
        var invoice = PayrollMockFactory.Invoice();

        var dto = invoice.MapToDto();

        Assert.Equal(invoice.CurrencyId, dto.CurrencyId);
        Assert.Equal(invoice.Currency?.Code ?? "", dto.CurrencyCode);
    }

    [Fact]
    public void MapToDetailDto_Carries_The_Invoice_CurrencyId_Beside_Its_Code()
    {
        var invoice = PayrollMockFactory.Invoice();

        var dto = invoice.MapToDetailDto();

        Assert.Equal(invoice.CurrencyId, dto.CurrencyId);
        Assert.Equal(invoice.Currency?.Code ?? "", dto.CurrencyCode);
    }

    [Fact]
    public void MapToDetailDto_Carries_PdfGenerationFailed_And_Error_When_Failed()
    {
        var invoice = PayrollMockFactory.Invoice();
        invoice.SetPdfGenerationError("blob storage timeout");

        var dto = invoice.MapToDetailDto();

        Assert.True(dto.PdfGenerationFailed);
        Assert.Equal("blob storage timeout", dto.PdfGenerationError);
    }

    [Fact]
    public void MapToDetailDto_Defaults_PdfGenerationFailed_False_And_Error_Null_When_Pending()
    {
        var invoice = PayrollMockFactory.Invoice();

        var dto = invoice.MapToDetailDto();

        Assert.False(dto.PdfGenerationFailed);
        Assert.Null(dto.PdfGenerationError);
    }

    [Fact]
    public void Both_Invoice_Shapes_Carry_Their_Periods_Calendar_Dates_Beside_The_Label()
    {
        var invoice = PayrollMockFactory.Invoice(payPeriod: PayrollMockFactory.OpenPeriod(new DateOnly(2026, 11, 12)));

        var row = invoice.MapToDto();
        var detail = invoice.MapToDetailDto();

        Assert.Equal((new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 25)), (row.PayPeriodStartDate, row.PayPeriodEndDate));
        Assert.Equal((new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 25)), (detail.PayPeriodStartDate, detail.PayPeriodEndDate));
        Assert.Equal(invoice.PayPeriod!.GetPeriodLabel(), row.PayPeriodLabel);
        Assert.Equal(invoice.PayPeriod!.GetPeriodLabel(), detail.PayPeriodLabel);
    }

    [Fact]
    public void MapToDetailDto_Carries_The_Constant_Symbol_It_Is_Given()
    {
        var dto = PayrollMockFactory.Invoice().MapToDetailDto(constantSymbol: "0308");

        Assert.Equal("0308", dto.ConstantSymbol);
    }
}
