using System.Text;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Infrastructure.Pdf;

/// <summary>
/// The whole mapper → renderer path for the payment reference, because a field-model assertion and a
/// rendered document are different claims: the mapper can be right and the layout can still drop the
/// field, which is exactly the shape of the defect ADR-0046 closes. Runs the REAL
/// <see cref="QuestPdfService"/> over the REAL <c>CreatePdfData</c> mapper and reads the symbol back
/// out of the produced PDF bytes.
/// </summary>
public class PayoutInvoiceRenderedSymbolTests
{
    private const string VariableSymbol = PayrollMockFactory.TestVariableSymbol;

    private static readonly QuestPdfService Pdf = new(
        new LayoutBuilderFactory([], [new DefaultInvoiceLayoutBuilder(), new CzechInvoiceLayoutBuilder()]),
        NullLogger<QuestPdfService>.Instance);

    /// <summary>
    /// "Reaches the rendered document" asserted on the RENDER, not on the field model: the same
    /// document rendered with two different symbols must produce different bytes, and rendered twice
    /// with the same symbol must produce identical ones. A layout that dropped the field — which is
    /// precisely what the old conditional did when the symbol was null — makes the two renders equal
    /// and this test red. Direct text extraction is not available: QuestPDF subsets its fonts and emits
    /// glyph ids with no ToUnicode CMap, so a string search over the content streams would assert
    /// nothing (verified by dumping a rendered invoice).
    /// </summary>
    [Fact]
    public void The_Allocated_Symbol_Reaches_The_Rendered_Document()
    {
        var data = MapFixture(VariableSymbol);

        var rendered = Render(data);
        var renderedAgain = Render(data);
        var renderedWithAnotherSymbol = Render(data with
        {
            VariableSymbol = PayrollMockFactory.NextTestVariableSymbol()
        });

        Assert.NotEmpty(rendered);
        // The equality half is what stops the other half being vacuous: PDFs carry a wall-clock
        // CreationDate, so "two renders differ" is true even of a layout that dropped the field.
        Assert.Equal(rendered, renderedAgain);
        Assert.NotEqual(rendered, renderedWithAnotherSymbol);
    }

    /// <summary>
    /// The same document with NO symbol must also differ from the one that has it — otherwise the field
    /// is being dropped rather than rendered as "—", which is the exact regression D5.1 exists to stop.
    /// </summary>
    [Fact]
    public void A_Document_Without_A_Symbol_Renders_Differently_From_One_With_It()
    {
        var data = MapFixture(VariableSymbol);

        Assert.NotEqual(Render(data), Render(data with { VariableSymbol = null }));
    }

    /// <summary>
    /// The change that would have caught the original bug: a payout invoice with no reference must
    /// print the field as "—" rather than omit it, so the absence is loud on the document rather than
    /// invisible in a conditional.
    /// </summary>
    [Fact]
    public void A_Missing_Symbol_Still_Renders_The_Field_Rather_Than_Hiding_It()
    {
        var probe = new ProbePayment();

        var fields = probe.Payment(MapFixture(variableSymbol: null));

        Assert.Contains(fields, f => f.Label == probe.Labels.VariableSymbol && f.Value is null);
    }

    // The wall-clock CreationDate/ModDate are zeroed IN PLACE, so every byte offset — and therefore the
    // xref table — stays valid and the two documents remain comparable byte for byte.
    private static byte[] Render(InvoicePdfData data)
    {
        var bytes = Pdf.GenerateInvoicePdf(data, context: null, countryCode: null);
        var normalized = System.Text.RegularExpressions.Regex.Replace(
            Encoding.Latin1.GetString(bytes), @"(?<=D:)\d{14}", new string('0', 14));
        return Encoding.Latin1.GetBytes(normalized);
    }

    private static InvoicePdfData MapFixture(string? variableSymbol)
    {
        var invoice = PayrollMockFactory.Invoice(
            payPeriod: PayrollMockFactory.OpenPeriod(),
            variableSymbol: variableSymbol ?? PayrollMockFactory.TestVariableSymbol);

        if (variableSymbol is null)
        {
            typeof(EmployeeInvoice).GetProperty(nameof(EmployeeInvoice.VariableSymbol))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(invoice, [null]);
        }

        var user = User.CreateWithPassword("cleaner@cleansia.test", "12345678Test!", "Jan", "Novák");
        var employee = Employee.CreateWithUser(user);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);

        return invoice.CreatePdfData(
            employee,
            currency,
            [],
            countryContext: null,
            companyInfo: CompanyInfoFixture(),
            payoutDetails: null);
    }

    private static CompanyInfo CompanyInfoFixture() =>
        CompanyInfo.Create(
            legalName: "Cleansia s.r.o.",
            tradingName: "Cleansia",
            registrationNumber: "87654321",
            street: "Testovací 1",
            city: "Praha",
            zipCode: "11000",
            countryId: "cz",
            vatNumber: "CZ87654321");

    private sealed class ProbePayment : DefaultInvoiceLayoutBuilder
    {
        public new InvoiceLabels Labels => base.Labels;
        public IReadOnlyList<InvoiceField> Payment(InvoicePdfData data) => PaymentFields(data);
    }
}
