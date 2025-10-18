using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Infra.Services.Templates;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cleansia.Core.AppServices.Extensions;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class RegenerateInvoicePdf
{
    public record Command(string InvoiceId, string LanguageId) : ICommand<Response>;

    public record Response(string PdfBlobUrl);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ILanguageRepository _languageRepository;
        private readonly IEmployeeInvoiceRepository _invoiceRepository;
        private readonly IInvoiceTemplateRepository _templateRepository;

        public Validator(
            IEmployeeRepository employeeRepository,
            ILanguageRepository languageRepository,
            IEmployeeInvoiceRepository invoiceRepository,
            IInvoiceTemplateRepository templateRepository)
        {
            _invoiceRepository = invoiceRepository;
            _employeeRepository = employeeRepository;
            _languageRepository = languageRepository;
            _templateRepository = templateRepository;

            RuleFor(x => x.InvoiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(invoiceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotFound)
                .MustAsync(HasAvailableTemplateAsync)
                .WithMessage(BusinessErrorMessage.TemplateNotFound);

            RuleFor(x => x.LanguageId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(languageRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotFound);
        }

        private async Task<bool> HasAvailableTemplateAsync(Command command, string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            if (invoice == null)
            {
                return false;
            }

            var employee = await _employeeRepository
                .GetQueryable()
                .Include(e => e.Address)
                .FirstOrDefaultAsync(e => e.Id == invoice.EmployeeId, cancellationToken);

            if (employee == null)
            {
                return false;
            }

            var countryId = employee.Address?.CountryId ?? "CZE";
            var language = await _languageRepository.GetByIdAsync(command.LanguageId, cancellationToken);

            if (language is null)
            {
                return false;
            }

            var template = await _templateRepository.GetActiveByCountryAndLanguageAsync(
                countryId,
                language.Code,
                cancellationToken);

            return template != null;
        }
    }

    public class Handler(
        IPdfService pdfService,
        ITemplateEngine templateEngine,
        ICurrencyRepository currencyRepository,
        IEmployeeRepository employeeRepository,
        ILanguageRepository languageRepository,
        IBlobContainerClientFactory clientFactory,
        IEmployeeInvoiceRepository employeeInvoiceRepository,
        IInvoiceTemplateRepository invoiceTemplateRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository,
        ICountryInvoiceConfigRepository countryInvoiceConfigRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var invoice = await employeeInvoiceRepository.GetByIdAsync(command.InvoiceId, cancellationToken);

            var employee = await employeeRepository.GetByIdAsync(invoice.EmployeeId, cancellationToken);
            var currency = await currencyRepository.GetByIdAsync(invoice.CurrencyId, cancellationToken);

            var orderPays = await orderEmployeePayRepository.GetByInvoiceId(invoice.Id).ToListAsync(cancellationToken);

            var countryId = employee.Address?.CountryId ?? "CZE";
            var language = await languageRepository.GetByIdAsync(command.LanguageId, cancellationToken);

            var countryContext = await GetCountryInvoiceContextAsync(countryId, cancellationToken);

            var pdfData = invoice.CreatePdfData(employee, currency, orderPays, countryContext);

            var templateHtml = await GetTemplateHtmlAsync(countryId, language!.Code, cancellationToken);

            var mergedHtml = await templateEngine.CompileAsync(templateHtml, pdfData, cancellationToken);

            var pdfBytes = await pdfService.GenerateInvoicePdfAsync(
                pdfData,
                mergedHtml,
                countryContext,
                cancellationToken);

            var blobUrl = await UploadPdfAsync(invoice, employee, pdfBytes, cancellationToken);

            invoice.SetPdfBlobUrl(blobUrl);

            return BusinessResult.Success(new Response(blobUrl));
        }

        private async Task<CountryInvoiceContext?> GetCountryInvoiceContextAsync(string countryId, CancellationToken cancellationToken)
        {
            var config = await countryInvoiceConfigRepository.GetByCountryIdAsync(countryId, cancellationToken);
            if (config == null)
            {
                return null;
            }

            return new CountryInvoiceContext
            {
                VatRequired = config.VatRequired,
                VatRate = config.VatRate,
                DigitalSignatureRequired = config.DigitalSignatureRequired,
                EInvoiceFormat = config.EInvoiceFormat,
                LegalDisclaimerTemplate = config.LegalDisclaimerTemplate
            };
        }

        private async Task<string> GetTemplateHtmlAsync(string countryId, string languageCode, CancellationToken cancellationToken)
        {
            var template = await invoiceTemplateRepository.GetActiveByCountryAndLanguageAsync(
                countryId,
                languageCode,
                cancellationToken);

            var templateBlobClient = clientFactory.GetBlobContainerClient(Constants.BlobContainers.InvoiceTemplates);
            var templateBlob = await templateBlobClient.DownloadAsync(template!.BlobUrl, cancellationToken);
            using var reader = new StreamReader(templateBlob.Content);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        private async Task<string> UploadPdfAsync(EmployeeInvoice invoice, Employee employee, byte[] pdfBytes, CancellationToken cancellationToken)
        {
            var employeeName = $"{employee.User?.FirstName}_{employee.User?.LastName}";
            var payPeriodDescription = invoice.PayPeriod!.GetPeriodLabel();
            var invoiceFileName = invoice.InvoiceNumber;

            var blobName = $"{payPeriodDescription}/{employeeName}/{invoiceFileName}.pdf";
            var blobClient = clientFactory.GetBlobContainerClient(Constants.BlobContainers.GeneratedInvoices);

            using var pdfStream = new MemoryStream(pdfBytes);
            await blobClient.UploadAsync(blobName, pdfStream, cancellationToken: cancellationToken);

            return blobClient.GetBlobUri(blobName).ToString();
        }
    }
}