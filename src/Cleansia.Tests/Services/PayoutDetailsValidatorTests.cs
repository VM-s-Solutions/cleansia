using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// ADR-0034 D4 — payout validation is real and FAILS CLOSED. The pre-change rule was
/// <c>NotEmpty() + Length(15, 34)</c> on all three write paths, so <c>"totally not an iban!!"</c> and a
/// 16-digit card number both passed and would have been printed on a payout invoice a human keys a
/// transfer from.
/// </summary>
public class PayoutDetailsValidatorTests
{
    private const string CzCountryId = "country-cz";
    private const string SkCountryId = "country-sk";
    private const string DeCountryId = "country-de";
    private const string OwnerAccount = "5885638003";
    private const string OwnerBankCode = "5500";
    private const string OwnerIban = "CZ3155000000005885638003";

    private readonly Mock<ICountryRepository> _countries = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurations = new();

    public PayoutDetailsValidatorTests()
    {
        Register(CzCountryId, "CZE", PayoutScheme.CzskDomesticWithIban);
        Register(SkCountryId, "SVK", PayoutScheme.CzskDomesticWithIban);
        Register(DeCountryId, "DEU", configuredScheme: null);
    }

    private void Register(string countryId, string isoCode, PayoutScheme? configuredScheme)
    {
        var country = Country.Create(isoCode, isoCode);
        country.Id = countryId;

        _countries.Setup(r => r.GetByIdAsync(countryId, It.IsAny<CancellationToken>())).ReturnsAsync(country);
        _countries.Setup(r => r.GetByIsoCodeAsync(isoCode, It.IsAny<CancellationToken>())).ReturnsAsync(country);

        var configuration = CountryConfiguration.Create(countryId, "CZK", "cs", 21m);
        if (configuredScheme is not null)
        {
            typeof(CountryConfiguration)
                .GetProperty(nameof(CountryConfiguration.PayoutScheme))!
                .SetValue(configuration, configuredScheme);
        }

        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync(countryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
    }

    private IPayoutDetailsValidator CreateValidator() =>
        new PayoutDetailsValidator(_countries.Object, _countryConfigurations.Object);

    private static PayoutDetailsInput Domestic(
        string? accountNumber = OwnerAccount,
        string? bankCode = OwnerBankCode,
        string? prefix = null,
        string? iban = null,
        string? swift = null,
        string bankCountryId = CzCountryId,
        string? workCountryId = CzCountryId) =>
        new(bankCountryId, workCountryId, prefix, accountNumber, bankCode, iban, swift, null, null);

    [Fact]
    public async Task The_Owners_Own_Account_Is_Accepted_And_Its_Iban_Is_Derived()
    {
        var result = await CreateValidator().ValidateAsync(Domestic());

        Assert.True(result.IsValid);
        Assert.Equal(PayoutScheme.CzskDomesticWithIban, result.Canonical!.Scheme);
        Assert.Equal(OwnerIban, result.Canonical.Iban);
        Assert.Equal(OwnerAccount, result.Canonical.AccountNumber);
        Assert.Equal(OwnerBankCode, result.Canonical.BankCode);
        Assert.Null(result.Canonical.AccountPrefix);
    }

    [Fact]
    public async Task A_Transposed_Digit_Is_Rejected()
    {
        var result = await CreateValidator().ValidateAsync(Domestic(accountNumber: "5885368003"));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.PayoutInvalidAccountNumber, result.ErrorKey);
    }

    [Fact]
    public async Task A_Prefixed_Account_Is_Accepted_And_Both_Parts_Are_Zero_Padded()
    {
        var result = await CreateValidator().ValidateAsync(
            Domestic(accountNumber: "2000145399", bankCode: "0800", prefix: "19"));

        Assert.True(result.IsValid);
        Assert.Equal("000019", result.Canonical!.AccountPrefix);
        Assert.Equal("2000145399", result.Canonical.AccountNumber);
        Assert.Equal("CZ6508000000192000145399", result.Canonical.Iban);
    }

