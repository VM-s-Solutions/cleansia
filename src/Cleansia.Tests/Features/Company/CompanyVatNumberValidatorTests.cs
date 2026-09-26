using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Company;
using Cleansia.Core.Domain.Repositories;
using FluentValidation.TestHelper;
using Moq;

namespace Cleansia.Tests.Features.Company;

public class CompanyVatNumberValidatorTests
{
    private readonly Mock<ICountryRepository> _countries = new();
    private readonly Mock<ICompanyInfoRepository> _companies = new();

    public CompanyVatNumberValidatorTests()
    {
        _countries.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _companies.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Creating_A_Vat_Payer_Requires_Its_Vat_Number(string? number)
    {
        var result = await new CreateCompanyInfo.Validator(_countries.Object, _companies.Object)
            .TestValidateAsync(CreateCommand(isVatPayer: true, number));

        result.ShouldHaveValidationErrorFor(c => c.VatNumber).WithErrorMessage(BusinessErrorMessage.Required).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Updating_A_Vat_Payer_Requires_Its_Vat_Number(string? number)
    {
        var result = await new UpdateCompanyInfo.Validator(_countries.Object, _companies.Object)
            .TestValidateAsync(UpdateCommand(isVatPayer: true, number));

        result.ShouldHaveValidationErrorFor(c => c.VatNumber).WithErrorMessage(BusinessErrorMessage.Required).Only();
    }

    [Theory]
    [InlineData(true, "CZ12345678")]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, "   ")]
    public async Task A_Numbered_Payer_And_An_Unnumbered_Nonpayer_Can_Be_Created_Or_Updated(bool isVatPayer, string? number)
    {
        var created = await new CreateCompanyInfo.Validator(_countries.Object, _companies.Object)
            .TestValidateAsync(CreateCommand(isVatPayer, number));
        var updated = await new UpdateCompanyInfo.Validator(_countries.Object, _companies.Object)
            .TestValidateAsync(UpdateCommand(isVatPayer, number));

        created.ShouldNotHaveAnyValidationErrors();
        updated.ShouldNotHaveAnyValidationErrors();
    }

    private static CreateCompanyInfo.Command CreateCommand(bool isVatPayer, string? number) => new(
        LegalName: "Cleansia s.r.o.", TradingName: "Cleansia", Tagline: null,
        RegistrationNumber: "12345678", VatNumber: number, Street: "Dlouha 1", City: "Praha", ZipCode: "11000",
        CountryId: "cz", Phone: null, Email: null, Website: null, BankName: null, BankAccountNumber: null,
        Iban: null, Swift: null, IsVatPayer: isVatPayer, VatRegisteredFrom: isVatPayer ? new DateOnly(2026, 4, 1) : null);

    private static UpdateCompanyInfo.Command UpdateCommand(bool isVatPayer, string? number) => new(
        CompanyInfoId: "company-vat", LegalName: "Cleansia s.r.o.", TradingName: "Cleansia", Tagline: null,
        RegistrationNumber: "12345678", VatNumber: number, Street: "Dlouha 1", City: "Praha", ZipCode: "11000",
        CountryId: "cz", Phone: null, Email: null, Website: null, BankName: null, BankAccountNumber: null,
        Iban: null, Swift: null, IsVatPayer: isVatPayer, VatRegisteredFrom: isVatPayer ? new DateOnly(2026, 4, 1) : null);
}
