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
    public record Command(string InvoiceId, string LanguageCode) : ICommand<Response>;

    public record Response(string PdfBlobUrl);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            IEmployeeInvoiceRepository invoiceRepository,
            ILanguageRepository languageRepository)
        {
            RuleFor(x => x.InvoiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(invoiceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotFound);

            RuleFor(x => x.LanguageCode)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(languageRepository.ExistsWithCodeAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotFound);
        }
    }

    public class Handler(
        IPdfService pdfService,
        ICurrencyRepository currencyRepository,
        IEmployeeRepository employeeRepository,
        ILanguageRepository languageRepository,
        ICompanyInfoRepository companyInfoRepository,
        IBlobContainerClientFactory clientFactory,
        IEmployeeInvoiceRepository employeeInvoiceRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository,
        ICountryInvoiceConfigRepository countryInvoiceConfigRepository,
        ICountryConfigurationRepository countryConfigurationRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var invoice = await employeeInvoiceRepository.GetByIdAsync(command.InvoiceId, cancellationToken);

            var employee = await employeeRepository.GetByIdAsync(invoice.EmployeeId, cancellationToken);
            var currency = await currencyRepository.GetByIdAsync(invoice.CurrencyId, cancellationToken);

            var countryId = employee.Address?.CountryId;

            // Try to get company info by employee's country, fallback to any active
            var companyInfo = countryId != null
                ? await companyInfoRepository.GetActiveByCountryAsync(countryId, cancellationToken)
                : null;
            companyInfo ??= await companyInfoRepository.GetActiveCompanyInfoAsync(cancellationToken);

            if (companyInfo == null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(companyInfoRepository.GetActiveCompanyInfoAsync), BusinessErrorMessage.CompanyInfoNotFound));
            }

            var orderPays = await orderEmployeePayRepository
                .GetByInvoiceIdAsync(invoice.Id, cancellationToken);
            var language = await languageRepository.GetByCodeAsync(command.LanguageCode, cancellationToken);

            var countryContext = await GetCountryInvoiceContextAsync(countryId, cancellationToken);

            var dateFormat = "dd.MM.yyyy";
            if (!string.IsNullOrEmpty(countryId))
            {
                var countryConfig = await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);
                if (!string.IsNullOrEmpty(countryConfig?.DateFormat))
                    dateFormat = countryConfig.DateFormat;
            }

            var pdfData = invoice.CreatePdfData(employee, currency, orderPays, countryContext, companyInfo, dateFormat);

            var countryCode = employee.Address?.Country?.IsoCode;
            var pdfBytes = pdfService.GenerateInvoicePdf(pdfData, countryContext, countryCode);

            var blobUrl = await UploadPdfAsync(invoice, employee, pdfBytes, cancellationToken);

            invoice.SetPdfBlobUrl(blobUrl);
            invoice.ClearPdfGenerationError();

            return BusinessResult.Success(new Response(blobUrl));
        }

        private async Task<CountryInvoiceContext?> GetCountryInvoiceContextAsync(string? countryId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(countryId)) return null;

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