using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Cleansia.Core.AppServices.Extensions;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class RegenerateInvoicePdf
{
    public record Command(string InvoiceId, string LanguageCode) : ICommand<Response>;

    public record Response(string PdfBlobUrl);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IEmployeeInvoiceRepository _invoiceRepository;

        public Validator(
            IEmployeeInvoiceRepository invoiceRepository,
            ILanguageRepository languageRepository)
        {
            _invoiceRepository = invoiceRepository;

            // A SETTLED INVOICE IS A FILED DOCUMENT AND IS NOT RE-RENDERED. Owner ruling 2026-09-09.
            //
            // This validator checked only that the invoice and the language existed, so a PAID payout
            // invoice could be re-rendered — and the render OVERWRITES THE PDF IN PLACE, at the URL the
            // cleaner already has. Nothing about the money can move (the amounts and the currency are
            // frozen on the row, and the VAT posture is now fixed by ruling), but the LANGUAGE can:
            // re-rendering a settled invoice in another language replaces the document a cleaner has
            // already filed with their tax return. InvoiceDocumentLanguageTests says in its own words
            // that a copy in another language is a SECOND document; this is what stops the first one
            // being destroyed to make it.
            //
            // The same four-way shape as AssignInvoiceVariableSymbol, minus the two rules that are
            // specific to allocating a payment reference. Both keys already exist and are already
            // translated in all five admin locales — this adds no new error contract.
            RuleFor(x => x.InvoiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(invoiceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotFound)
                .MustAsync(NotCancelledAsync)
                .WithMessage(BusinessErrorMessage.InvoiceAlreadyCancelled)
                .MustAsync(NotPaidAsync)
                .WithMessage(BusinessErrorMessage.InvoiceAlreadyPaid);

            RuleFor(x => x.LanguageCode)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(languageRepository.ExistsWithCodeAsync)
                .WithMessage(BusinessErrorMessage.LanguageNotFound);
        }

        private async Task<bool> NotCancelledAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            return !invoice!.IsCancelled;
        }

        private async Task<bool> NotPaidAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            return invoice!.Status != EmployeeInvoiceStatus.Paid;
        }
    }

    public class Handler(
        IPdfService pdfService,
        ICurrencyRepository currencyRepository,
        IEmployeeRepository employeeRepository,
        ICompanyInfoRepository companyInfoRepository,
        IBlobContainerClientFactory clientFactory,
        IEmployeeInvoiceRepository employeeInvoiceRepository,
        IEmployeePayoutDetailsRepository employeePayoutDetailsRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository,
        ICountryInvoiceConfigRepository countryInvoiceConfigRepository,
        ICountryConfigurationRepository countryConfigurationRepository,
        ILogger<Handler> logger)
        : ICommandHandler<Command, Response>
    {
        private const string EmptyRenderMessage = "PDF generation returned empty result";
        private const string NoCompanyInfoMessage = "No active company info is configured to issue this invoice against";

        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var invoice = await employeeInvoiceRepository.GetByIdAsync(command.InvoiceId, cancellationToken);
            if (invoice is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.InvoiceId), BusinessErrorMessage.InvoiceNotFound));
            }

            try
            {
                var employee = await employeeRepository.GetByIdAsync(invoice.EmployeeId, cancellationToken);
                var currency = await currencyRepository.GetByIdAsync(invoice.CurrencyId, cancellationToken);

                if (employee is null)
                {
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.InvoiceId), BusinessErrorMessage.EmployeeNotFound));
                }

                var countryId = employee.Address?.CountryId;

                // Try to get company info by employee's country, fallback to any active
                var companyInfo = countryId != null
                    ? await companyInfoRepository.GetActiveByCountryAsync(countryId, cancellationToken)
                    : null;
                companyInfo ??= await companyInfoRepository.GetActiveCompanyInfoAsync(cancellationToken);

                if (companyInfo == null)
                {
                    return await RecordFailedRenderAsync(
                        invoice,
                        NoCompanyInfoMessage,
                        new Error(nameof(companyInfoRepository.GetActiveCompanyInfoAsync), BusinessErrorMessage.CompanyInfoNotFound),
                        cancellationToken);
                }

                var orderPays = await orderEmployeePayRepository
                    .GetByInvoiceIdAsync(invoice.Id, cancellationToken);

                var countryContext = await GetCountryInvoiceContextAsync(countryId, cancellationToken);

                var dateFormat = "dd.MM.yyyy";
                if (!string.IsNullOrEmpty(countryId))
                {
                    var countryConfig = await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);
                    if (!string.IsNullOrEmpty(countryConfig?.DateFormat))
                        dateFormat = countryConfig.DateFormat;
                }

                var payoutDetails = await employeePayoutDetailsRepository
                    .GetByEmployeeIdAsync(invoice.EmployeeId, cancellationToken);

                var pdfData = invoice.CreatePdfData(employee, currency, orderPays, countryContext, companyInfo, payoutDetails, dateFormat);

                // The document's language is the JURISDICTION's, not the caller's and not the reader's:
                // it is a tax document, and its legal-notice box is reviewed per country, so a notice
                // rendered in a language chosen elsewhere is indistinguishable from one written for that
                // reader's jurisdiction. Same input as the original render, so a re-render reproduces it.
                var countryCode = employee.Address?.Country?.IsoCode;
                var pdfBytes = pdfService.GenerateInvoicePdf(pdfData, countryContext, countryCode);

                // Uploading this would replace a readable document with an unopenable one under the
                // same blob name, and leave the row pointing at it.
                if (pdfBytes is null or { Length: 0 })
                {
                    return await RecordFailedRenderAsync(
                        invoice,
                        EmptyRenderMessage,
                        new Error(nameof(command.InvoiceId), BusinessErrorMessage.PdfGenerationFailed),
                        cancellationToken);
                }

                var blobUrl = await UploadPdfAsync(invoice, employee, pdfBytes, cancellationToken);

                invoice.SetPdfBlobUrl(blobUrl);
                invoice.ClearPdfGenerationError();

                return BusinessResult.Success(new Response(blobUrl));
            }
            // A cancelled request is not a failed render: nothing was attempted to completion and the
            // recording write would be cancelled too, so it propagates rather than stamping the row.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to regenerate the PDF for invoice {InvoiceId}", invoice.Id);

                return await RecordFailedRenderAsync(
                    invoice,
                    ex.Message,
                    new Error(nameof(command.InvoiceId), BusinessErrorMessage.PdfGenerationFailed),
                    cancellationToken);
            }
        }

        // The failure state cannot ride the pipeline: UnitOfWorkPipelineBehavior commits SUCCESSFUL
        // commands only, so a refusal that merely stages SetPdfGenerationError writes nothing and the
        // row goes on reading healthy while its stored document is stale. Flush it here. The flush's
        // own failure stays inside — the render failure is the outcome the caller must see, and the
        // compensation must not replace a handled refusal with a 500 that hides it.
        private async Task<BusinessResult<Response>> RecordFailedRenderAsync(
            EmployeeInvoice invoice,
            string error,
            Error responseError,
            CancellationToken cancellationToken)
        {
            invoice.SetPdfGenerationError(error);

            try
            {
                await employeeInvoiceRepository.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not record the failed PDF render on invoice {InvoiceId}", invoice.Id);
            }

            return BusinessResult.Failure<Response>(responseError);
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
                LegalDisclaimerTemplate = config.LegalDisclaimerTemplate,
                LegalDisclaimerLanguageCode = config.LegalDisclaimerLanguageCode,
                LegalDisclaimerReviewStatus = config.LegalDisclaimerReviewStatus,
                ConstantSymbol = config.ConstantSymbol
            };
        }

        private async Task<string> UploadPdfAsync(EmployeeInvoice invoice, Employee employee, byte[] pdfBytes, CancellationToken cancellationToken)
        {
            // THE EMPLOYEE'S ID, NOT THEIR NAME. The path used to interpolate FirstName_LastName, which
            // is mutable: a cleaner who married and changed their surname made the next render write to
            // a NEW path, leaving the original PDF orphaned at the old URL and moving PdfBlobUrl to a
            // document with different contents. "Overwrites in place" was only true while nobody's name
            // changed. Less readable when browsing the container, and stable, which is the property a
            // document's address needs.
            var employeeFolder = employee.Id;
            var payPeriodDescription = invoice.PayPeriod!.GetPeriodLabel();
            var invoiceFileName = invoice.InvoiceNumber;

            var blobName = $"{payPeriodDescription}/{employeeFolder}/{invoiceFileName}.pdf";
            var blobClient = clientFactory.GetBlobContainerClient(Constants.BlobContainers.GeneratedInvoices);

            using var pdfStream = new MemoryStream(pdfBytes);
            await blobClient.UploadAsync(blobName, pdfStream, cancellationToken: cancellationToken);

            return blobClient.GetBlobUri(blobName).ToString();
        }
    }
}