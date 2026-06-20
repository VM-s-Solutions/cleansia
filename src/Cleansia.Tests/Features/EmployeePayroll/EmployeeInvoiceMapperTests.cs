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
}
