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
    public record Command(string InvoiceId) : ICommand<Response>;

    public record Response(string PdfBlobUrl);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IEmployeeInvoiceRepository _invoiceRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IInvoiceTemplateRepository _templateRepository;
        private readonly ICurrencyRepository _currencyRepository;

        public Validator(
            IEmployeeInvoiceRepository invoiceRepository,
            IEmployeeRepository employeeRepository,
            IInvoiceTemplateRepository templateRepository,
            ICurrencyRepository currencyRepository)
        {
            _invoiceRepository = invoiceRepository;
            _employeeRepository = employeeRepository;
            _templateRepository = templateRepository;
            _currencyRepository = currencyRepository;

            RuleFor(x => x.InvoiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_invoiceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotFound)
                .MustAsync(HasValidEmployeeAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound)
                .MustAsync(HasValidPayPeriodAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotFound)
                .MustAsync(HasValidCurrencyAsync)
                .WithMessage(BusinessErrorMessage.InvalidCurrency)
                .MustAsync(HasAvailableTemplateAsync)
                .WithMessage(BusinessErrorMessage.TemplateNotFound);
        }

        private async Task<bool> HasValidEmployeeAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            if (invoice == null) return false;

            return await _employeeRepository.ExistsAsync(invoice.EmployeeId, cancellationToken);
        }

        private async Task<bool> HasValidPayPeriodAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository
                .GetQueryable()
                .Include(i => i.PayPeriod)
                .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

            return invoice?.PayPeriod != null;
        }

        private async Task<bool> HasValidCurrencyAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            if (invoice == null) return false;

            return await _currencyRepository.ExistsAsync(invoice.CurrencyId, cancellationToken);
        }

        private async Task<bool> HasAvailableTemplateAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            if (invoice == null) return false;

            var employee = await _employeeRepository
                .GetQueryable()
                .Include(e => e.Address)
                .FirstOrDefaultAsync(e => e.Id == invoice.EmployeeId, cancellationToken);

            if (employee == null) return false;

            var countryId = employee.Address?.CountryId ?? "CZE";
            var languageCode = "en";

            var template = await _templateRepository.GetActiveByCountryAndLanguageAsync(
                countryId,
                languageCode,
                cancellationToken);

            return template != null;
        }
    }

    public class Handler(
        IEmployeeInvoiceRepository invoiceRepository,
        IOrderEmployeePayRepository orderPayRepository,
        ICountryInvoiceConfigRepository configRepository,
        ICurrencyRepository currencyRepository,
        IEmployeeRepository employeeRepository,
        IInvoiceTemplateRepository templateRepository,
        IPdfService pdfService,
        ITemplateEngine templateEngine,
        IBlobContainerClientFactory blobFactory)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var invoice = await invoiceRepository
                .GetQueryable()
                .Include(i => i.PayPeriod)
                .FirstAsync(i => i.Id == command.InvoiceId, cancellationToken);

            var employee = await employeeRepository
                .GetQueryable()
                .Include(e => e.Address)
                .Include(e => e.User)
                .FirstAsync(e => e.Id == invoice.EmployeeId, cancellationToken);

            var currency = await currencyRepository.GetByIdAsync(invoice.CurrencyId, cancellationToken);

            var orderPays = await orderPayRepository
                .GetQueryable()
                .Include(op => op.Order)
                .Where(op => op.EmployeeInvoiceId == invoice.Id)
                .ToListAsync(cancellationToken);

            var countryId = employee.Address?.CountryId ?? "CZE";
            // TODO: change hard coded language to the dynamic language system
            var languageCode = "en";

            var countryContext = await GetCountryInvoiceContextAsync(countryId, cancellationToken);

            var pdfData = invoice.CreatePdfData(employee, currency, orderPays, countryContext);

            var templateHtml = await GetTemplateHtmlAsync(countryId, languageCode, cancellationToken);

            var mergedHtml = await templateEngine.CompileAsync(templateHtml, pdfData, cancellationToken);

            var pdfBytes = await pdfService.GenerateInvoicePdfAsync(
                pdfData,
                mergedHtml,
                countryContext,
                cancellationToken);

            var blobUrl = await UploadPdfAsync(invoice, employee, pdfBytes, cancellationToken);

            invoice.SetPdfBlobUrl(blobUrl);

            await invoiceRepository.CommitAsync(cancellationToken);

            return BusinessResult.Success(new Response(blobUrl));
        }

        private async Task<CountryInvoiceContext?> GetCountryInvoiceContextAsync(string countryId, CancellationToken cancellationToken)
        {
            var config = await configRepository.GetByCountryIdAsync(countryId, cancellationToken);
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
            var template = await templateRepository.GetActiveByCountryAndLanguageAsync(
                countryId,
                languageCode,
                cancellationToken);

            var templateBlobClient = blobFactory.GetBlobContainerClient(Constants.BlobContainers.InvoiceTemplates);
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
            var blobClient = blobFactory.GetBlobContainerClient(Constants.BlobContainers.GeneratedInvoices);

            using var pdfStream = new MemoryStream(pdfBytes);
            await blobClient.UploadAsync(blobName, pdfStream, cancellationToken: cancellationToken);

            return blobClient.GetBlobUri(blobName).ToString();
        }
    }
}