using System.Text;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Infrastructure.Pdf;

/// <summary>
/// Owner ruling 2026-09-28, decision 23: an invoice whose issue set off the cash the cleaner held carries a
/// set-off statement in the cleaner's language — the invoice total, the cash set off and the transfer —
/// while the invoice's own summary still states the whole amount due.
/// </summary>
[Collection("QuestPdfRenderer")]
public class PayoutInvoiceCashSetOffStatementTests
{
    private static readonly QuestPdfService Pdf = new(
        new LayoutBuilderFactory([], [new DefaultInvoiceLayoutBuilder(), new CzechInvoiceLayoutBuilder()]),
        NullLogger<QuestPdfService>.Instance);

    [Fact]
    public void The_Statement_States_The_Invoice_Total_The_Cash_Set_Off_And_The_Transfer_In_The_Cleaners_Language()
    {
        var probe = new ProbeLayout();
        var data = MapFixture(setOff: 300m, languageCode: "uk");

        var lines = probe.Statement(data);

        Assert.Equal(
        [
            ("Сума рахунку", "Kč1,000.00", false),
            ("Зарахована готівка від клієнтів, яка є у вас", "-Kč300.00", false),
            ("Переказ на ваш рахунок", "Kč700.00", true),
        ],
        lines);
    }

    [Fact]
    public void The_Invoices_Own_Amount_Due_Is_Unchanged_By_The_Set_Off()
    {
        var probe = new ProbeLayout();

        var withSetOff = probe.Summary(MapFixture(setOff: 300m, languageCode: "en"));
        var withoutSetOff = probe.Summary(MapFixture(setOff: 0m, languageCode: "en"));

        Assert.Equal(withoutSetOff, withSetOff);
        Assert.Contains(("Amount due", "Kč1,000.00", true), withSetOff);
    }

    [Fact]
    public void A_Set_Off_Reaches_The_Rendered_Document_In_The_Cleaners_Language()
    {
        var czech = MapFixture(setOff: 300m, languageCode: "cs");

        Assert.Equal(Render(czech), Render(czech));
        Assert.NotEqual(Render(MapFixture(setOff: 0m, languageCode: "cs")), Render(czech));
        Assert.NotEqual(Render(czech), Render(czech with { StatementLanguageCode = "ru" }));
    }

    [Fact]
    public void An_Invoice_With_Nothing_Set_Off_Carries_No_Statement_Whatever_The_Language()
    {
        var english = MapFixture(setOff: 0m, languageCode: "en");

        Assert.Equal(Render(english), Render(english with { StatementLanguageCode = "sk" }));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("sk")]
    [InlineData("uk")]
    [InlineData("ru")]
    public void Every_Locale_Words_Every_Line_Of_The_Statement_In_Its_Own_Language(string languageCode)
    {
        var labels = CashSetOffStatementLabels.For(languageCode);
        var english = CashSetOffStatementLabels.English;

        Assert.NotSame(english, labels);
        Assert.NotEqual(english.Title, labels.Title);
        Assert.NotEqual(english.InvoiceTotal, labels.InvoiceTotal);
        Assert.NotEqual(english.CashSetOff, labels.CashSetOff);
        Assert.NotEqual(english.Transfer, labels.Transfer);
        Assert.NotEqual(english.Note, labels.Note);
    }

    [Fact]
    public void The_Statement_Is_In_The_Five_Platform_Locales_And_Falls_Back_To_English()
    {
        Assert.Equal(["cs", "en", "ru", "sk", "uk"], CashSetOffStatementLabels.SupportedLanguages.Order());
        Assert.Same(CashSetOffStatementLabels.English, CashSetOffStatementLabels.For(null));
        Assert.Same(CashSetOffStatementLabels.English, CashSetOffStatementLabels.For("de"));
    }

    private static byte[] Render(InvoicePdfData data)
    {
        var bytes = Pdf.GenerateInvoicePdf(data, context: null, countryCode: null);
        var normalized = System.Text.RegularExpressions.Regex.Replace(
            Encoding.Latin1.GetString(bytes), @"(?<=D:)\d{14}", new string('0', 14));
        return Encoding.Latin1.GetBytes(normalized);
    }

    private static InvoicePdfData MapFixture(decimal setOff, string languageCode)
    {
        var invoice = PayrollMockFactory.Invoice(subTotal: 1000m, payPeriod: PayrollMockFactory.OpenPeriod());
        invoice.SetOffCash(setOff);

        var user = User.CreateWithPassword("cleaner@cleansia.test", "12345678Test!", "Jan", "Novák");
        var employee = Employee.CreateWithUser(user);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");

        return invoice.CreatePdfData(
            employee,
            currency,
            [],
            countryContext: null,
            companyInfo: CompanyInfo.Create(
                legalName: "Cleansia s.r.o.",
                tradingName: "Cleansia",
                registrationNumber: "87654321",
                street: "Testovací 1",
                city: "Praha",
                zipCode: "11000",
                countryId: "cz"),
            payoutDetails: null,
            statementLanguageCode: languageCode);
    }

    private sealed class ProbeLayout : DefaultInvoiceLayoutBuilder
    {
        public IReadOnlyList<(string Label, string Value, bool IsBold)> Statement(InvoicePdfData data) =>
            CashSetOffStatementLines(data);

        public IReadOnlyList<(string Label, string Value, bool IsBold)> Summary(InvoicePdfData data) =>
            SummaryLines(data);
    }
}
