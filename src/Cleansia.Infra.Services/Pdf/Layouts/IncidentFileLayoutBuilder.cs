using Cleansia.Infra.Services.Pdf.Components;
using Cleansia.Infra.Services.Pdf.IncidentFile;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Infra.Services.Pdf.Theme;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf.Layouts;

/// <summary>
/// Renders the section model of <see cref="IncidentFileSections"/> — never the data directly, so the
/// pages and the printed SHA-256 cannot disagree. One layout for every market: the file is internal
/// evidence, not a fiscal document, and carries no per-country variant.
/// </summary>
public static class IncidentFileLayoutBuilder
{
    public const string DocumentTitle = "Customer incident file";

    public static void Build(IDocumentContainer container, IncidentFilePdfData data, IReadOnlyList<IncidentFileSection> sections, string dataSha256)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(CleansiaPdfTheme.FontSizeBody));

            // The letterhead is part of the flow, as on the invoice: a running masthead on every
            // continuation page would push a long trail onto many more of them.
            page.Content().Column(col =>
            {
                col.Item().Element(c => BuildHeader(c, data));
                col.Item().PaddingHorizontal(30).PaddingVertical(6).Column(body =>
                {
                    body.Item().PaddingVertical(8).Text(DocumentTitle)
                        .FontSize(16).Bold().FontColor(CleansiaPdfTheme.TextPrimary);

                    foreach (var section in sections)
                    {
                        body.Item().Element(c => c.SectionTitle(section.Title));
                        foreach (var block in section.Blocks)
                        {
                            body.Item().Element(c => BuildBlock(c, block));
                        }
                    }

                    body.Item().PaddingTop(CleansiaPdfTheme.SectionSpacing).Element(c => BuildIntegrity(c, dataSha256));
                });
            });

            page.Footer().PaddingHorizontal(30).PaddingBottom(12).Element(f => BuildFooter(f, data));
        });
    }

    private static void BuildHeader(IContainer container, IncidentFilePdfData data)
    {
        container.GradientHeader(
            "CLEANSIA",
            "Evidence record for an incident — internal, not for the data subject",
            meta =>
            {
                meta.Column(col =>
                {
                    col.Item().Element(c => c.MetaField("Subject", data.Subject.UserId));
                    col.Item().Element(c => c.MetaField("Scope", data.OrderIdFilter is null ? "All orders" : $"Order {data.OrderIdFilter}"));
                    col.Item().Element(c => c.MetaField("Generated", data.GeneratedAt.ToUniversalTime().ToString("yyyy-MM-dd")));
                });
            },
            padding: 18,
            nameFontSize: 20);
    }

    private static void BuildBlock(IContainer container, IncidentFileBlock block)
    {
        switch (block)
        {
            case IncidentFileSubheading subheading:
                container.PaddingTop(10).Element(c => c.BlockTitle(subheading.Text, CleansiaPdfTheme.FontSizeMetaValue));
                break;
            case IncidentFileParagraph paragraph:
                container.PaddingTop(4).Text(paragraph.Text)
                    .FontSize(CleansiaPdfTheme.FontSizeBody)
                    .FontColor(CleansiaPdfTheme.TextSecondary)
                    .Italic();
                break;
            case IncidentFileFieldList fields:
                container.PaddingTop(4).Column(col =>
                {
                    foreach (var field in fields.Fields)
                    {
                        col.Item().Element(c => c.InlineField(field.Label, field.Value));
                    }
                });
                break;
            case IncidentFileTable table:
                BuildTable(container, table);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(block), block.GetType().Name, "an incident file block the layout does not know");
        }
    }

    private static void BuildTable(IContainer container, IncidentFileTable table)
    {
        container.PaddingTop(4).Column(col =>
        {
            if (table.Caption is not null)
            {
                col.Item().Text(table.Caption)
                    .FontSize(CleansiaPdfTheme.FontSizeLabel)
                    .Bold()
                    .FontColor(CleansiaPdfTheme.TextSecondary);
            }

            if (table.Rows.Count == 0)
            {
                col.Item().Text("None.")
                    .FontSize(CleansiaPdfTheme.FontSizeLabel)
                    .FontColor(CleansiaPdfTheme.TextSecondary)
                    .Italic();
                return;
            }

            col.Item().Border(1).BorderColor(CleansiaPdfTheme.BorderLight).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    // The last column is the one that carries free text (a value, a message, a user agent),
                    // so it takes the width the others do not need.
                    for (var i = 0; i < table.Headers.Count; i++)
                    {
                        cd.RelativeColumn(i == table.Headers.Count - 1 ? 3 : 1);
                    }
                });

                t.Header(header =>
                {
                    foreach (var h in table.Headers)
                    {
                        header.Cell()
                            .Background(CleansiaPdfTheme.TableHeaderBg)
                            .BorderBottom(1).BorderColor(CleansiaPdfTheme.BorderLight)
                            .PaddingVertical(4).PaddingHorizontal(6)
                            .Text(h)
                            .FontSize(CleansiaPdfTheme.FontSizeLabel)
                            .Bold()
                            .FontColor(CleansiaPdfTheme.TextSecondary);
                    }
                });

                for (var r = 0; r < table.Rows.Count; r++)
                {
                    foreach (var cell in table.Rows[r])
                    {
                        t.Cell()
                            .Background(r % 2 == 1 ? CleansiaPdfTheme.TableAltRowBg : CleansiaPdfTheme.White)
                            .BorderBottom(1).BorderColor(CleansiaPdfTheme.BorderLight)
                            .PaddingVertical(4).PaddingHorizontal(6)
                            .Text(cell)
                            .FontSize(CleansiaPdfTheme.FontSizeLabel)
                            .FontColor(CleansiaPdfTheme.TextPrimary);
                    }
                }
            });
        });
    }

    private static void BuildIntegrity(IContainer container, string dataSha256)
    {
        container.Column(col =>
        {
            col.Item().Element(c => c.SectionTitle("Integrity"));
            col.Item().PaddingTop(4).Element(c => c.InlineField("SHA-256", dataSha256));
            col.Item().Text("Computed over the canonical text of sections 1–5. The header, the footer and this block are outside it. Unsigned.")
                .FontSize(CleansiaPdfTheme.FontSizeSmall)
                .FontColor(CleansiaPdfTheme.TextSecondary);
        });
    }

    private static void BuildFooter(IContainer container, IncidentFilePdfData data)
    {
        container.BorderTop(1).BorderColor(CleansiaPdfTheme.BorderLight)
            .PaddingTop(CleansiaPdfTheme.SmallPadding)
            .Row(row =>
            {
                row.RelativeItem().Text($"Generated {IncidentFileSections.Stamp(data.GeneratedAt)} by {data.GeneratedBy}")
                    .FontSize(CleansiaPdfTheme.FontSizeSmall)
                    .FontColor(CleansiaPdfTheme.TextSecondary);

                row.ConstantItem(80).AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(CleansiaPdfTheme.FontSizeSmall).FontColor(CleansiaPdfTheme.TextSecondary));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
    }
}