    [Fact]
    public async Task An_Invalid_Prefix_Is_Rejected_On_Its_Own_Key()
    {
        var result = await CreateValidator().ValidateAsync(
            Domestic(accountNumber: "2000145399", bankCode: "0800", prefix: "18"));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.PayoutInvalidAccountPrefix, result.ErrorKey);
    }

    [Fact]
    public async Task A_Supplied_Iban_That_Disagrees_With_The_Derived_One_Is_Rejected()
    {
        var result = await CreateValidator().ValidateAsync(
            Domestic(iban: "CZ6508000000192000145399"));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.PayoutIbanMismatch, result.ErrorKey);
    }

    [Fact]
    public async Task A_Supplied_Iban_That_Agrees_Is_Accepted_However_It_Is_Spaced()
    {
        var result = await CreateValidator().ValidateAsync(
            Domestic(iban: "CZ31 5500 0000 0058 8563 8003"));

        Assert.True(result.IsValid);
        Assert.Equal(OwnerIban, result.Canonical!.Iban);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_Missing_Account_Number_Is_Rejected(string? accountNumber)
    {
        var result = await CreateValidator().ValidateAsync(Domestic(accountNumber: accountNumber));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.PayoutAccountNumberRequired, result.ErrorKey);
    }

    [Theory]
    [InlineData("55000")]
    [InlineData("55X0")]
    [InlineData(null)]
    public async Task A_Malformed_Bank_Code_Is_Rejected(string? bankCode)
    {
        var result = await CreateValidator().ValidateAsync(Domestic(bankCode: bankCode));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.PayoutInvalidBankCode, result.ErrorKey);
    }

    [Fact]
    public async Task The_String_That_Passes_Today_Is_Rejected()
    {
        var result = await CreateValidator().ValidateAsync(new PayoutDetailsInput(
            DeCountryId, DeCountryId, null, null, null, "totally not an iban!!", null, null, null));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.PayoutCountryNotSupported, result.ErrorKey);
    }

    [Fact]
    public async Task A_Luhn_Valid_Card_Number_Is_Rejected_As_A_Card_Not_As_A_Bad_Account()
    {
        var result = await CreateValidator().ValidateAsync(Domestic(accountNumber: "4111111111111111"));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.PayoutLooksLikeCard, result.ErrorKey);
    }

    [Fact]
    public async Task A_Card_Number_Typed_Into_The_Iban_Field_Is_Rejected_Too()
    {
        var result = await CreateValidator().ValidateAsync(new PayoutDetailsInput(
            DeCountryId, DeCountryId, null, null, null, "4111 1111 1111 1111", null, null, null));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.PayoutLooksLikeCard, result.ErrorKey);
    }

    [Fact]
    public async Task An_Unconfigured_Country_Still_Accepts_A_SelfDescribing_Iban()
    {
        var result = await CreateValidator().ValidateAsync(new PayoutDetailsInput(
            DeCountryId, DeCountryId, null, null, null, "DE89370400440532013000", null, null, null));

        Assert.True(result.IsValid);
        Assert.Equal(PayoutScheme.SepaIban, result.Canonical!.Scheme);
        Assert.Equal("DE89370400440532013000", result.Canonical.Iban);
    }

    [Fact]
    public async Task An_Iban_From_A_Different_Country_Than_The_Bank_Is_Rejected()
    {
        var result = await CreateValidator().ValidateAsync(new PayoutDetailsInput(
            DeCountryId, DeCountryId, null, null, null, OwnerIban, null, null, null));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.PayoutCountryNotSupported, result.ErrorKey);
    }

    [Fact]
    public async Task An_Unknown_Bank_Country_With_A_Non_Iban_Value_Fails_Closed()
    {
        _countries
            .Setup(r => r.GetByIdAsync("country-zz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Atlantis", "ZZZ"));

        var result = await CreateValidator().ValidateAsync(new PayoutDetailsInput(
            "country-zz", null, null, "5885638003", "5500", null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.PayoutCountryNotSupported, result.ErrorKey);
    }

    [Fact]
    public async Task Slovakia_Is_The_Same_Scheme_And_Costs_One_Seed_Value()
    {
        var result = await CreateValidator().ValidateAsync(Domestic(
            accountNumber: "8742637541", bankCode: "1200", prefix: "19",
            bankCountryId: SkCountryId, workCountryId: SkCountryId));

        Assert.True(result.IsValid);
        Assert.Equal(PayoutScheme.CzskDomesticWithIban, result.Canonical!.Scheme);
        Assert.StartsWith("SK", result.Canonical.Iban);
    }

    [Fact]
    public async Task A_Well_Formed_Swift_For_The_Bank_Country_Is_Accepted()
    {
        var result = await CreateValidator().ValidateAsync(Domestic(swift: "RZBCCZPP"));

        Assert.True(result.IsValid);
        Assert.Equal("RZBCCZPP", result.Canonical!.Swift);
    }

    [Theory]
    [InlineData("RZBC")]
    [InlineData("RZBCDEPP")]
    [InlineData("1ZBCCZPP")]
    public async Task A_Malformed_Or_Wrong_Country_Swift_Is_Rejected(string swift)
    {
        var result = await CreateValidator().ValidateAsync(Domestic(swift: swift));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.PayoutInvalidSwift, result.ErrorKey);
    }

    [Fact]
    public async Task A_CrossBorder_Bank_Requires_A_Swift()
    {
        var result = await CreateValidator().ValidateAsync(Domestic(
            accountNumber: "8742637541", bankCode: "1200", prefix: "19",
            bankCountryId: SkCountryId, workCountryId: CzCountryId));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.PayoutSwiftRequired, result.ErrorKey);
    }

    [Fact]
    public async Task A_CrossBorder_Bank_With_A_Swift_Is_Accepted()
    {
        var result = await CreateValidator().ValidateAsync(Domestic(
            accountNumber: "8742637541", bankCode: "1200", prefix: "19", swift: "TATRSKBX",
            bankCountryId: SkCountryId, workCountryId: CzCountryId));

        Assert.True(result.IsValid);
        Assert.Equal("TATRSKBX", result.Canonical!.Swift);
    }
}
