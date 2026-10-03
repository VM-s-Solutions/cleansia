using Cleansia.Core.AppServices.Features.Orders;
using System.Net;
using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// Owner ruling 2026-09-28: one company name everywhere. Every e-mail's copyright line names the company
/// the receipts name — the active company of the order's market for an e-mail about an order, the ambient
/// company's for any other — instead of a hard-coded "© Cleansia s.r.o.", and a footer entered as a
/// translation row no longer overrides it. The brand stands in only when no company record resolves.
/// </summary>
public sealed class EmailFooterCompanyIdentityTests
{
    private const string Recipient = "customer@example.com";
    private const string MarketCountry = "cz";
    private const string MarketCompany = "Cleansia CZ s.r.o.";
    private const string AmbientCompany = "Cleansia Holding a.s.";

    private readonly Mock<ICompanyInfoRepository> _companies = new();

    public EmailFooterCompanyIdentityTests()
    {
        _companies
            .Setup(r => r.GetActiveByCountryAsync(MarketCountry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company(MarketCompany));
        _companies
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company(AmbientCompany));
    }

    public static TheoryData<string> OrderEmails => new() { "receipt", "status", "booked", "pay-link" };

    public static TheoryData<string> OtherEmails => new()
    {
        "reset", "confirmation", "test-receipt", "period-closed", "period-reminder", "promo",
        "cash-remittance", "wind-down-customer", "wind-down-cleaner", "admin-notification",
    };

    [Theory]
    [MemberData(nameof(OrderEmails))]
    public async Task An_Email_About_An_Order_Names_The_Company_Of_Its_Market(string email)
    {
        var (service, values) = BuildService();

        await Send(service, email, "en");

        AssertFooter($"{MarketCompany} All rights reserved.", values["FooterText"]);
    }

    [Theory]
    [MemberData(nameof(OtherEmails))]
    public async Task Any_Other_Email_Names_The_Ambient_Company(string email)
    {
        var (service, values) = BuildService();

        await Send(service, email, "en");

        AssertFooter($"{AmbientCompany} All rights reserved.", values["FooterText"]);
    }

    [Fact]
    public async Task The_Period_End_Reminder_Job_Names_The_Company_That_Owns_The_Period()
    {
        const string periodTenant = "tenant-cz";
        string? overriddenTenant = null;
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider
            .Setup(p => p.SetTenantOverride(It.IsAny<string>()))
            .Callback((string tenantId) => overriddenTenant = tenantId);
        tenantProvider
            .Setup(p => p.ClearTenantOverride())
            .Callback(() => overriddenTenant = null);
        _companies
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => overriddenTenant == periodTenant ? Company(AmbientCompany) : null);

