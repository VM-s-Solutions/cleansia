#nullable enable
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class DownloadInvoice
{
    public record Query(string InvoiceId) : IRequest<Response?>;

    public record Response(byte[] PdfBytes, string FileName);

    internal class Handler(
        IEmployeeInvoiceRepository invoiceRepository,
        IBlobContainerClientFactory clientFactory)
        : IRequestHandler<Query, Response?>
    {
        public async Task<Response?> Handle(Query request, CancellationToken cancellationToken)
        {
            var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken);

            if (invoice == null || string.IsNullOrEmpty(invoice.PdfBlobUrl))
            {
                return null;
            }

            var blobClient = clientFactory.GetBlobContainerClient(Constants.BlobContainers.GeneratedInvoices);
            var blobDownload = await blobClient.DownloadAsync(invoice.PdfBlobUrl, cancellationToken);

            using var memoryStream = new MemoryStream();
            await blobDownload.Content.CopyToAsync(memoryStream, cancellationToken);
            var pdfBytes = memoryStream.ToArray();

            var fileName = $"Invoice_{invoice.InvoiceNumber}.pdf";

            return new Response(pdfBytes, fileName);
        }
    }
}
