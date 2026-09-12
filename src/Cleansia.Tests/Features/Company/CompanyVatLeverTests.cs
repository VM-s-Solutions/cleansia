using System.Reflection;
using Cleansia.Core.AppServices.Features.Company;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Company;

/// <summary>
/// Turning VAT on, which the platform could not do at all.
///
/// <para><c>CompanyInfo.SetVatPayerStatus</c> had exactly two references in the whole repository — its
/// own definition and one unit test. <c>CompanyInfo.Create</c> did not take the flag and its
/// initializer omitted it, and neither admin command carried it, so every company row was permanently
/// a neplátce. <b>Registering for VAT meant a hand-written UPDATE against the production database</b> —
/// the one operation the owner has forbidden outright — so this was a constraint violation, not a
/// missing feature.</para>
///
/// <para>Neither command had a single test constructing it before this file: adding two required
/// fields to both broke nothing, which is how the gap stayed invisible.</para>
/// </summary>
public class CompanyVatLeverTests
{
    private static readonly DateOnly RegisteredFrom = new(2026, 4, 1);

    private readonly Mock<ICompanyInfoRepository> _repository = new();

    private CompanyInfo? _added;

    public CompanyVatLeverTests() =>
        _repository.Setup(r => r.Add(It.IsAny<CompanyInfo>()))
            .Callback<CompanyInfo>(c => _added = c);

    // Both handlers are internal and no project has InternalsVisibleTo, so they are built the way
    // GetPagedServicesHandlerTests builds its own.
    private static object HandlerFor(Type feature, object repository) =>
        Activator.CreateInstance(
            feature.GetNestedType("Handler", BindingFlags.NonPublic)!, repository)!;

    private async Task<CompanyInfo> CreateAsync(bool isVatPayer, DateOnly? from, string? vatNumber)
    {
        var handler = HandlerFor(typeof(CreateCompanyInfo), _repository.Object);
        var command = new CreateCompanyInfo.Command(
            LegalName: "Cleansia s.r.o.",
            TradingName: "Cleansia",
            Tagline: null,
            RegistrationNumber: "12345678",
            VatNumber: vatNumber,
            Street: "Dlouha 1",
            City: "Praha",
            ZipCode: "11000",
            CountryId: "cz",
            Phone: null,
            Email: null,
            Website: null,
            BankName: null,
            BankAccountNumber: null,
            Iban: null,
            Swift: null,
            IsVatPayer: isVatPayer,
            VatRegisteredFrom: from);

        await (Task<BusinessResult<CreateCompanyInfo.Response>>)handler.GetType()
            .GetMethod("Handle")!.Invoke(handler, [command, CancellationToken.None])!;

        Assert.NotNull(_added);
        return _added!;
    }

    private async Task<CompanyInfo> UpdateAsync(
        CompanyInfo existing, bool isVatPayer, DateOnly? from, string? vatNumber)
    {
        _repository
            .Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = HandlerFor(typeof(UpdateCompanyInfo), _repository.Object);
        var command = new UpdateCompanyInfo.Command(
            CompanyInfoId: existing.Id,
            LegalName: existing.LegalName,
            TradingName: existing.TradingName,
            Tagline: null,
            RegistrationNumber: existing.RegistrationNumber,
            VatNumber: vatNumber,
            Street: existing.Street,
            City: existing.City,
            ZipCode: existing.ZipCode,
            CountryId: existing.CountryId,
            Phone: null,
            Email: null,
            Website: null,
            BankName: null,
            BankAccountNumber: null,
            Iban: null,
            Swift: null,
            IsVatPayer: isVatPayer,
            VatRegisteredFrom: from);

        var result = await (Task<BusinessResult<UpdateCompanyInfo.Response>>)handler.GetType()
            .GetMethod("Handle")!.Invoke(handler, [command, CancellationToken.None])!;

        Assert.True(result.IsSuccess, $"UpdateCompanyInfo failed with: {result.Error?.Message}");
        return existing;
    }

    /// <summary>
    /// THE LEVER. A company created as a VAT payer IS one, with the date the registration took effect —
    /// and before this chunk the same command could only ever produce <c>false</c>.
    /// </summary>
    [Fact]
    public async Task A_Company_Can_Be_Created_As_A_Vat_Payer()
    {
        var company = await CreateAsync(isVatPayer: true, from: RegisteredFrom, vatNumber: "CZ12345678");

        Assert.True(company.IsVatPayer);
        Assert.Equal(RegisteredFrom, company.VatRegisteredFrom);
        Assert.Equal("CZ12345678", company.VatNumber);
    }

    /// <summary>Anti-vacuity: the same command still produces a neplátce when asked to.</summary>
    [Fact]
    public async Task A_Company_Created_As_A_Neplatce_Is_One()
    {
        var company = await CreateAsync(isVatPayer: false, from: null, vatNumber: null);

        Assert.False(company.IsVatPayer);
        Assert.Null(company.VatRegisteredFrom);
    }

    /// <summary>
    /// A NEPLÁTCE KEEPS NO VAT NUMBER, even when the form supplies one. Under §108 ZDPH anyone who
    /// states VAT on a document owes it with no deduction, so a stale number on a non-payer is a
    /// liability rather than an untidy field.
    /// </summary>
    [Fact]
    public async Task Creating_A_Neplatce_With_A_Vat_Number_Clears_It()
    {
        var company = await CreateAsync(isVatPayer: false, from: null, vatNumber: "CZ12345678");

        Assert.False(company.IsVatPayer);
        Assert.Null(company.VatNumber);
    }

    [Fact]
    public async Task Registration_Can_Be_Turned_On_Later()
    {
        var company = await CreateAsync(isVatPayer: false, from: null, vatNumber: null);

        await UpdateAsync(company, isVatPayer: true, from: RegisteredFrom, vatNumber: "CZ12345678");

        Assert.True(company.IsVatPayer);
        Assert.Equal(RegisteredFrom, company.VatRegisteredFrom);
        Assert.Equal("CZ12345678", company.VatNumber);
    }

    /// <summary>
    /// THE ORDERING, which is the trap in the update handler. <c>SetVatPayerStatus(false)</c> clears the
    /// VAT number, so it has to run AFTER <c>UpdateTaxInfo</c> — which is the line that would otherwise
    /// put the number straight back onto a company that is no longer registered. A handler with the two
    /// calls the other way round passes every other test in this file and fails this one.
    /// </summary>
    [Fact]
    public async Task Turning_Registration_Off_Clears_The_Number_Even_When_The_Form_Still_Sends_One()
    {
        var company = await CreateAsync(isVatPayer: true, from: RegisteredFrom, vatNumber: "CZ12345678");

        await UpdateAsync(company, isVatPayer: false, from: RegisteredFrom, vatNumber: "CZ12345678");

        Assert.False(company.IsVatPayer);
        Assert.Null(company.VatNumber);
    }

    /// <summary>
    /// ...and the date goes with it. A company that is not registered has no date on which it became
    /// registered, and a stale one would make the next flip look like a re-registration on the old date
    /// — on the field every VAT return is filed against.
    /// </summary>
    [Fact]
    public async Task Turning_Registration_Off_Clears_The_Date()
    {
        var company = await CreateAsync(isVatPayer: true, from: RegisteredFrom, vatNumber: "CZ12345678");

        await UpdateAsync(company, isVatPayer: false, from: RegisteredFrom, vatNumber: null);

        Assert.Null(company.VatRegisteredFrom);
    }
}
