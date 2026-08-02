using Cleansia.Infra.Services.Pdf.Theme;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf.Components;

public static class PdfComponentExtensions
{
    public static void GradientHeader(this IContainer container, string companyName, string? tagline, Action<IContainer> metaContent,
        float padding = 25, float nameFontSize = CleansiaPdfTheme.FontSizeCompanyName)
    {
        container.Background(CleansiaPdfTheme.BrandPrimary).Padding(padding).Row(row =>
        {
            row.RelativeItem(3).Column(col =>
            {
                col.Item().Text(companyName)
                    .FontSize(nameFontSize)
                    .Bold()
                    .FontColor(CleansiaPdfTheme.White);

                if (!string.IsNullOrWhiteSpace(tagline))
                {
                    col.Item().PaddingTop(4).Text(tagline)
                        .FontSize(CleansiaPdfTheme.FontSizeTagline)
                        .FontColor(CleansiaPdfTheme.White);
                }
            });

            row.RelativeItem(2).AlignRight().AlignMiddle().Column(metaCol =>
            {
                metaCol.Item()
                    .Background(CleansiaPdfTheme.HeaderMetaBg)
                    .Padding(14)
                    .Element(metaContent);
            });
        });
    }

    public static void DocumentTitle(this IContainer container, string title)
    {
        container.PaddingVertical(CleansiaPdfTheme.SectionSpacing)
            .Text(title)
            .FontSize(18)
            .Bold()
            .FontColor(CleansiaPdfTheme.TextPrimary);
    }

    public static void SectionTitle(this IContainer container, string title)
    {
        container.PaddingTop(CleansiaPdfTheme.SectionSpacing)
            .Column(col =>
            {
                col.Item().PaddingBottom(6).Text(title.ToUpperInvariant())
                    .FontSize(CleansiaPdfTheme.FontSizeSectionTitle)
                    .Bold()
                    .FontColor(CleansiaPdfTheme.BrandPrimary);
                col.Item().LineHorizontal(2).LineColor(CleansiaPdfTheme.BrandPrimary);
            });
    }

    public static void TwoColumnInfoSection(this IContainer container, Action<IContainer> left, Action<IContainer> right)
    {
        container
            .BorderLeft(3).BorderColor(CleansiaPdfTheme.BrandPrimary)
            .Background(CleansiaPdfTheme.LightBlue)
            .Padding(16)
            .Row(row =>
            {
                row.RelativeItem().Element(left);
                row.ConstantItem(30);
                row.RelativeItem().Element(right);
            });
    }

    public static void BlockTitle(this IContainer container, string title, float fontSize = CleansiaPdfTheme.FontSizeSectionTitle)
    {
        container.Column(col =>
        {
            col.Item().Text(title.ToUpperInvariant())
                .FontSize(fontSize)
                .Bold()
                .FontColor(CleansiaPdfTheme.TextPrimary);
            col.Item().PaddingTop(5);
        });
    }

    public static void InlineField(this IContainer container, string label, string? value)
    {
        container.PaddingBottom(3).Row(row =>
        {
            row.ConstantItem(95).Text(label)
                .FontSize(CleansiaPdfTheme.FontSizeLabel)
                .FontColor(CleansiaPdfTheme.TextSecondary)
                .Bold();
            row.RelativeItem().Text(value ?? "—")
                .FontSize(CleansiaPdfTheme.FontSizeBody)
                .FontColor(CleansiaPdfTheme.TextPrimary);
        });
    }

    public static void FieldGrid(this IContainer container, IReadOnlyList<Models.InvoiceField> fields, int columns)
    {
        container.PaddingTop(10)
            .Background(CleansiaPdfTheme.LightBlue)
            .Padding(14)
            .Column(col =>
            {
                for (var start = 0; start < fields.Count; start += columns)
                {
                    var row = fields.Skip(start).Take(columns).ToList();
                    col.Item().Row(r =>
                    {
                        foreach (var field in row)
                            r.RelativeItem().Element(c => c.LabeledField(field.Label, field.Value));

                        for (var pad = row.Count; pad < columns; pad++)
                            r.RelativeItem();
                    });
                }
            });
    }

