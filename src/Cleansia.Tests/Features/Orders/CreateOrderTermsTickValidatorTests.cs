using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Domain.Legal;
using Cleansia.Tests.Features.PayConfig;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The booking's terms gate (owner ruling 2026-09-14, Q-AUD-L4 overruled): a checkout asserts the tick
/// or is refused with <see cref="BusinessErrorMessage.TermsNotAccepted"/>. The one caller excused is a
/// signed-in customer whose account already holds BOTH legal consents granted and not withdrawn — that
/// customer sees no box on any client and sends nothing. A guest has no account to hold a consent on,
/// so a guest always asserts it. The consent read is skipped when the tick is asserted: the tick is
/// the answer, and a customer re-consenting at checkout must not be refused for a record the server
/// has not written yet.
/// </summary>
public sealed class CreateOrderTermsTickValidatorTests
{
    private const string CustomerId = "customer-terms-tick";

    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IUserConsentRepository> _consents = new();
    private readonly Mock<ILegalDocumentResolver> _legalDocuments = new();

    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();

    private void SignedInAs(string? userId) => _session.Setup(s => s.GetUserId()).Returns(userId);

    private void OnRecord(params UserConsent[] consents) =>
        _consents
            .Setup(r => r.GetByUserIdNoTrackingAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consents.ToList());

    private readonly Dictionary<(LegalDocumentType Type, DateOnly EffectiveFrom), LegalDocument> _texts = new();

    // One document per type and date, so a consent and the text in force name the same row exactly
    // when the test says they are the same text.
    private LegalDocument Text(LegalDocumentType type, DateOnly effectiveFrom)
    {
        if (!_texts.TryGetValue((type, effectiveFrom), out var document))
        {
            document = type == LegalDocumentType.PrivacyPolicy
                ? LegalDocumentFixtures.Privacy(effectiveFrom)
                : LegalDocumentFixtures.Terms(effectiveFrom);
            _texts[(type, effectiveFrom)] = document;
        }

        return document;
    }

    private UserConsent Granted(ConsentType type, string? version = "2026-09-14")
    {
        var document = version is null
            ? null
            : Text(type == ConsentType.PrivacyPolicy ? LegalDocumentType.PrivacyPolicy : LegalDocumentType.TermsOfService, DateOnly.Parse(version));
        return UserConsent.Grant(CustomerId, type, "203.0.113.9", "Chrome", document?.Version, document?.Id);
    }

    private void InForce(DateOnly terms, DateOnly privacy) =>
        InForce(Text(LegalDocumentType.TermsOfService, terms), Text(LegalDocumentType.PrivacyPolicy, privacy));

    private void InForce(LegalDocument terms, LegalDocument privacy)
    {
        _legalDocuments
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.TermsOfService, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(terms);
        _legalDocuments
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.PrivacyPolicy, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(privacy);
    }

    private static readonly DateOnly September14 = new(2026, 9, 14);
    private static readonly DateOnly September27 = new(2026, 9, 27);

    private UserConsent Withdrawn(ConsentType type) => Granted(type).Withdraw();

