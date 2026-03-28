using Cleansia.Infra.Services.Pdf.Models;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf.Layouts;

public interface IInvoiceLayoutBuilder
{
    string CountryCode { get; }
    void Build(IDocumentContainer container, InvoicePdfData data, CountryInvoiceContext? context);
}
