using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Tests.Features.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Infrastructure.Pdf;

/// <summary>
/// THE LINES ON A RECEIPT ADD UP TO THE TOTAL PRINTED BESIDE THEM.
///
/// <para>They did not. The table carried catalogue-priced services and packages and nothing else: no
/// discount row, no express-surcharge row, and extras listed as unpriced chips. A customer who booked
/// an express clean with an extra and a promo code held a document whose lines summed to one number
/// and whose Total said another, with nothing on the page to explain the gap.</para>
///
/// <para><b>The booking is made by the real factory, not assembled by hand.</b> The identity being
/// asserted is arithmetic about how a price is BUILT — raw lines, then a surcharge on their sum, then
/// discounts measured against the charged price — so a fixture that set the four numbers itself would
/// be asserting its own arithmetic. <c>OrderFactory</c> writes them; the receipt reads them.</para>
///
/// <para><b>And it holds in cents, not only in exact arithmetic.</b> Every money column on the order is
/// <c>numeric(18,2)</c> and rounds on its own, so terms that add up before the write can miss the stored
/// total by a cent after it. <see cref="Column"/> applies that write to every figure the test reads.</para>
/// </summary>
public class ReceiptLinesReconcileTests
{
    private const string CountryId = "cz";
    private const string LanguageCode = "en";
    private const string UserId = "user-1";
    private const string ServiceId = "service-deep-clean";
    private const string PackageId = "package-moveout";
    private const string ExtraSlug = "windows";
    private const string ExtraName = "Windows";
    private const string ReceiptNumber = "2026-000042";

    // Rooms + bathrooms = 3 units, so the service line is 1000 + 100 x 3.
    private const decimal ServiceBasePrice = 1000m;
    private const decimal ServicePerRoomPrice = 100m;
    private const decimal PackagePrice = 500m;
    private const decimal ExtraPrice = 200m;
    private const decimal ServiceLineTotal = ServiceBasePrice + ServicePerRoomPrice * 3;
    private const decimal RawSubtotal = ServiceLineTotal + PackagePrice + ExtraPrice;
    private const decimal PromoDiscount = 300m;

    private static readonly DateTime Now = new(2026, 9, 22, 9, 0, 0, DateTimeKind.Utc);

    // Lead time inside [2h, 4h) — the express band. → BookingPolicy.RequiresExpressSurcharge
    private static readonly DateTime ExpressCleaningDate = Now.AddHours(3);
    private static readonly DateTime PlainCleaningDate = Now.AddDays(3);

    private static readonly Currency Czk = Currency.Create("CZK", "Kč", "Czech Koruna");
    private static readonly Currency Eur = Currency.Create("EUR", "€", "Euro");
    private static readonly ProbeLayout Layout = new();

    private readonly Mock<IPdfService> _pdfService = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();

    private ReceiptPdfData? _rendered;

