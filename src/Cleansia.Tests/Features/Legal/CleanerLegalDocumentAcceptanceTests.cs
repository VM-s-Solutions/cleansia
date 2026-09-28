using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Legal;

/// <summary>
/// Owner ruling 2026-09-28: a cleaner reads the documents in force for their market and accepts one by
/// echoing the text row they were shown; their consent row for that document moves to its version, and
/// an acceptance row keeps the act after the next version moves the consent row on. A text of another
/// version, or of a customer document, is refused. Until they are approved for a market, their market is
/// the one their address is in — the market they are approved for is checked against the same texts.
/// </summary>
public sealed class CleanerLegalDocumentAcceptanceTests
{
    private const string UserId = "user-cleaner";
    private const string EmployeeId = "employee-cleaner";
    private const string Czechia = "country-cze";
    private const string Slovakia = "country-svk";

    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<ILegalDocumentRepository> _documents = new();
    private readonly Mock<ILegalDocumentResolver> _resolver = new();
    private readonly Mock<IUserConsentRepository> _consents = new();
    private readonly Mock<ICountryConfigurationRepository> _configurations = new();
    private readonly Mock<IConsentService> _consentService = new();
    private readonly Mock<ICleanerLegalDocumentAcceptanceRepository> _acceptances = new();
    private readonly Mock<IRequestMetadataProvider> _requestMetadata = new();

    private readonly LegalDocument _current = FrameworkContract(new DateOnly(2027, 1, 1));
    private readonly LegalDocument _previous = FrameworkContract(new DateOnly(2026, 12, 1));
    private readonly LegalDocument _slovakContract = FrameworkContract(new DateOnly(2027, 2, 1), Slovakia);

    public CleanerLegalDocumentAcceptanceTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _requestMetadata.SetupGet(m => m.IpAddress).Returns("203.0.113.9");
        _requestMetadata.SetupGet(m => m.DeviceLabel).Returns("Android/Pixel");
        ArrangeCleaner(workCountryId: Czechia, addressCountryId: null);

