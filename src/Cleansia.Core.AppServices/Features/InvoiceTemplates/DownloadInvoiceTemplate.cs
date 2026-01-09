#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.InvoiceTemplates;

public class DownloadInvoiceTemplate
{
    public record Query(string InvoiceTemplateId) : IQuery<Response>;

    public record Response(byte[] Content, string FileName, string ContentType);

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IInvoiceTemplateRepository invoiceTemplateRepository)
        {
            RuleFor(x => x.InvoiceTemplateId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(invoiceTemplateRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceTemplateNotFound);
        }
    }

    internal class Handler(
        IInvoiceTemplateRepository invoiceTemplateRepository,
        IBlobContainerClientFactory clientFactory)
        : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var template = await invoiceTemplateRepository.GetByIdAsync(query.InvoiceTemplateId, cancellationToken);

            if (template == null || string.IsNullOrEmpty(template.BlobUrl))
            {
                return BusinessResult.Failure<Response>(new Error(nameof(query.InvoiceTemplateId), BusinessErrorMessage.InvoiceTemplateNotFound));
            }

            var blobClient = clientFactory.GetBlobContainerClient(Constants.BlobContainers.InvoiceTemplates);
            var blobDownload = await blobClient.DownloadAsync(template.BlobUrl, cancellationToken);

            using var memoryStream = new MemoryStream();
            await blobDownload.Content.CopyToAsync(memoryStream, cancellationToken);
            var content = memoryStream.ToArray();

            // Determine content type based on file extension
            var fileName = Path.GetFileName(template.BlobUrl);
            var extension = Path.GetExtension(template.BlobUrl).ToLowerInvariant();
            var contentType = extension switch
            {
                ".html" => "text/html",
                ".htm" => "text/html",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };

            return BusinessResult.Success(new Response(content, fileName, contentType));
        }
    }
}