        var period = PayPeriod.CreateBiWeekly(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)).AddDays(-13));
        period.TenantId = periodTenant;
        var periods = new Mock<IPayPeriodRepository>();
        periods
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { period }.AsQueryable().BuildMock());

        var cleaner = Employee.CreateWithUser(
            User.CreateWithPassword("cleaner@example.com", "Password1", "Petr", "Svoboda", UserProfile.Employee, "en"));
        cleaner.TenantId = periodTenant;
        var employees = new Mock<IEmployeeRepository>();
        employees
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { cleaner }.AsQueryable().BuildMock());

        var (emailService, values) = BuildService();
        var job = new PeriodReminderBackgroundService(
            periods.Object,
            employees.Object,
            emailService,
            tenantProvider.Object,
            NullLogger<PeriodReminderBackgroundService>.Instance);

        await job.SendPeriodEndRemindersAsync(CancellationToken.None);

        AssertFooter($"{AmbientCompany} All rights reserved.", values["FooterText"]);
        Assert.Null(overriddenTenant);
    }

    [Theory]
    [InlineData("en", "All rights reserved.")]
    [InlineData("cs", "Všechna práva vyhrazena.")]
    [InlineData("sk", "Všetky práva vyhradené.")]
    [InlineData("uk", "Усі права захищено.")]
    [InlineData("ru", "Все права защищены.")]
    public async Task The_Rights_Line_Is_In_The_Emails_Language(string language, string rights)
    {
        var (service, values) = BuildService();

        await Send(service, "booked", language);

        AssertFooter($"{MarketCompany} {rights}", values["FooterText"]);
    }

    [Fact]
    public async Task Without_A_Company_Record_The_Footer_Names_The_Brand()
    {
        _companies.Reset();
        var (service, values) = BuildService();

        await Send(service, "booked", "en");

        AssertFooter("Cleansia. All rights reserved.", values["FooterText"]);
    }

    private static void AssertFooter(string expectedAfterYear, string? footer) =>
        Assert.Matches($@"^© \d{{4}} {Regex.Escape(expectedAfterYear)}$", footer ?? string.Empty);

    private static CompanyInfo Company(string legalName) =>
        CompanyInfo.Create(legalName, "Cleansia", "12345678", "Václavské náměstí 1", "Praha", "110 00", "country-cze");

    private static Task Send(EmailService service, string email, string language)
    {
        var ct = CancellationToken.None;
        var order = BuildOrder();
        return email switch
        {
            "receipt" => service.SendOrderReceiptEmailAsync(Recipient, order, languageCode: language, ct: ct),
            "status" => service.SendOrderStatusUpdateEmailAsync(Recipient, order, "Cancelled", language, ct),
            "booked" => service.SendOrderBookedEmailAsync(Recipient, order, 24, language, ct),
            "pay-link" => service.SendReceivablePayLinkEmailAsync(
                Recipient, order, Receivable.ForLockout(order, 500m), "https://pay.test/r", language, ct),
            "reset" => service.SendResetPasswordEmailAsync(Recipient, "Jana Novakova", "123456", language, ct),
            "confirmation" => service.SendEmailConfirmationAsync(Recipient, "Jana Novakova", "123456", language, ct),
            "test-receipt" => service.SendTestOrderReceiptEmailAsync(
                Recipient, "Jana Novakova", "ORD-1", "1. 10. 2026", "1 234,50 Kč", language, ct),
            "period-closed" => service.SendPeriodClosedEmailAsync(
                Recipient, "Petr Svoboda", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), DateTime.UtcNow, "September", language, ct: ct),
            "period-reminder" => service.SendPeriodEndReminderEmailAsync(
                Recipient, "Petr Svoboda", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), 3, "September", language, ct),
            "promo" => service.SendPromoCodeEmailAsync(Recipient, "WELCOME10", "-10 %", null, language, ct),
            "cash-remittance" => service.SendCashRemittanceRequestEmailAsync(
                Recipient, "Petr Svoboda", 1500m, "Kč", DateTime.UtcNow.AddDays(-40), language, ct),
            "wind-down-customer" => service.SendCompanyWindDownCustomerNoticeAsync(
                Recipient, "Jana Novakova", [AmbientCompany], new DateOnly(2026, 12, 1), language, ct),
            "wind-down-cleaner" => service.SendCompanyWindDownCleanerNoticeAsync(
                Recipient, "Petr Svoboda", [AmbientCompany], new DateOnly(2026, 12, 1), language, ct),
            "admin-notification" => service.SendAdminNotificationEmailAsync(
                Recipient,
                AdminNotificationEventCatalog.OrderNew,
                new Dictionary<string, string>
                {
                    ["orderNumber"] = "ORD-1",
                    ["amount"] = "1 234,50 Kč",
                    ["paymentType"] = nameof(PaymentType.Cash),
                    ["countryId"] = MarketCountry,
                    ["orderId"] = "order-1",
                },
                language,
                ct),
            _ => throw new ArgumentOutOfRangeException(nameof(email), email, "No sender for this e-mail."),
        };
    }

    private static Order BuildOrder()
    {
        var order = Order.Create(
            customerName: "Jana Novakova",
            customerEmail: Recipient,
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", MarketCountry),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Cash,
            totalPrice: 1234.5m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-1",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = "czk";
        order.SetCurrency(czk);
        return order;
    }

    /// <summary>
    /// The real service with a renderer double that keeps the values it was handed, and a translation row
    /// for every e-mail that still carries the old hard-coded footer.
    /// </summary>
    private (EmailService Service, Dictionary<string, string?> Values) BuildService()
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");

        var translationRepository = new Mock<IEmailTemplateTranslationRepository>();
        translationRepository
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(
                It.IsAny<EmailType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Dictionary<string, string> { ["FooterText"] = "© 2026 Cleansia s.r.o. All rights reserved." });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new AcceptingHandler(), disposeHandler: false));

        var captured = new Dictionary<string, string?>(StringComparer.Ordinal);
        var renderer = new Mock<IEmailTemplateRenderer>();
        renderer
            .Setup(r => r.Render(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string?>>()))
            .Callback((string _, IReadOnlyDictionary<string, string?> values) =>
            {
                captured.Clear();
                foreach (var (key, value) in values)
                {
                    captured[key] = value;
                }
            })
            .Returns("<html></html>");

        var service = new EmailService(
            config.Object,
            NullLogger<EmailService>.Instance,
            httpClientFactory.Object,
            translationRepository.Object,
            renderer.Object,
            Mock.Of<ICountryConfigurationRepository>(),
            _companies.Object);

        return (service, captured);
    }

    private sealed class AcceptingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
    }
}
