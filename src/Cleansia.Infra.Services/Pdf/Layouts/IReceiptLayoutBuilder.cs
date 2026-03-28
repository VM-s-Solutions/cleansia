using Cleansia.Infra.Services.Pdf.Models;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf.Layouts;

public interface IReceiptLayoutBuilder
{
    string CountryCode { get; }
    void Build(IDocumentContainer container, ReceiptPdfData data);
}
