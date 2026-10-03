using System.Globalization;
using System.Text;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// Decision X4 (owner ruling 2026-09-28, option b): the contract is confirmed on a durable medium, a PDF
/// built from what was stored when it was concluded. The customer's names the seller from the company
/// record of the market, the booking, the price, when the contract was concluded, the terms in force on
/// the day of the booking and the request to start within the withdrawal period; the cleaner's names the
/// operating company and the cleaner, prices the work at the seat's reward and carries the text accepted,
/// its version and its fingerprint. Both in the reader's language.
/// </summary>
[Collection("QuestPdfRenderer")]
public sealed class ContractConfirmationServiceTests
{
    private const string CountryId = "cz";
    private const string OperatorTenantId = "cleansia-cz";
    private const string ConsentVersion = "early-performance-draft-2026-09-29";
    private static readonly byte[] Rendered = [1, 2, 3];
    private static readonly TimeSpan PragueSummer = TimeSpan.FromHours(2);
    private static readonly DateTimeOffset BookedOn = new(2026, 8, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PaidOn = new(2026, 8, 3, 8, 3, 0, TimeSpan.Zero);

    private readonly Mock<IPdfService> _pdf = new();
    private readonly Mock<ICompanyInfoRepository> _companies = new();
    private Mock<ILegalDocumentRepository> _documents = new();
    private readonly Mock<ICountryConfigurationRepository> _markets = new();
    private ConfirmationPdfData? _printed;

    public ContractConfirmationServiceTests()
    {
        _pdf.Setup(p => p.GenerateConfirmationPdf(It.IsAny<ConfirmationPdfData>()))
            .Callback<ConfirmationPdfData>(data => _printed = data)
            .Returns(Rendered);
        _markets.Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(CountryId, "CZK", "cs", 0.21m, timeZoneId: "Europe/Prague"));
    }

    private ContractConfirmationService CreateService(IPdfService? pdf = null) =>
        new(pdf ?? _pdf.Object, _companies.Object, _documents.Object, _markets.Object);

    private static CompanyInfo Company() => CompanyInfo.Create(
        "Cleansia CZ s.r.o.", "Cleansia", "12345678", "Vinohradská 1", "Praha", "12000", CountryId,
        phone: "+420 222 333 444", email: "info@cleansia.cz");

    private LegalDocumentText ArrangeTermsInForceOnTheBookingDay()
    {
        var terms = LegalDocument.Create(LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, null, new DateOnly(2026, 7, 1));
        terms.AddText("en", "Terms of Service", "## Terms\n\nThe terms.");
        var czech = terms.AddText("cs", "Obchodní podmínky", "## Podmínky\n\nPodmínky.");
        _documents
            .Setup(r => r.GetInForceAsync(
                LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, CountryId,
                DateOnly.FromDateTime(BookedOn.UtcDateTime), It.IsAny<CancellationToken>()))
            .ReturnsAsync(terms);
        return czech;
    }

