using Cleansia.Infra.Services.Pdf.Components;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Infra.Services.Pdf.Theme;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf.Layouts;

/// <summary>
/// One layout for every market and both confirmations: the facts, then the contract text. The document
/// is a statement of what was agreed, not a fiscal one, and carries no per-country variant.
/// </summary>
public static class ConfirmationLayoutBuilder
{
    public static void Build(IDocumentContainer container, ConfirmationPdfData data)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(CleansiaPdfTheme.FontSizeBody));

            page.Content().Column(col =>
            {
                col.Item().GradientHeader(
                    data.Issuer,
                    data.Title,
                    meta => meta.MetaField(data.ReferenceLabel, data.Reference),
                    padding: 18,
                    nameFontSize: 18);

                col.Item().PaddingHorizontal(30).PaddingVertical(6).Column(body =>
                {
                    body.Item().Element(c => c.DocumentTitle(data.Title));

                    foreach (var section in data.Sections.Where(s => s.Fields.Count > 0))
                    {
                        body.Item().Element(c => c.SectionTitle(section.Title));
                        body.Item().PaddingTop(4).Column(fields =>
                        {
                            foreach (var (label, value) in section.Fields)
                            {
                                fields.Item().Element(c => Field(c, label, value));
                            }
                        });
                    }

                    if (data.Text.Count > 0)
                    {
                        body.Item().PaddingTop(CleansiaPdfTheme.SectionSpacing).LineHorizontal(1).LineColor(CleansiaPdfTheme.BorderLight);
                    }

                    foreach (var (text, isHeading) in data.Text)
                    {
                        if (isHeading)
                        {
                            body.Item().PaddingTop(10).Text(text)
                                .FontSize(CleansiaPdfTheme.FontSizeMetaValue)
                                .Bold()
                                .FontColor(CleansiaPdfTheme.TextPrimary);
                        }
                        else
                        {
                            body.Item().PaddingTop(4).Text(text)
                                .FontSize(CleansiaPdfTheme.FontSizeBody)
                                .FontColor(CleansiaPdfTheme.TextPrimary);
                        }
                    }
                });
            });

            page.Footer().PaddingHorizontal(30).PaddingBottom(12).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(CleansiaPdfTheme.FontSizeSmall).FontColor(CleansiaPdfTheme.TextSecondary));
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private static void Field(IContainer container, string label, string value)
    {
        container.PaddingBottom(3).Row(row =>
        {
            row.ConstantItem(170).Text(label)
                .FontSize(CleansiaPdfTheme.FontSizeLabel)
                .FontColor(CleansiaPdfTheme.TextSecondary)
                .Bold();
            row.RelativeItem().Text(value)
                .FontSize(CleansiaPdfTheme.FontSizeBody)
                .FontColor(CleansiaPdfTheme.TextPrimary);
        });
    }
}
