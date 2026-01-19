#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.ReceiptTemplates;

public class DownloadReceiptTemplate
{
    public record Query(string ReceiptTemplateId) : IQuery<Response>;

    public record Response(byte[] Content, string FileName, string ContentType);

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IReceiptTemplateRepository receiptTemplateRepository)
        {
            RuleFor(x => x.ReceiptTemplateId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(receiptTemplateRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.ReceiptTemplateNotFound);
        }
    }

    internal class Handler(
        IReceiptTemplateRepository receiptTemplateRepository,
        IBlobContainerClientFactory clientFactory)
        : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var template = await receiptTemplateRepository.GetByIdAsync(query.ReceiptTemplateId, cancellationToken);

            if (template == null || string.IsNullOrEmpty(template.BlobUrl))
            {
                return BusinessResult.Failure<Response>(new Error(nameof(query.ReceiptTemplateId), BusinessErrorMessage.ReceiptTemplateNotFound));
            }

            var blobClient = clientFactory.GetBlobContainerClient(Constants.BlobContainers.ReceiptTemplates);
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