    private static Order CardOrder()
    {
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "jana@example.test",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Vinohradská 12", "Praha", "12000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: new DateTime(2026, 8, 10, 7, 0, 0, DateTimeKind.Utc),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: "user-1");
        order.SetCurrency(Currency.Create("CZK", "Kč", "Czech koruna"));
        order.Created("user-1", BookedOn);
        order.RecordEarlyPerformanceConsent(ConsentVersion, BookedOn.AddSeconds(5), "cleansia.customer", null, null);
        return order;
    }

    private static string Instant(DateTimeOffset utc, string language) =>
        $"{utc.ToOffset(PragueSummer).ToString("G", CultureInfo.GetCultureInfo(language))} (UTC+02:00)";

    private static string Plain(string value) => value.Replace('\u00A0', ' ').Replace('\u202F', ' ');

    private IReadOnlyList<(string Label, string Value)> Section(string title) =>
        _printed!.Sections.Single(s => s.Title == title).Fields;

    [Fact]
    public async Task The_Booking_Confirmation_Names_The_Seller_The_Price_The_Moment_The_Terms_And_The_Withdrawal_Request()
    {
        var terms = ArrangeTermsInForceOnTheBookingDay();
        _companies.Setup(r => r.GetActiveByCountryAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(Company());
        var order = CardOrder();

        var (bytes, fileName) = await CreateService().ForBookingAsync(order, PaidOn, "cs", CancellationToken.None);

        Assert.Same(Rendered, bytes);
        Assert.Equal($"booking-confirmation-{order.DisplayOrderNumber}.pdf", fileName);
        Assert.Equal("Cleansia CZ s.r.o.", _printed!.Issuer);
        Assert.Equal("Potvrzení rezervace", _printed.Title);
        Assert.Equal(order.DisplayOrderNumber, _printed.Reference);
        Assert.Equal(
            [
                ("Společnost", "Cleansia CZ s.r.o."),
                ("IČO", "12345678"),
                ("Sídlo", "Vinohradská 1, Praha 12000"),
                ("E-mail", "info@cleansia.cz"),
                ("Telefon", "+420 222 333 444"),
            ],
            Section("Prodávající"));
        Assert.Equal(
            [("Celková cena", "1 500,00 CZK"), ("Platba", "Kartou")],
            Section("Cena").Select(f => (f.Label, Plain(f.Value))).ToList());
        Assert.Equal(
            [
                ("Smlouva uzavřena", Instant(PaidOn, "cs")),
                ("Obchodní podmínky, verze", "2026-07-01"),
                ("Otisk textu (SHA-256)", terms.ContentHash),
            ],
            Section("Smlouva"));
        Assert.Equal(
            [
                ("Vaše žádost", "Výslovně jste požádali, aby úklid začal ve 14denní lhůtě pro odstoupení od smlouvy, a vzali jste na vědomí, že po úplném poskytnutí služby právo na odstoupení ztrácíte."),
                ("Žádost učiněna", Instant(BookedOn.AddSeconds(5), "cs")),
                ("Verze znění", ConsentVersion),
            ],
            Section("Odstoupení od smlouvy"));
        Assert.Contains(("Datum a čas úklidu", new DateTime(2026, 8, 10, 9, 0, 0).ToString("g", CultureInfo.GetCultureInfo("cs"))), Section("Rezervace"));
        Assert.Empty(_printed.Text);
    }

    [Fact]
    public async Task A_Cash_Booking_Says_It_Is_Paid_To_The_Cleaner_And_A_Missing_Record_Is_Said_Not_Invented()
    {
        var order = Order.Create(
            "Jana Nováková", "jana@example.test", "+420777123456",
            Address.Create("Vinohradská 12", "Praha", "12000", CountryId),
            1, 1, new DateTime(2026, 8, 10, 7, 0, 0, DateTimeKind.Utc), PaymentType.Cash, 900m, "czk", PaymentStatus.Pending, userId: "user-1");
        order.Created("user-1", BookedOn);

        await CreateService().ForBookingAsync(order, BookedOn, "en", CancellationToken.None);

        Assert.Equal("Cleansia", _printed!.Issuer);
        Assert.DoesNotContain(_printed.Sections, s => s.Title == "Seller" && s.Fields.Count > 0);
        Assert.Contains(("Payment", "In cash to the cleaner on the day"), Section("Price"));
        Assert.Contains(("Terms of service, version", "Not recorded"), Section("Contract"));
        Assert.Equal([("Your request", "Not recorded")], Section("Withdrawal from the contract"));
    }

    [Theory]
    [InlineData("en", "Booking confirmation", "Seller", "within the 14-day withdrawal period")]
    [InlineData("cs", "Potvrzení rezervace", "Prodávající", "ve 14denní lhůtě pro odstoupení")]
    [InlineData("sk", "Potvrdenie rezervácie", "Predávajúci", "v 14-dňovej lehote na odstúpenie")]
    [InlineData("uk", "Підтвердження бронювання", "Продавець", "протягом 14-денного строку")]
    [InlineData("ru", "Подтверждение бронирования", "Продавец", "в течение 14-дневного срока")]
    public async Task Every_Locale_Words_The_Booking_Confirmation_In_Its_Own_Language(
        string language, string title, string sellerSection, string statement)
    {
        _companies.Setup(r => r.GetActiveByCountryAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(Company());

        await CreateService().ForBookingAsync(CardOrder(), PaidOn, language, CancellationToken.None);

        Assert.Equal(title, _printed!.Title);
        Assert.NotEmpty(Section(sellerSection));
        Assert.Contains(_printed.Sections.SelectMany(s => s.Fields), f => f.Value.Contains(statement));
    }

    [Fact]
    public async Task The_Cleaners_Copy_Names_The_Company_And_The_Cleaner_Prices_The_Work_At_The_Seats_Reward_And_Carries_The_Text()
    {
        _companies
            .Setup(r => r.GetActiveForOperatorAsync(OperatorTenantId, CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company());
        _documents = WorkContractTestData.LegalDocumentRepository();
        var text = WorkContractTestData.Document().Texts.Single(t => t.Id == WorkContractTestData.TextIdCs);
        var facts = new WorkContractFacts(
            "ORD-7Q2K", new DateTime(2026, 8, 10, 7, 0, 0, DateTimeKind.Utc), 180, 450m, "CZK", "Praha 2 · 120 xx", CountryId, 2, 1, [], [], []);
        var acceptance = WorkContractAcceptance.Create(
            "order-1", "seat-1", "employee-1", text, WorkContractTestData.Version, "cleansia.partner", null, null, null, facts.ToJson());
        acceptance.TenantId = OperatorTenantId;
        var cleaner = Employee.CreateWithUser(User.CreateWithGoogle("petra@example.test", "Petra", "Dvořáková", "google-1", "cs"));

        var (bytes, fileName) = await CreateService().ForWorkContractAsync(acceptance, cleaner, "cs", CancellationToken.None);

        Assert.Same(Rendered, bytes);
        Assert.Equal("work-contract-ORD-7Q2K.pdf", fileName);
        Assert.Equal("Smlouva o dílo", _printed!.Title);
        Assert.Equal("ORD-7Q2K", _printed.Reference);
        Assert.Equal(
            [("Společnost", "Cleansia CZ s.r.o."), ("IČO", "12345678"), ("Sídlo", "Vinohradská 1, Praha 12000")],
            Section("Objednatel"));
        Assert.Equal([("Jméno", "Petra Dvořáková")], Section("Zhotovitel"));
        Assert.Equal(
            [("Odměna za vaše místo na zakázce", "450,00 CZK")],
            Section("Cena").Select(f => (f.Label, Plain(f.Value))).ToList());
        Assert.Equal(
            [
                ("Přijato", Instant(acceptance.AcceptedOn, "cs")),
                ("Verze smlouvy", WorkContractTestData.Version),
                ("Otisk textu (SHA-256)", text.ContentHash),
            ],
            Section("Smlouva"));
        Assert.Equal([("Cena", true), ("Cena je uvedena v CZK.", false)], _printed.Text);
    }

    /// <summary>
    /// The seeded company record gives support@cleansia.cz and the literal phone placeholder
    /// <c>&lt;company_phone_number&gt;</c> (owner decisions 2026-10-03). The seller section prints both as
    /// written, and QuestPDF draws them as text, never markup, with the glyph check on.
    /// </summary>
    [Fact]
    public async Task The_Booking_Confirmation_Prints_The_Seeded_Contact_As_Written()
    {
        _companies.Setup(r => r.GetActiveByCountryAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(CompanyInfo.Create(
            "Cleansia s.r.o.", "CLEANSIA", "12345678", "Václavské náměstí 1", "Prague", "11000", CountryId,
            phone: "<company_phone_number>", email: "support@cleansia.cz"));

        await CreateService().ForBookingAsync(CardOrder(), PaidOn, "cs", CancellationToken.None);

        Assert.Contains(("E-mail", "support@cleansia.cz"), Section("Prodávající"));
        Assert.Contains(("Telefon", "<company_phone_number>"), Section("Prodávající"));

        var checkedBefore = QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = true;
        try
        {
            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(
                new QuestPdfService(new LayoutBuilderFactory([], []), NullLogger<QuestPdfService>.Instance)
                    .GenerateConfirmationPdf(_printed!), 0, 4));
        }
        finally
        {
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = checkedBefore;
        }
    }

    [Fact]
    public async Task Both_Confirmations_Render_To_A_Pdf_In_Any_Script()
    {
        _companies.Setup(r => r.GetActiveByCountryAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(Company());
        _companies
            .Setup(r => r.GetActiveForOperatorAsync(OperatorTenantId, CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company());
        _documents = WorkContractTestData.LegalDocumentRepository();
        var pdf = new QuestPdfService(new LayoutBuilderFactory([], []), NullLogger<QuestPdfService>.Instance);
        var text = WorkContractTestData.Document().Texts.Single(t => t.Id == WorkContractTestData.TextIdEn);
        var facts = new WorkContractFacts(
            "ORD-7Q2K", new DateTime(2026, 8, 10, 7, 0, 0, DateTimeKind.Utc), 180, 450m, "CZK", "Praha 2 · 120 xx", CountryId, 2, 1, [], [], []);
        var acceptance = WorkContractAcceptance.Create(
            "order-1", "seat-1", "employee-1", text, WorkContractTestData.Version, "cleansia.partner", null, null, null, facts.ToJson());
        acceptance.TenantId = OperatorTenantId;
        var cleaner = Employee.CreateWithUser(User.CreateWithGoogle("petra@example.test", "Петра", "Дворжакова", "google-1", "uk"));

        var booking = await CreateService(pdf).ForBookingAsync(CardOrder(), PaidOn, "ru", CancellationToken.None);
        var contract = await CreateService(pdf).ForWorkContractAsync(acceptance, cleaner, "uk", CancellationToken.None);

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(booking.Bytes, 0, 4));
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(contract.Bytes, 0, 4));
    }
}