        foreach (var document in new[] { _current, _previous, _slovakContract })
        {
            foreach (var text in document.Texts)
            {
                _documents
                    .Setup(r => r.GetByTextIdWithTextsAsync(text.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(document);
            }
        }

        _resolver
            .Setup(r => r.ResolveInForceAsync(
                LegalDocumentAudience.Employee, LegalDocumentType.CleanerFrameworkContract, Czechia, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_current);
        _resolver
            .Setup(r => r.ResolveInForceAsync(
                LegalDocumentAudience.Employee, LegalDocumentType.CleanerFrameworkContract, Slovakia, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_slovakContract);
        _configurations
            .Setup(r => r.GetByCountryIdAsync(Czechia, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(Czechia, "CZK", "cs", 0.21m));
        _configurations
            .Setup(r => r.GetByCountryIdAsync(Slovakia, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(Slovakia, "EUR", "sk", 0.23m));
    }

    private void ArrangeCleaner(string? workCountryId, string? addressCountryId)
    {
        var user = User.CreateWithPassword("cleaner@example.com", "x", "Cle", "Aner");
        user.Id = UserId;
        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;
        if (workCountryId is not null)
        {
            employee.AssignWorkCountry(workCountryId);
        }

        if (addressCountryId is not null)
        {
            employee.UpdateAddress(Address.Create("Hlavná 1", "Bratislava", "81101", addressCountryId));
        }

        _employees.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());
    }

    private static LegalDocument FrameworkContract(DateOnly effectiveFrom, string? countryId = null)
    {
        var document = LegalDocument.Create(
            LegalDocumentAudience.Employee, LegalDocumentType.CleanerFrameworkContract, countryId, effectiveFrom);
        document.AddText("en", "Framework contract", "## Terms\n\nThe reward is paid in {{currency}}.");
        document.AddText("cs", "Rámcová smlouva", "## Podmínky\n\nOdměna se vyplácí v {{currency}}.");
        return document;
    }

    private AcceptLegalDocument.Validator Validator() => new(_session.Object, _employees.Object, _documents.Object, _resolver.Object);

    private AcceptLegalDocument.Handler Handler() =>
        new(_session.Object, _employees.Object, _documents.Object, _consentService.Object,
            _acceptances.Object, _requestMetadata.Object, new HostAudienceProvider("cleansia.partner"));

    private GetMyLegalDocuments.Handler ReadHandler(ILegalDocumentResolver? resolver = null) =>
        new(_session.Object, _employees.Object, _configurations.Object, resolver ?? _resolver.Object, _consents.Object);

    private static string TextOf(LegalDocument document, string language) => document.TextFor(language)!.Id;

    [Fact]
    public async Task A_Text_Of_The_Document_In_Force_For_The_Cleaners_Market_Is_Accepted()
    {
        var result = await Validator().ValidateAsync(new AcceptLegalDocument.Command(TextOf(_current, "cs")));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task A_Text_Of_A_Version_No_Longer_In_Force_Is_Refused()
    {
        var result = await Validator().ValidateAsync(new AcceptLegalDocument.Command(TextOf(_previous, "en")));

        Assert.Equal(BusinessErrorMessage.LegalDocumentNotInForce, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Text_Of_A_Customer_Document_Is_Refused()
    {
        var terms = LegalDocument.Create(LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, null, new DateOnly(2026, 9, 27));
        terms.AddText("en", "Terms", "## Terms");
        var textId = terms.TextFor("en")!.Id;
        _documents.Setup(r => r.GetByTextIdWithTextsAsync(textId, It.IsAny<CancellationToken>())).ReturnsAsync(terms);

        var result = await Validator().ValidateAsync(new AcceptLegalDocument.Command(textId));

        Assert.Equal(BusinessErrorMessage.LegalDocumentNotInForce, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task An_Unknown_Text_Is_Refused()
    {
        var result = await Validator().ValidateAsync(new AcceptLegalDocument.Command("unknown-text"));

        Assert.Equal(BusinessErrorMessage.LegalDocumentNotInForce, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Caller_Who_Is_Not_A_Cleaner_Accepts_Nothing()
    {
        _employees.Setup(r => r.GetQueryable()).Returns(Array.Empty<Employee>().AsQueryable().BuildMock());

        var result = await Validator().ValidateAsync(new AcceptLegalDocument.Command(TextOf(_current, "cs")));

        Assert.Equal(BusinessErrorMessage.LegalDocumentNotInForce, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task The_Acceptance_Moves_The_Consent_Row_And_Records_The_Act_On_Its_Own_Row()
    {
        _consentService
            .Setup(s => s.TryGrantAsync(UserId, ConsentType.CleanerFrameworkContract, _current, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        CleanerLegalDocumentAcceptance? recorded = null;
        _acceptances.Setup(r => r.Add(It.IsAny<CleanerLegalDocumentAcceptance>())).Callback<CleanerLegalDocumentAcceptance>(a => recorded = a);

        var result = await Handler().Handle(new AcceptLegalDocument.Command(TextOf(_current, "cs")), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(LegalDocumentType.CleanerFrameworkContract, result.Value.Type);
        Assert.Equal("2027-01-01", result.Value.Version);
        Assert.NotNull(recorded);
        Assert.Equal(EmployeeId, recorded!.EmployeeId);
        Assert.Equal(TextOf(_current, "cs"), recorded.LegalDocumentTextId);
        Assert.Equal("2027-01-01", recorded.DocumentVersion);
        Assert.Equal("cleansia.partner", recorded.ClientAudience);
        Assert.Equal("203.0.113.9", recorded.IpAddress);
        Assert.Equal("Android/Pixel", recorded.DeviceLabel);
    }

    [Fact]
    public async Task Accepting_The_Version_Already_Held_Records_Nothing()
    {
        _consentService
            .Setup(s => s.TryGrantAsync(UserId, ConsentType.CleanerFrameworkContract, _current, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await Handler().Handle(new AcceptLegalDocument.Command(TextOf(_current, "cs")), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _acceptances.Verify(r => r.Add(It.IsAny<CleanerLegalDocumentAcceptance>()), Times.Never);
    }

    [Fact]
    public async Task The_Cleaner_Reads_Each_Document_In_Force_With_Whether_Its_Current_Version_Is_Accepted()
    {
        _consents
            .Setup(r => r.GetByUserIdNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([UserConsent.Grant(UserId, ConsentType.CleanerFrameworkContract, "203.0.113.9", "Android", _previous.Version, _previous.Id)]);

        var result = await ReadHandler().Handle(new GetMyLegalDocuments.Query("cs"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var document = Assert.Single(result.Value);
        Assert.Equal(LegalDocumentType.CleanerFrameworkContract, document.Type);
        Assert.Equal(_current.Id, document.LegalDocumentId);
        Assert.Equal(TextOf(_current, "cs"), document.LegalDocumentTextId);
        Assert.Equal("2027-01-01", document.Version);
        Assert.Equal("cs", document.Language);
        Assert.Contains("CZK", document.ContentHtml);
        Assert.False(document.IsAccepted);
        Assert.Equal("2026-12-01", document.AcceptedVersion);
    }

    /// <summary>
    /// A cleaner applying to work in Slovakia is not approved yet, so they have no work country. They read
    /// and accept the Slovak contract — the market their address is in, as their required uploads are —
    /// rather than the default market's, which is not the text they will be approved and paid under.
    /// </summary>
    [Fact]
    public async Task Before_Approval_The_Cleaner_Reads_And_Accepts_The_Texts_Of_The_Market_Their_Address_Is_In()
    {
        ArrangeCleaner(workCountryId: null, addressCountryId: Slovakia);
        _consents.Setup(r => r.GetByUserIdNoTrackingAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var read = await ReadHandler().Handle(new GetMyLegalDocuments.Query("en"), CancellationToken.None);
        var accept = await Validator().ValidateAsync(new AcceptLegalDocument.Command(TextOf(_slovakContract, "en")));

        var document = Assert.Single(read.Value);
        Assert.Equal(_slovakContract.Id, document.LegalDocumentId);
        Assert.Contains("EUR", document.ContentHtml);
        Assert.True(accept.IsValid, string.Join("; ", accept.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task Nothing_In_Force_Reads_As_An_Empty_List()
    {
        var result = await ReadHandler(new Mock<ILegalDocumentResolver>().Object)
            .Handle(new GetMyLegalDocuments.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