    public ReceiptLinesReconcileTests()
    {
        Czk.Id = "czk";
        Eur.Id = "eur";

        var company = CompanyInfo.Create(
            legalName: "Cleansia s.r.o.",
            tradingName: "Cleansia",
            registrationNumber: "12345678",
            street: "Hlavní 1",
            city: "Praha",
            zipCode: "11000",
            countryId: CountryId);

        _companyInfoRepository
            .Setup(r => r.GetActiveByCountryAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _companyInfoRepository
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _countryRepository
            .Setup(r => r.GetByIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Czechia", "CZE", "CZ"));
        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(CountryId, "czk", "cs", standardVatRate: 21m));

        _pdfService
            .Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
            .Callback<ReceiptPdfData, string?>((data, _) => _rendered = data)
            .Returns([1, 2, 3]);
        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(new Mock<IBlobContainerClient>().Object);
    }

    /// <summary>
    /// The case the finding names: a booking carrying all three of the things that had no line.
    /// </summary>
    [Fact]
    public async Task A_Booking_With_A_Discount_An_Express_Surcharge_And_Extras_Has_Lines_That_Sum_To_Its_Total()
    {
        var data = await RenderBooking(new Booking());

        Assert.Equal(data.Total, Layout.Items(data).Sum(line => line.Amount));
    }

    /// <summary>
    /// And the three are actually THERE, at the amounts the booking was priced with — a sum that
    /// balanced because all three were still missing would pass the assertion above.
    /// </summary>
    [Fact]
    public async Task Each_Of_The_Three_Gets_Its_Own_Line()
    {
        var data = await RenderBooking(new Booking());
        var lines = Layout.Items(data);

        Assert.Equal(ServiceLineTotal, Assert.Single(lines, l => l.Description == "Deep clean").Amount);
        Assert.Equal(ExtraPrice, Assert.Single(lines, l => l.Description == ExtraName).Amount);
        // The surcharge is 20% of the RAW subtotal; the discount is the resolved one grossed up by the
        // same 20%, because the price it comes off carries the surcharge.
        Assert.Equal(
            RawSubtotal * BookingPolicy.ExpressSurchargeRate,
            Assert.Single(lines, l => l.Description == "Express surcharge").Amount);
        Assert.Equal(
            -PromoDiscount * (1 + BookingPolicy.ExpressSurchargeRate),
            Assert.Single(lines, l => l.Description == "Promo code discount").Amount);
    }

    /// <summary>
    /// A plain booking — no express, no discount — keeps the table it always had: the catalogue lines
    /// and nothing else. The rows are conditional so this is the shape most receipts still print.
    /// </summary>
    [Fact]
    public async Task A_Plain_Booking_Carries_No_Surcharge_Or_Discount_Rows()
    {
        var data = await RenderBooking(new Booking(CleaningDate: PlainCleaningDate, Promo: 0m));
        var lines = Layout.Items(data);

        Assert.DoesNotContain(lines, l => l.Description == "Express surcharge");
        Assert.DoesNotContain(lines, l => l.Description.Contains("discount", StringComparison.Ordinal));
        Assert.Equal(data.Total, lines.Sum(line => line.Amount));
    }

    /// <summary>
    /// EXPRESS AND A PROMO, IN EURO CENTS. 58.33 + 2.00 = 60.33 raw, a 12.07 promo. The surcharge is
    /// 12.066 and the charged promo 14.484 — each is rounded by its own column, to 12.07 and 14.48,
    /// while the total 57.912 rounds to 57.91. The stored lines then summed to 57.92: a receipt a cent
    /// off its own total.
    /// </summary>
    [Fact]
    public async Task An_Express_Promo_Booking_In_Cents_Adds_Up_Once_Every_Term_Is_Stored()
    {
        var data = await RenderBooking(new Booking(
            Currency: Eur, ServiceBase: 58.33m, PerRoom: 0m, Package: null, Extra: 2.00m, Promo: 12.07m));

        Assert.Equal(Column(data.Total), Layout.Items(data).Sum(line => Column(line.Amount)));
    }

    /// <summary>
    /// A PLUS MEMBER, NO EXPRESS. 5% of 10.10 is 0.505, which the column rounds up to 0.51 while the
    /// total 9.595 rounds up to 9.60 — the lines summed to 9.59. The membership discount was the one
    /// term written with no rounding of its own at all.
    /// </summary>
    [Fact]
    public async Task A_Plus_Members_Booking_In_Cents_Adds_Up_Once_Every_Term_Is_Stored()
    {
        var data = await RenderBooking(new Booking(
            Currency: Eur, ServiceBase: 8.10m, PerRoom: 0m, Package: null, Extra: 2.00m,
            CleaningDate: PlainCleaningDate, Promo: 0m, PlusPercentage: 5m));

        var lines = Layout.Items(data);
        Assert.Contains(lines, l => l.Description == "Cleansia Plus discount");
        Assert.Equal(Column(data.Total), lines.Sum(line => Column(line.Amount)));
    }

    /// <summary>
    /// EXPRESS AND A LOYALTY DISCOUNT ALONE, IN EURO CENTS — an express booking by a tier member, the
    /// everyday case the promo and Plus tests do not reach. 60.33 raw, a 3.02 tier discount: the total
    /// 68.772 is stored as 68.77, the surcharge as 12.07 and the charged discount 3.624 rounds to 3.62,
    /// so the lines would sum to 68.78. The cent has to land on the loyalty line, the only discount.
    /// </summary>
    [Fact]
    public async Task An_Express_Loyalty_Booking_In_Cents_Adds_Up_With_The_Cent_On_The_Loyalty_Line()
    {
        var data = await RenderBooking(new Booking(
            Currency: Eur, ServiceBase: 58.33m, PerRoom: 0m, Package: null, Extra: 2.00m, Promo: 0m, Tier: 3.02m));

        var lines = Layout.Items(data);
        Assert.Equal(-3.63m, Assert.Single(lines, l => l.Description == "Loyalty discount").Amount);
        Assert.Equal(Column(data.Total), lines.Sum(line => Column(line.Amount)));
    }

    /// <summary>
    /// PLUS AND A TIER TOGETHER, OVER THE 12% CAP, ON THE EXPRESS PATH. Both sources are pro-rated to
    /// 4.82 and 2.42, charged as 5.784 and 2.904; each rounds down and the pair misses the stored
    /// discount (8.69) by a cent, which lands on the larger — the Plus line — and nowhere else.
    /// </summary>
    [Fact]
    public async Task A_Capped_Plus_And_Loyalty_Booking_In_Cents_Puts_The_Cent_On_The_Larger_Discount()
    {
        var data = await RenderBooking(new Booking(
            Currency: Eur, ServiceBase: 58.33m, PerRoom: 0m, Package: null, Extra: 2.00m, Promo: 0m,
            PlusPercentage: 10m, Tier: 3.02m));

        var lines = Layout.Items(data);
        Assert.Equal(-5.79m, Assert.Single(lines, l => l.Description == "Cleansia Plus discount").Amount);
        Assert.Equal(-2.90m, Assert.Single(lines, l => l.Description == "Loyalty discount").Amount);
        Assert.Equal(Column(data.Total), lines.Sum(line => Column(line.Amount)));
    }

    /// <summary>
    /// The cent the rounding moves lands on the discount, never on the price: the charged total is what
    /// the pricing arithmetic says it is, and only the reported saving absorbs the residue.
    /// </summary>
    [Fact]
    public async Task The_Charged_Total_Is_Untouched_By_How_The_Terms_Are_Stored()
    {
        var data = await RenderBooking(new Booking(
            Currency: Eur, ServiceBase: 58.33m, PerRoom: 0m, Package: null, Extra: 2.00m, Promo: 12.07m));

        Assert.Equal((60.33m - 12.07m) * (1 + BookingPolicy.ExpressSurchargeRate), data.Total);
        Assert.Equal(12.07m, data.ExpressSurcharge);
        Assert.Equal(14.49m, data.PromoDiscount);
    }

    /// <summary>
    /// The three discount sources are reported separately, because they were resolved separately and
    /// each is a different promise. Asserted at the layout, where all three can be non-zero at once —
    /// the booking path never produces the promo alongside the pair.
    /// </summary>
    [Fact]
    public void Every_Discount_Source_Gets_Its_Own_Line_And_They_Still_Sum()
    {
        var data = new ReceiptPdfData
        {
            ReceiptNumber = ReceiptNumber,
            OrderNumber = "ORD-1",
            IssuedDate = "22.09.2026",
            CustomerName = "Jan Novák",
            Services = [new ReceiptLineItem("Deep clean", 1000m)],
            Packages = [],
            Extras = [],
            TierDiscount = 100m,
            MembershipDiscount = 50m,
            PromoDiscount = 25m,
            Total = 825m,
            Currency = "Kč",
            PaymentStatus = PaymentStatus.Paid,
            PaymentType = PaymentType.Card,
        };

        var lines = Layout.Items(data);

        Assert.Equal(-100m, Assert.Single(lines, l => l.Description == "Loyalty discount").Amount);
        Assert.Equal(-50m, Assert.Single(lines, l => l.Description == "Cleansia Plus discount").Amount);
        Assert.Equal(-25m, Assert.Single(lines, l => l.Description == "Promo code discount").Amount);
        Assert.Equal(data.Total, lines.Sum(line => line.Amount));
    }

    /// <summary>What a <c>numeric(18,2)</c> column keeps of a value written to it.</summary>
    private static decimal Column(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private sealed record Booking(
        Currency? Currency = null,
        decimal ServiceBase = ServiceBasePrice,
        decimal PerRoom = ServicePerRoomPrice,
        decimal? Package = PackagePrice,
        decimal Extra = ExtraPrice,
        DateTime? CleaningDate = null,
        decimal Promo = PromoDiscount,
        decimal PlusPercentage = 0m,
        decimal Tier = 0m);

    private async Task<ReceiptPdfData> RenderBooking(Booking booking)
    {
        var order = await BookAsync(booking);
        order.Id = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";

        await CreateReceiptService().RealizeFiscalAndPdfAsync(
            order,
            OrderReceipt.Create(order.Id, ReceiptNumber, "receipt.pdf", "2026/ORD/receipt.pdf", LanguageCode),
            CancellationToken.None);

        Assert.NotNull(_rendered);
        return _rendered!;
    }

    private async Task<Order> BookAsync(Booking booking)
    {
        var currency = booking.Currency ?? Czk;
        var service = Service.Create("category-1", "Deep clean", "Everything", estimatedTime: 60);
        service.Id = ServiceId;
        var package = Package.Create("Move-out", "The lot");
        package.Id = PackageId;
        var extra = Extra.Create(ExtraSlug, ExtraName, null);
        string[] packageIds = booking.Package is null ? [] : [PackageId];

        var serviceRepository = new Mock<IServiceRepository>();
        serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { service }.AsQueryable().BuildMock());
        var packageRepository = new Mock<IPackageRepository>();
        packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns((booking.Package is null ? [] : new[] { package }).AsQueryable().BuildMock());

        var loyaltyService = new Mock<ILoyaltyService>();
        loyaltyService
            .Setup(s => s.ResolveTierDiscountForOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierDiscountResult(
                booking.Tier, booking.Tier > 0m ? LoyaltyTier.GoldPolisher : LoyaltyTier.BronzeCleaner));
        var membershipRepository = new Mock<IUserMembershipRepository>();
        membershipRepository
            .Setup(r => r.GetEntitledForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking.PlusPercentage > 0m ? Membership(booking.PlusPercentage, currency) : null);

        var rawSubtotal = booking.ServiceBase + booking.PerRoom * 3 + (booking.Package ?? 0m) + booking.Extra;

        var factory = new OrderFactory(
            _orderRepository.Object,
            serviceRepository.Object,
            packageRepository.Object,
            ExtraRepositoryDouble.Holding(extra),
            CataloguePriceDoubles.Services(currency, (ServiceId, booking.ServiceBase, booking.PerRoom)),
            CataloguePriceDoubles.Packages(currency, (PackageId, booking.Package ?? 0m)),
            CataloguePriceDoubles.Extras(currency, (extra.Id, booking.Extra)),
            PayConfigRepositoryDouble.Covering(currency.Id, [ServiceId], packageIds),
            // Null company info at BOOKING time keeps the VAT breakdown out of this suite's way; the
            // receipt's own issuer is resolved separately, in ReceiptService.
            Mock.Of<ICompanyInfoRepository>(),
            _countryConfigurationRepository.Object,
            Mock.Of<IVatCalculator>(),
            loyaltyService.Object,
            membershipRepository.Object,
            NoPreferredCleanerHold.Resolver,
            WorkContractResolvers.Resolver().Object,
            Mock.Of<INotificationProducer>(),
            Mock.Of<IAdminNotifier>(),
            NullLogger<OrderFactory>.Instance);

        return await factory.CreateAsync(
            new CreateOrderInput(
                UserId: booking.PlusPercentage > 0m || booking.Tier > 0m ? UserId : null,
                CustomerName: "Jan Novák",
                CustomerEmail: "jan@example.com",
                CustomerPhone: "+420123456789",
                Address: Address.Create("Hlavní 2", "Praha", "11000", CountryId),
                Rooms: 2,
                Bathrooms: 1,
                SelectedExtraSlugs: [ExtraSlug],
                CleaningDate: booking.CleaningDate ?? ExpressCleaningDate,
                PaymentType: PaymentType.Cash,
                Currency: currency,
                SelectedServiceIds: [ServiceId],
                SelectedPackageIds: packageIds,
                RawSubtotal: rawSubtotal,
                NowUtc: Now,
                ReservedExpressWaiver: null,
                OperatorTenantId: null,
                PromoDiscountAmount: booking.Promo,
                PromoCodeId: booking.Promo > 0m ? "promo-1" : null),
            CancellationToken.None);
    }

    private static UserMembership Membership(decimal discountPercentage, Currency currency)
    {
        var plan = MembershipPlan.Create(
            code: "PLUS",
            name: "Cleansia Plus",
            discountPercentage: discountPercentage,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true);
        var membership = UserMembership.Create(
            userId: UserId,
            membershipPlanId: plan.Id,
            currencyId: currency.Id,
            stripeSubscriptionId: "sub_1",
            currentPeriodStart: Now.AddDays(-1),
            currentPeriodEnd: Now.AddMonths(1));
        typeof(UserMembership).GetProperty(nameof(UserMembership.MembershipPlan))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(membership, [plan]);
        return membership;
    }

    private ReceiptService CreateReceiptService() => new(
        _pdfService.Object,
        Mock.Of<IOrderReceiptRepository>(),
        Mock.Of<IFiscalCounterRepository>(),
        _languageRepository.Object,
        _companyInfoRepository.Object,
        _countryRepository.Object,
        _countryConfigurationRepository.Object,
        _blobClientFactory.Object,
        Mock.Of<IFiscalServiceResolver>(),
        NullLogger<ReceiptService>.Instance);

    private sealed class ProbeLayout : DefaultReceiptLayoutBuilder
    {
        public IReadOnlyList<(string Description, decimal Amount)> Items(ReceiptPdfData data) =>
            ItemLines(data);
    }
}
