using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Domain.Legal;
using Cleansia.Tests.Features.Orders;
using FluentValidation.TestHelper;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// Owner ruling 2026-09-28: a customer whose accepted terms or privacy policy is not the text in force
/// accepts again before a new booking — and a recurring schedule is one, since every order it creates
/// is charged under the terms it was set up with. Without the tick the schedule is refused on
/// <see cref="BusinessErrorMessage.TermsNotAccepted"/>; with it, both consent rows move to the texts in
/// force for the saved address's market and the schedule's evidence names the versions.
/// </summary>
public sealed class CreateRecurringBookingTermsTickTests
{
    private const string UserId = "user-recurring-terms";
    private const string SavedAddressId = "saved-recurring-terms";
    private const string Czechia = "country-cz";

    private static readonly DateOnly September14 = new(2026, 9, 14);
    private static readonly DateOnly September27 = new(2026, 9, 27);

    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ISavedAddressRepository> _savedAddresses = new();
    private readonly Mock<IUserConsentRepository> _consents = new();
    private readonly Mock<ILegalDocumentResolver> _resolver = new();
    private readonly Mock<IConsentService> _consentService = new();
    private readonly Mock<IRecurringBookingTemplateRepository> _templates = new();
    private readonly Mock<IUserMembershipRepository> _memberships = new();
    private readonly AuditContext _auditContext = new();

    private readonly LegalDocument _oldTerms = LegalDocumentFixtures.Terms(September14);
    private readonly LegalDocument _newTerms = LegalDocumentFixtures.Terms(September27);
    private readonly LegalDocument _privacy = LegalDocumentFixtures.Privacy(September14);

    public CreateRecurringBookingTermsTickTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _savedAddresses.Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync([SavedAddressInCzechia()]);
        _resolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.TermsOfService, Czechia, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_newTerms);
        _resolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.PrivacyPolicy, Czechia, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_privacy);
        _memberships
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembership.Create(
                userId: UserId,
                membershipPlanId: "plan-plus",
                currencyId: "currency-czk",
                stripeSubscriptionId: "sub_terms",
                currentPeriodStart: DateTime.UtcNow.AddDays(-1),
                currentPeriodEnd: DateTime.UtcNow.AddMonths(1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task A_Customer_Who_Accepted_Older_Terms_Is_Refused_Without_The_Tick(bool? termsAccepted)
    {
        OnRecord(Accepted(ConsentType.TermsOfService, _oldTerms), Accepted(ConsentType.PrivacyPolicy, _privacy));

        var result = await Validator().TestValidateAsync(Command(termsAccepted));

        result.ShouldHaveValidationErrorFor(x => x.TermsAccepted)
            .WithErrorMessage(BusinessErrorMessage.TermsNotAccepted)
            .WithErrorCode(nameof(CreateRecurringBooking.Command.TermsAccepted));
    }

    [Fact]
    public async Task A_Customer_Who_Accepted_Older_Terms_Sets_Up_The_Schedule_With_The_Tick()
    {
        OnRecord(Accepted(ConsentType.TermsOfService, _oldTerms), Accepted(ConsentType.PrivacyPolicy, _privacy));

        var result = await Validator().TestValidateAsync(Command(termsAccepted: true));

        result.ShouldNotHaveValidationErrorFor(x => x.TermsAccepted);
    }

    [Fact]
    public async Task A_Customer_Holding_The_Texts_In_Force_Needs_No_Tick()
    {
        OnRecord(Accepted(ConsentType.TermsOfService, _newTerms), Accepted(ConsentType.PrivacyPolicy, _privacy));

        var result = await Validator().TestValidateAsync(Command(termsAccepted: null));

        result.ShouldNotHaveValidationErrorFor(x => x.TermsAccepted);
    }

    [Fact]
    public async Task The_Tick_Moves_Both_Consents_To_The_Texts_In_Force_And_The_Evidence_Names_Them()
    {
        var result = await Handler().Handle(Command(termsAccepted: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _consentService.Verify(s => s.TryGrantAsync(UserId, ConsentType.TermsOfService, _newTerms, It.IsAny<CancellationToken>()), Times.Once);
        _consentService.Verify(s => s.TryGrantAsync(UserId, ConsentType.PrivacyPolicy, _privacy, It.IsAny<CancellationToken>()), Times.Once);
        var payload = JsonDocument.Parse(_auditContext.DrainSnapshot()!.AfterJson!).RootElement;
        Assert.True(payload.GetProperty("termsAccepted").GetBoolean());
        Assert.Equal("2026-09-27", payload.GetProperty("termsVersionAccepted").GetString());
        Assert.Equal("2026-09-14", payload.GetProperty("privacyVersionAccepted").GetString());
    }

    [Fact]
    public async Task Without_The_Tick_Nothing_Is_Granted_And_The_Evidence_Names_The_Versions_Held()
    {
        OnRecord(Accepted(ConsentType.TermsOfService, _newTerms), Accepted(ConsentType.PrivacyPolicy, _privacy));

        var result = await Handler().Handle(Command(termsAccepted: null), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _consentService.VerifyNoOtherCalls();
        var payload = JsonDocument.Parse(_auditContext.DrainSnapshot()!.AfterJson!).RootElement;
        Assert.Equal("2026-09-27", payload.GetProperty("termsVersionAccepted").GetString());
        Assert.Equal("2026-09-14", payload.GetProperty("privacyVersionAccepted").GetString());
    }

    private void OnRecord(params UserConsent[] consents) =>
        _consents.Setup(r => r.GetByUserIdNoTrackingAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(consents.ToList());

    private static UserConsent Accepted(ConsentType type, LegalDocument document) =>
        UserConsent.Grant(UserId, type, "203.0.113.9", "Chrome", document.Version, document.Id);

    private CreateRecurringBooking.Validator Validator() =>
        new(
            Mock.Of<IOrderRepository>(),
            _session.Object,
            _savedAddresses.Object,
            OrderMarketDoubles.Trading(CreateOrderTestData.DefaultCurrency()),
            OrderMarketDoubles.Servicing(Czechia),
            CatalogueDoubles.Services(),
            CatalogueDoubles.Packages(),
            _consents.Object,
            _resolver.Object,
            SavedCards.SavedCardDoubles.Guaranteed(), Mock.Of<IReceivableRepository>());

    private CreateRecurringBooking.Handler Handler() =>
        new(
            _templates.Object,
            _savedAddresses.Object,
            _memberships.Object,
            _session.Object,
            OrderMarketDoubles.OperatedBy("cleansia-cz"),
            Mock.Of<ICountryConfigurationRepository>(),
            _consentService.Object,
            _consents.Object,
            _resolver.Object,
            _auditContext);

    private static CreateRecurringBooking.Command Command(bool? termsAccepted) =>
        new(
            Frequency: (int)RecurrenceFrequency.Weekly,
            DayOfWeek: (int)System.DayOfWeek.Tuesday,
            TimeOfDay: "09:00",
            Rooms: 2,
            Bathrooms: 1,
            SavedAddressId: SavedAddressId,
            SelectedServiceIds: ["service-1"],
            SelectedPackageIds: [],
            PaymentType: (int)PaymentType.Card,
            StartsOn: DateTime.UtcNow.AddDays(3),
            TermsAccepted: termsAccepted);

    private static SavedAddress SavedAddressInCzechia()
    {
        var address = Address.Create("Dlouhá 12", "Praha", "11000", Czechia);
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        typeof(SavedAddress).GetProperty(nameof(SavedAddress.Address))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(saved, [address]);
        return saved;
    }
}
