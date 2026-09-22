using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class DownloadInvoice
{
    public class Validator : AbstractValidator<Query>
    {
        public Validator(IEmployeeInvoiceRepository employeeInvoiceRepository)
        {
            RuleFor(x => x.InvoiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeInvoiceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotFound);
        }
    }

    public record Query(string InvoiceId) : IQuery<Response>;

    public record Response(byte[] PdfBytes, string FileName);

    internal class Handler(
        IEmployeeInvoiceRepository invoiceRepository,
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider,
        IBlobContainerClientFactory clientFactory)
        : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken);

            if (invoice == null || string.IsNullOrEmpty(invoice.PdfBlobUrl))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(request.InvoiceId), BusinessErrorMessage.InvoiceNotFound));
            }

            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            if (role != UserProfile.Administrator.ToString())
            {
                var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
                if (string.IsNullOrEmpty(employeeId) || invoice.EmployeeId != employeeId)
                {
                    return BusinessResult.Failure<Response>(
                        new Error(nameof(request.InvoiceId), BusinessErrorMessage.InvoiceNotFound));
                }
            }

            var blobClient = clientFactory.GetBlobContainerClient(Constants.BlobContainers.GeneratedInvoices);
            var blobDownload = await blobClient.DownloadAsync(BlobNameOf(invoice.PdfBlobUrl), cancellationToken);

            using var memoryStream = new MemoryStream();
            await blobDownload.Content.CopyToAsync(memoryStream, cancellationToken);
            var pdfBytes = memoryStream.ToArray();

            var fileName = $"Invoice_{invoice.InvoiceNumber}.pdf";

            return BusinessResult.Success(new Response(pdfBytes, fileName));
        }

        // The row holds the absolute address the writers get back from the container (the archive
        // copies by it), while the client reads by name within the container: the name is what
        // follows the container segment, unescaped the way the address escaped it.
        private static string BlobNameOf(string pdfBlobUrl)
        {
            if (!Uri.TryCreate(pdfBlobUrl, UriKind.Absolute, out var address))
            {
                return pdfBlobUrl;
            }

            var path = Uri.UnescapeDataString(address.AbsolutePath);
            var container = $"/{Constants.BlobContainers.GeneratedInvoices}/";
            var start = path.IndexOf(container, StringComparison.Ordinal);
            return start < 0 ? pdfBlobUrl : path[(start + container.Length)..];
        }
    }
}