    public static void LabeledField(this IContainer container, string label, string? value)
    {
        container.Column(col =>
        {
            col.Item().Text(label)
                .FontSize(CleansiaPdfTheme.FontSizeLabel)
                .FontColor(CleansiaPdfTheme.TextSecondary)
                .Bold();
            col.Item().Text(value ?? "—")
                .FontSize(CleansiaPdfTheme.FontSizeBody)
                .FontColor(CleansiaPdfTheme.TextPrimary);
            col.Item().PaddingBottom(6);
        });
    }

    public static void MetaField(this IContainer container, string label, string value)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(label)
                    .FontSize(CleansiaPdfTheme.FontSizeSmall)
                    .FontColor(Color.FromHex("#94a3b8"));
                col.Item().Text(value)
                    .FontSize(CleansiaPdfTheme.FontSizeMetaValue)
                    .FontColor(CleansiaPdfTheme.White)
                    .Bold();
                col.Item().PaddingBottom(4);
            });
        });
    }

    public static void StyledTable(this IContainer container, string[] headers, Action<TableDescriptor> bodyBuilder)
    {
        container.PaddingTop(10).Border(1).BorderColor(CleansiaPdfTheme.BorderLight).Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(4);
                cd.RelativeColumn(1);
            });

            foreach (var header in headers)
            {
                table.Cell()
                    .Background(CleansiaPdfTheme.BrandPrimary)
                    .PaddingVertical(10)
                    .PaddingHorizontal(14)
                    .Text(header.ToUpperInvariant())
                    .FontSize(CleansiaPdfTheme.FontSizeBody)
                    .FontColor(CleansiaPdfTheme.White)
                    .Bold();
            }

            bodyBuilder(table);
        });
    }

    public static void OrderDetailsTable(this IContainer container, string[] headers, uint[] relativeWidths, Action<TableDescriptor> bodyBuilder)
    {
        container.PaddingTop(6).Border(1).BorderColor(CleansiaPdfTheme.BorderLight).Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                foreach (var w in relativeWidths)
                    cd.RelativeColumn(w);
            });

            foreach (var header in headers)
            {
                table.Cell()
                    .Background(CleansiaPdfTheme.BrandPrimary)
                    .PaddingVertical(10)
                    .PaddingHorizontal(10)
                    .Text(header.ToUpperInvariant())
                    .FontSize(CleansiaPdfTheme.FontSizeBody)
                    .FontColor(CleansiaPdfTheme.White)
                    .Bold();
            }

            bodyBuilder(table);
        });
    }

    public static void TableCell(this TableDescriptor table, string text, int rowIndex, bool alignRight = false)
    {
        var cell = table.Cell()
            .Background(rowIndex % 2 == 1 ? CleansiaPdfTheme.TableAltRowBg : CleansiaPdfTheme.White)
            .BorderBottom(1)
            .BorderColor(CleansiaPdfTheme.BorderLight)
            .PaddingVertical(9)
            .PaddingHorizontal(14);

        var t = cell.Text(text)
            .FontSize(CleansiaPdfTheme.FontSizeBody)
            .FontColor(CleansiaPdfTheme.TextPrimary);

        if (alignRight)
            t.AlignRight();
    }

    public static void SummaryBox(this IContainer container, IEnumerable<(string Label, string Value, bool IsBold)> lines)
    {
        var linesList = lines.ToList();
        container.AlignRight().Width(260)
            .Border(2).BorderColor(CleansiaPdfTheme.BrandPrimary)
            .Background(CleansiaPdfTheme.LightBlue)
            .Column(col =>
            {
                for (var i = 0; i < linesList.Count; i++)
                {
                    var (label, value, isBold) = linesList[i];
                    var isLast = i == linesList.Count - 1;

                    if (isLast && linesList.Count > 1)
                    {
                        col.Item().LineHorizontal(2).LineColor(CleansiaPdfTheme.BrandPrimary);
                    }

                    col.Item()
                        .PaddingVertical(isBold ? 10 : 8)
                        .PaddingHorizontal(16)
                        .Row(row =>
                        {
                            var labelText = row.RelativeItem().Text($"{label}:")
                                .FontSize(isBold ? 12 : CleansiaPdfTheme.FontSizeBody)
                                .FontColor(CleansiaPdfTheme.TextPrimary);
                            if (isBold) labelText.Bold();

                            var valueText = row.RelativeItem().AlignRight().Text(value)
                                .FontSize(isBold ? 12 : CleansiaPdfTheme.FontSizeBody)
                                .FontColor(CleansiaPdfTheme.TextPrimary);
                            if (isBold) valueText.Bold();
                        });
                }
            });
    }

    public static void PaymentStatusSection(this IContainer container, string status)
    {
        var color = CleansiaPdfTheme.GetStatusColor(status);

        container.PaddingTop(CleansiaPdfTheme.SectionSpacing)
            .AlignCenter()
            .Column(col =>
            {
                col.Item().AlignCenter().Text("Payment Status:")
                    .FontSize(CleansiaPdfTheme.FontSizeBody)
                    .FontColor(CleansiaPdfTheme.TextSecondary);

                col.Item().PaddingTop(6).AlignCenter()
                    .Background(color)
                    .PaddingVertical(6)
                    .PaddingHorizontal(24)
                    .Text(status.ToUpperInvariant())
                    .FontSize(10)
                    .FontColor(CleansiaPdfTheme.White)
                    .Bold()
                    .AlignCenter();
            });
    }

    public static void StandardFooter(this IContainer container, string companyName, string? contactInfo, DateTime? generatedAt = null)
    {
        container.BorderTop(1).BorderColor(CleansiaPdfTheme.BorderLight)
            .PaddingTop(CleansiaPdfTheme.InnerPadding)
            .AlignCenter()
            .Column(col =>
            {
                col.Item().AlignCenter().Text(text =>
                {
                    text.Span("Thank you for choosing ").FontSize(CleansiaPdfTheme.FontSizeBody).FontColor(CleansiaPdfTheme.TextSecondary);
                    text.Span(companyName.ToUpperInvariant()).FontSize(CleansiaPdfTheme.FontSizeBody).FontColor(CleansiaPdfTheme.TextPrimary).Bold();
                    text.Span(" for your cleaning needs!").FontSize(CleansiaPdfTheme.FontSizeBody).FontColor(CleansiaPdfTheme.TextSecondary);
                });

                if (!string.IsNullOrWhiteSpace(contactInfo))
                {
                    col.Item().PaddingTop(4).AlignCenter().Text(contactInfo)
                        .FontSize(CleansiaPdfTheme.FontSizeSmall)
                        .FontColor(CleansiaPdfTheme.TextSecondary);
                }

                if (generatedAt.HasValue)
                {
                    col.Item().PaddingTop(4).AlignCenter().Text($"Generated: {generatedAt.Value:dd.MM.yyyy HH:mm} UTC")
                        .FontSize(CleansiaPdfTheme.FontSizeSmall)
                        .FontColor(CleansiaPdfTheme.TextSecondary);
                }
            });
    }

    public static void LegalNoticeBox(this IContainer container, string text, string title = "Legal notice")
    {
        container.PaddingVertical(CleansiaPdfTheme.SmallPadding)
            .BorderLeft(3)
            .BorderColor(CleansiaPdfTheme.LegalNoticeBorder)
            .Background(CleansiaPdfTheme.LegalNoticeBg)
            .Padding(CleansiaPdfTheme.InnerPadding)
            .Column(col =>
            {
                col.Item().Text(title.ToUpperInvariant())
                    .FontSize(CleansiaPdfTheme.FontSizeBody)
                    .Bold()
                    .FontColor(CleansiaPdfTheme.LegalNoticeBorder);
                col.Item().PaddingTop(4).Text(text)
                    .FontSize(CleansiaPdfTheme.FontSizeSmall)
                    .FontColor(CleansiaPdfTheme.TextPrimary);
            });
    }
}
