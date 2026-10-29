using System.Reflection;
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;
using Constants = Cleansia.Core.AppServices.Common.Constants;

namespace Cleansia.Tests.Features.EmployeePayroll;

public class DownloadInvoiceAvailabilityTests
{
    private readonly EmployeeInvoice _invoice = PayrollMockFactory.Invoice();
    private readonly Mock<IEmployeeInvoiceRepository> _invoices = new();
    private readonly Mock<IOrderAccessService> _access = new();
    private readonly Mock<IBlobContainerClientFactory> _factory = new();
    private readonly Mock<IBlobContainerClient> _blobs = new();

    public DownloadInvoiceAvailabilityTests()
    {
        _invoices.Setup(r => r.GetByIdAsync(_invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_invoice);
        _access.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_invoice.EmployeeId);
        _factory.Setup(f => f.GetBlobContainerClient(Constants.BlobContainers.GeneratedInvoices)).Returns(_blobs.Object);
        _blobs.Setup(b => b.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _blobs.Setup(b => b.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BlobFile(new MemoryStream([1, 2, 3]), "application/pdf"));
    }

    [Fact]
    public async Task Owned_Invoice_With_No_Pdf_Is_Pending_Instead_Of_Not_Found()
    {
        var result = await Download();

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvoicePdfNotReady, result.Error!.Message);
        _factory.Verify(f => f.GetBlobContainerClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Failed_Generation_With_No_Pdf_Uses_The_Generation_Error()
    {
        _invoice.SetPdfGenerationError("render failed");

        var result = await Download();

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PdfGenerationFailed, result.Error!.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Foreign_Invoice_Never_Reveals_Pdf_Readiness(bool hasPdf)
    {
        if (hasPdf) _invoice.SetPdfBlobUrl("previous.pdf");
        _access.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync("another-employee");

        var result = await Download();

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvoiceNotFound, result.Error!.Message);
        _factory.Verify(f => f.GetBlobContainerClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Missing_Invoice_Remains_Not_Found()
    {
        _invoices.Setup(r => r.GetByIdAsync(_invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync((EmployeeInvoice?)null);

        var result = await Download();

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvoiceNotFound, result.Error!.Message);
    }

    [Fact]
    public async Task Missing_Blob_Is_Unavailable_And_Is_Not_Downloaded()
    {
        _invoice.SetPdfBlobUrl("missing.pdf");
        _blobs.Setup(b => b.ExistsAsync("missing.pdf", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await Download();

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvoicePdfUnavailable, result.Error!.Message);
        _blobs.Verify(b => b.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Failed_Regeneration_Does_Not_Hide_The_Previous_Readable_Document()
    {
        _invoice.SetPdfBlobUrl("previous.pdf");
        _invoice.SetPdfGenerationError("new render failed");

        var result = await Download();

        Assert.True(result.IsSuccess);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Value.PdfBytes);
        Assert.Equal($"Invoice_{_invoice.InvoiceNumber}.pdf", result.Value.FileName);
    }

    [Fact]
    public async Task Storage_Failure_Is_Not_Falsely_Reported_As_Missing()
    {
        _invoice.SetPdfBlobUrl("document.pdf");
        _blobs.Setup(b => b.ExistsAsync("document.pdf", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("storage unavailable"));

        await Assert.ThrowsAsync<IOException>(Download);
    }

    private async Task<BusinessResult<DownloadInvoice.Response>> Download()
    {
        var type = typeof(DownloadInvoice).GetNestedType("Handler", BindingFlags.Public | BindingFlags.NonPublic)!;
        var session = new TestUserSessionProvider("cleaner-user", "cleaner@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Employee.ToString())]);
        var handler = Activator.CreateInstance(type, _invoices.Object, _access.Object, session, _factory.Object)!;
        return await (Task<BusinessResult<DownloadInvoice.Response>>)type.GetMethod("Handle")!.Invoke(handler,
            [new DownloadInvoice.Query(_invoice.Id), CancellationToken.None])!;
    }
}
