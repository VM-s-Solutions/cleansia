using Cleansia.Infra.Services.Pdf.Models;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf.Layouts;

public interface IInvoiceLayoutBuilder
{
    string CountryCode { get; }

    /// <summary>
    /// Every code this layout answers to. The invoice path selects on <c>Country.IsoCode</c>, which is
    /// ISO alpha-3, while tests and other callers use alpha-2 — a layout that declares only one form is
    /// silently never selected, so each declares both.
    /// </summary>
    IReadOnlyCollection<string> CountryCodes { get; }

    void Build(IDocumentContainer container, InvoicePdfData data, CountryInvoiceContext? context);
}
