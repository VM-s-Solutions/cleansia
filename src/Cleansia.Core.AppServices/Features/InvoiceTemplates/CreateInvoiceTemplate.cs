using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.InvoiceTemplates;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.InvoiceTemplates;

public class CreateInvoiceTemplate
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly string[] AllowedExtensions = { ".html", ".htm" };

    public record Command(
        string TemplateName,
        string CountryId,
        string LanguageId,
        string? Description,
        string? FileName,
        string? ContentType,
        byte[]? FileData) : ICommand<Response>;

    public record Response(string InvoiceTemplateId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            ICountryRepository countryRepository,
            ILanguageRepository languageRepository)
        {
            RuleFor(x => x.TemplateName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.CountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(countryRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.CountryNotFound);

            RuleFor(x => x.LanguageId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(languageRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.LanguageNotFound);

            RuleFor(x => x.Description)
                .MaximumLength(1000)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.FileData)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.TemplateFileRequired);

            RuleFor(x => x.FileName)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .When(x => x.FileData != null && x.FileData.Length > 0);

            RuleFor(x => x.FileData)
                .Must(data => data == null || data.Length <= MaxFileSizeBytes)
                .WithMessage(BusinessErrorMessage.TemplateFileSizeExceeded);

            RuleFor(x => x.FileName)
                .Must(fileName =>
                {
                    if (string.IsNullOrEmpty(fileName)) return true;
                    var extension = Path.GetExtension(fileName).ToLowerInvariant();
                    return AllowedExtensions.Contains(extension);
                })
                .WithMessage(BusinessErrorMessage.InvalidTemplateFileType)
                .When(x => !string.IsNullOrEmpty(x.FileName));
        }
    }

    internal class Handler(
        IInvoiceTemplateRepository invoiceTemplateRepository,
        ICountryRepository countryRepository,
        ILanguageRepository languageRepository,
        IBlobContainerClientFactory blobClientFactory)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var nextVersion = await invoiceTemplateRepository.GetNextVersionAsync(
                command.CountryId,
                command.LanguageId,
                cancellationToken);

            // Get country and language for blob path
            var country = await countryRepository.GetByIdAsync(command.CountryId, cancellationToken);
            var language = await languageRepository.GetByIdAsync(command.LanguageId, cancellationToken);

            // Generate blob path: {countryIsoCode}/{languageCode}/{templateName}-v{version}.html
            var sanitizedTemplateName = SanitizeFileName(command.TemplateName);
            var extension = Path.GetExtension(command.FileName).ToLowerInvariant();
            var blobPath = $"{country!.IsoCode.ToLowerInvariant()}/{language!.Code.ToLowerInvariant()}/{sanitizedTemplateName}-v{nextVersion}{extension}";

            // Upload file to blob storage
            var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.InvoiceTemplates);
            using var stream = new MemoryStream(command.FileData!);
            var metadata = Metadata.CreateBuilder()
                .WithMetadata(MetadataName.ContentType, command.ContentType ?? "text/html")
                .Build();
            await blobClient.UploadAsync(blobPath, stream, metadata, cancellationToken);

            var template = InvoiceTemplate.Create(
                command.TemplateName,
                command.CountryId,
                command.LanguageId,
                nextVersion,
                blobPath,
                command.Description);

            invoiceTemplateRepository.Add(template);

            return BusinessResult.Success(new Response(template.Id));
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.Replace(" ", "-").ToLowerInvariant();
        }
    }
}