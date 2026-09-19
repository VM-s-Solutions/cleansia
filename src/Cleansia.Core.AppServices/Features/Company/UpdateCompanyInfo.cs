using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Company;

public class UpdateCompanyInfo
{
    public record Command(
        string CompanyInfoId,
        string LegalName,
        string TradingName,
        string? Tagline,
        string RegistrationNumber,
        string? VatNumber,
        string Street,
        string City,
        string ZipCode,
        string CountryId,
        string? Phone,
        string? Email,
        string? Website,
        string? BankName,
        string? BankAccountNumber,
        string? Iban,
        string? Swift,
        // THE VAT LEVER. Absent from both commands until now, which is why SetVatPayerStatus had no
        // production caller and every company row was permanently a neplatce -- turning VAT on meant a
        // hand-written UPDATE against production, the one operation the owner has forbidden. The date
        // rides alongside because it is unrecoverable once the flag has flipped.
        bool IsVatPayer,
        DateOnly? VatRegisteredFrom) : ICommand<Response>;

    public record Response(string Id);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICountryRepository countryRepository, ICompanyInfoRepository companyInfoRepository)
        {
            RuleFor(x => x.CompanyInfoId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(companyInfoRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.CompanyInfoNotFound);

            RuleFor(x => x.LegalName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(200)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.TradingName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(200)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Tagline)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.RegistrationNumber)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.VatNumber)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Street)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.City)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.ZipCode)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(20)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.CountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) =>
                    await countryRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CountryNotFound)
                .MustAsync(async (command, countryId, ct) =>
                    !await companyInfoRepository.ExistsActiveForCountryExcludingAsync(countryId, command.CompanyInfoId, ct))
                .WithMessage(BusinessErrorMessage.CompanyInfoExistsForCountry);

            RuleFor(x => x.Phone)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Email)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .EmailAddress()
                .When(x => !string.IsNullOrEmpty(x.Email))
                .WithMessage(BusinessErrorMessage.InvalidEmailFormat);

            RuleFor(x => x.Website)
                .MaximumLength(200)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.BankName)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.BankAccountNumber)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Iban)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Swift)
                .MaximumLength(20)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    internal class Handler(ICompanyInfoRepository companyInfoRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var companyInfo = await companyInfoRepository.GetByIdAsync(command.CompanyInfoId, cancellationToken);

            if (companyInfo is null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.CompanyInfoId), BusinessErrorMessage.CompanyInfoNotFound));
            }

            companyInfo.UpdateLegalInfo(command.LegalName, command.TradingName, command.Tagline);
            companyInfo.UpdateTaxInfo(command.RegistrationNumber, command.VatNumber);
            companyInfo.UpdateAddress(command.Street, command.City, command.ZipCode, command.CountryId);
            companyInfo.UpdateContactInfo(command.Phone, command.Email, command.Website);
            companyInfo.UpdateBankDetails(command.BankName, command.BankAccountNumber, command.Iban, command.Swift);

            // LAST, and after UpdateTaxInfo specifically. SetVatPayerStatus(false) clears the VAT number,
            // so running it here means a company switched off keeps no number behind -- running it
            // BEFORE UpdateTaxInfo would let that line put one straight back on a neplatce, which is how
            // a document ends up stating VAT the company does not owe (SS108 ZDPH).
            companyInfo.SetVatPayerStatus(command.IsVatPayer, command.VatRegisteredFrom);

            return BusinessResult.Success(new Response(companyInfo.Id));
        }
    }
}