    private CreateOrder.Validator Validator()
    {
        var services = new Mock<IServiceRepository>();
        services.Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        services.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>())).Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        var packages = new Mock<IPackageRepository>();
        packages.Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        packages.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>())).Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(r => r.IsOfferableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var calculator = new Mock<IOrderPricingCalculator>();
        calculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DirtinessLevel>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());

        return new CreateOrder.Validator(
            packages.Object,
            services.Object,
            calculator.Object,
            Mock.Of<IOrderRepository>(),
            Mock.Of<IUserMembershipRepository>(),
            _session.Object,
            PayConfigRepositoryDouble.Holding(),
            currencies.Object,
            OrderMarketDoubles.AddressIn("cz"),
            OrderMarketDoubles.Trading(Czk),
            CataloguePriceDoubles.Services(Czk, (CreateOrderTestData.ServiceId, 500m, 100m)),
            CataloguePriceDoubles.Packages(Czk, (CreateOrderTestData.PackageId, 1000m)),
            Mock.Of<IPromoCodeService>(),
            Cleansia.Tests.Features.Orders.OrderMarketDoubles.OperatedBy("cleansia-cz"),
            Cleansia.Tests.Features.Orders.OrderMarketDoubles.TenantAt("cleansia-cz"),
            _consents.Object,
            CreateOrderTestData.Speaking(Constants.Language.English),
            Mock.Of<ICountryConfigurationRepository>(),
            _legalDocuments.Object,
            SavedCards.SavedCardDoubles.Guaranteed());
    }

    private static CreateOrder.Command Booking(bool? termsAccepted) =>
        CreateOrderTestData.ValidCommand(termsAccepted: termsAccepted);

    private static void AssertRefusedOnTheTick(FluentValidation.Results.ValidationResult result)
    {
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TermsNotAccepted);
        Assert.Equal(nameof(CreateOrder.Command.TermsAccepted), error.ErrorCode);
        Assert.Equal(nameof(CreateOrder.Command.TermsAccepted), error.PropertyName);
    }

    private static void AssertNotRefusedOnTheTick(FluentValidation.Results.ValidationResult result)
    {
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => $"{e.ErrorCode}={e.ErrorMessage}")));
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TermsNotAccepted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task A_Guest_Without_The_Tick_Is_Refused(bool? termsAccepted)
    {
        SignedInAs(null);

        AssertRefusedOnTheTick(await Validator().ValidateAsync(Booking(termsAccepted)));
        _consents.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Guest_With_The_Tick_Books()
    {
        SignedInAs(null);

        AssertNotRefusedOnTheTick(await Validator().ValidateAsync(Booking(termsAccepted: true)));
        _consents.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task A_Customer_Holding_Both_Legal_Consents_Books_Without_The_Tick(bool? termsAccepted)
    {
        SignedInAs(CustomerId);
        OnRecord(Granted(ConsentType.TermsOfService), Granted(ConsentType.PrivacyPolicy));

        AssertNotRefusedOnTheTick(await Validator().ValidateAsync(Booking(termsAccepted)));
    }

    [Fact]
    public async Task A_Customer_Whose_Privacy_Consent_Was_Withdrawn_Is_Refused_Without_The_Tick()
    {
        SignedInAs(CustomerId);
        OnRecord(Granted(ConsentType.TermsOfService), Withdrawn(ConsentType.PrivacyPolicy));

        AssertRefusedOnTheTick(await Validator().ValidateAsync(Booking(termsAccepted: null)));
    }

    [Fact]
    public async Task A_Customer_Holding_Only_The_Terms_Consent_Is_Refused_Without_The_Tick()
    {
        SignedInAs(CustomerId);
        OnRecord(Granted(ConsentType.TermsOfService), Granted(ConsentType.MarketingEmails));

        AssertRefusedOnTheTick(await Validator().ValidateAsync(Booking(termsAccepted: null)));
    }

    [Fact]
    public async Task A_Customer_With_No_Consent_On_Record_Is_Refused_Without_The_Tick()
    {
        SignedInAs(CustomerId);
        OnRecord();

        AssertRefusedOnTheTick(await Validator().ValidateAsync(Booking(termsAccepted: null)));
    }

    /// <summary>
    /// Owner ruling 2026-09-28: a customer who accepted an older terms version than the one
    /// in force for the booking's market is asked again before a new booking.
    /// </summary>
    [Fact]
    public async Task A_Customer_Whose_Accepted_Terms_Are_Older_Than_The_Version_In_Force_Is_Refused_Without_The_Tick()
    {
        SignedInAs(CustomerId);
        InForce(terms: September27, privacy: September14);
        OnRecord(Granted(ConsentType.TermsOfService, "2026-09-14"), Granted(ConsentType.PrivacyPolicy, "2026-09-14"));

        AssertRefusedOnTheTick(await Validator().ValidateAsync(Booking(termsAccepted: null)));
    }

    [Fact]
    public async Task A_Customer_Whose_Accepted_Privacy_Policy_Is_Older_Than_The_Version_In_Force_Is_Refused_Without_The_Tick()
    {
        SignedInAs(CustomerId);
        InForce(terms: September14, privacy: September27);
        OnRecord(Granted(ConsentType.TermsOfService, "2026-09-14"), Granted(ConsentType.PrivacyPolicy, "2026-09-14"));

        AssertRefusedOnTheTick(await Validator().ValidateAsync(Booking(termsAccepted: null)));
    }

    [Fact]
    public async Task A_Customer_Holding_The_Versions_In_Force_Books_Without_The_Tick()
    {
        SignedInAs(CustomerId);
        InForce(terms: September27, privacy: September14);
        OnRecord(Granted(ConsentType.TermsOfService, "2026-09-27"), Granted(ConsentType.PrivacyPolicy, "2026-09-14"));

        AssertNotRefusedOnTheTick(await Validator().ValidateAsync(Booking(termsAccepted: null)));
    }

    [Fact]
    public async Task An_Unversioned_Consent_Does_Not_Stand_For_A_Text_In_Force()
    {
        SignedInAs(CustomerId);
        InForce(terms: September14, privacy: September14);
        OnRecord(Granted(ConsentType.TermsOfService, null), Granted(ConsentType.PrivacyPolicy, "2026-09-14"));

        AssertRefusedOnTheTick(await Validator().ValidateAsync(Booking(termsAccepted: null)));
    }

    /// <summary>
    /// A market's own copy seeded under the platform-wide text's date is a different text: the customer
    /// accepted the one, and the booking's market has the other in force.
    /// </summary>
    [Fact]
    public async Task A_Customer_Who_Accepted_The_Platform_Text_Is_Asked_Again_For_A_Market_Copy_Of_The_Same_Date()
    {
        SignedInAs(CustomerId);
        InForce(
            LegalDocumentFixtures.Terms(September14, countryId: "cz"),
            Text(LegalDocumentType.PrivacyPolicy, September14));
        OnRecord(Granted(ConsentType.TermsOfService, "2026-09-14"), Granted(ConsentType.PrivacyPolicy, "2026-09-14"));

        AssertRefusedOnTheTick(await Validator().ValidateAsync(Booking(termsAccepted: null)));
    }

    [Fact]
    public async Task A_Customer_Behind_The_Version_In_Force_Books_With_The_Tick()
    {
        SignedInAs(CustomerId);
        InForce(terms: September27, privacy: September27);
        OnRecord(Granted(ConsentType.TermsOfService, "2026-09-14"), Granted(ConsentType.PrivacyPolicy, "2026-09-14"));

        AssertNotRefusedOnTheTick(await Validator().ValidateAsync(Booking(termsAccepted: true)));
    }

    [Fact]
    public async Task A_Customer_With_The_Tick_Books_Without_The_Consent_Read()
    {
        SignedInAs(CustomerId);

        AssertNotRefusedOnTheTick(await Validator().ValidateAsync(Booking(termsAccepted: true)));
        _consents.VerifyNoOtherCalls();
    }
}
