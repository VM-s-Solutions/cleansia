using Cleansia.Infra.Services.Pdf.Components;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Infra.Services.Pdf.Theme;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf.Layouts;

public class DefaultReceiptLayoutBuilder : IReceiptLayoutBuilder
{
    public virtual string CountryCode => "default";

    public virtual void Build(IDocumentContainer container, ReceiptPdfData data)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(CleansiaPdfTheme.FontSizeBody));

            page.Header().Element(h => BuildHeader(h, data));
            page.Content().PaddingHorizontal(30).PaddingVertical(10).Element(c => BuildContent(c, data));
            page.Footer().PaddingHorizontal(30).PaddingBottom(20).Element(f => BuildFooter(f, data));
        });
    }

    protected virtual void BuildHeader(IContainer container, ReceiptPdfData data)
    {
        container.GradientHeader(
            data.Company?.TradingName ?? "CLEANSIA",
            data.Company?.Tagline,
            meta =>
            {
                meta.Column(col =>
                {
                    col.Item().Element(c => c.MetaField("Receipt #", data.ReceiptNumber));
                    col.Item().Element(c => c.MetaField("Order #", data.OrderNumber));
                    col.Item().Element(c => c.MetaField("Date", data.IssuedDate));
                });
            });
    }

    protected virtual void BuildContent(IContainer container, ReceiptPdfData data)
    {
        container.Column(col =>
        {
            col.Item().Element(c => c.DocumentTitle("Receipt"));

            col.Item().Element(c => BuildInfoSection(c, data));

            if (data.CleaningDate != null || data.Rooms != null || data.Bathrooms != null)
            {
                col.Item().Element(c => c.SectionTitle("Order Details"));
                col.Item().Element(c => BuildOrderDetails(c, data));
            }

            col.Item().Element(c => c.SectionTitle("Services & Packages"));
            col.Item().Element(c => BuildItemsTable(c, data));

            if (data.Extras.Count > 0)
            {
                col.Item().Element(c => BuildExtrasSection(c, data));
            }

            col.Item().PaddingTop(CleansiaPdfTheme.SectionSpacing).Element(c => BuildSummary(c, data));

            col.Item().Element(c => BuildPaymentInfo(c, data));

            // Fiscal registration block is only rendered when the receipt was registered with a fiscal authority.
            if (!string.IsNullOrWhiteSpace(data.FiscalCode))
            {
                col.Item().Element(c => BuildFiscalInfo(c, data));
            }
        });
    }

    protected virtual void BuildFiscalInfo(IContainer container, ReceiptPdfData data)
    {
        container.PaddingTop(CleansiaPdfTheme.SectionSpacing)
            .Column(col =>
            {
                col.Item().Element(c => c.SectionTitle("Fiscal Registration"));

                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Fiscal Code")
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextSecondary);
                        c.Item().Text(data.FiscalCode ?? string.Empty)
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextPrimary)
                            .Bold();
                    });

                    if (!string.IsNullOrWhiteSpace(data.FiscalProviderKey))
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Provider")
                                .FontSize(CleansiaPdfTheme.FontSizeBody)
                                .FontColor(CleansiaPdfTheme.TextSecondary);
                            c.Item().Text(data.FiscalProviderKey)
                                .FontSize(CleansiaPdfTheme.FontSizeBody)
                                .FontColor(CleansiaPdfTheme.TextPrimary);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(data.FiscalRegisteredAt))
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Registered")
                                .FontSize(CleansiaPdfTheme.FontSizeBody)
                                .FontColor(CleansiaPdfTheme.TextSecondary);
                            c.Item().Text(data.FiscalRegisteredAt)
                                .FontSize(CleansiaPdfTheme.FontSizeBody)
                                .FontColor(CleansiaPdfTheme.TextPrimary);
                        });
                    }
                });
            });
    }

    protected virtual void BuildInfoSection(IContainer container, ReceiptPdfData data)
    {
        container.TwoColumnInfoSection(
            left =>
            {
                left.Column(col =>
                {
                    col.Item().Text("CUSTOMER INFORMATION")
                        .FontSize(CleansiaPdfTheme.FontSizeSectionTitle)
                        .Bold()
                        .FontColor(CleansiaPdfTheme.TextPrimary);
                    col.Item().PaddingTop(6);
                    col.Item().Element(c => c.LabeledField("Name", data.CustomerName));
                    col.Item().Element(c => c.LabeledField("Email", data.CustomerEmail));
                    col.Item().Element(c => c.LabeledField("Phone", data.CustomerPhone));
                    col.Item().Element(c => c.LabeledField("Address", data.CustomerAddress));
                });
            },
            right =>
            {
                right.Column(col =>
                {
                    col.Item().Text("COMPANY INFORMATION")
                        .FontSize(CleansiaPdfTheme.FontSizeSectionTitle)
                        .Bold()
                        .FontColor(CleansiaPdfTheme.TextPrimary);
                    col.Item().PaddingTop(6);

                    if (data.Company != null)
                    {
                        col.Item().Element(c => c.LabeledField("Company", data.Company.LegalName));

                        if (!string.IsNullOrWhiteSpace(data.Company.RegistrationNumber))
                            col.Item().Element(c => c.LabeledField("Reg. #", data.Company.RegistrationNumber));

                        if (!string.IsNullOrWhiteSpace(data.Company.VatNumber))
                            col.Item().Element(c => c.LabeledField("VAT #", data.Company.VatNumber));

                        col.Item().Element(c => c.LabeledField("Address", data.Company.Address));
                        col.Item().Element(c => c.LabeledField("Contact", data.Company.ContactInfo));

                        if (!string.IsNullOrWhiteSpace(data.Company.Iban))
                            col.Item().Element(c => c.LabeledField("IBAN", data.Company.Iban));
                    }
                });
            });
    }

    protected virtual void BuildOrderDetails(IContainer container, ReceiptPdfData data)
    {
        container.PaddingTop(10)
            .Background(CleansiaPdfTheme.LightBlue)
            .Padding(14)
            .Row(row =>
            {
                if (data.CleaningDate != null)
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Cleaning Date")
                            .FontSize(CleansiaPdfTheme.FontSizeLabel)
                            .FontColor(CleansiaPdfTheme.TextSecondary)
                            .Bold();
                        col.Item().Text(data.CleaningDate)
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextPrimary);
                    });
                }

                if (data.Rooms.HasValue)
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Rooms")
                            .FontSize(CleansiaPdfTheme.FontSizeLabel)
                            .FontColor(CleansiaPdfTheme.TextSecondary)
                            .Bold();
                        col.Item().Text(data.Rooms.Value.ToString())
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextPrimary);
                    });
                }

                if (data.Bathrooms.HasValue)
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Bathrooms")
                            .FontSize(CleansiaPdfTheme.FontSizeLabel)
                            .FontColor(CleansiaPdfTheme.TextSecondary)
                            .Bold();
                        col.Item().Text(data.Bathrooms.Value.ToString())
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextPrimary);
                    });
                }

                if (data.EstimatedTime.HasValue)
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Est. Duration")
                            .FontSize(CleansiaPdfTheme.FontSizeLabel)
                            .FontColor(CleansiaPdfTheme.TextSecondary)
                            .Bold();
                        var hours = data.EstimatedTime.Value / 60;
                        var mins = data.EstimatedTime.Value % 60;
                        var duration = hours > 0
                            ? mins > 0 ? $"{hours}h {mins}m" : $"{hours}h"
                            : $"{mins}m";
                        col.Item().Text(duration)
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextPrimary);
                    });
                }
            });
    }

    protected virtual void BuildItemsTable(IContainer container, ReceiptPdfData data)
    {
        container.StyledTable(
            ["Description", "Amount"],
            table =>
            {
                var rowIndex = 0;
                foreach (var service in data.Services)
                {
                    table.TableCell(service.Name, rowIndex);
                    table.TableCell($"{data.Currency}{service.Price:N2}", rowIndex, alignRight: true);
                    rowIndex++;
                }

                foreach (var package in data.Packages)
                {
                    table.TableCell($"{package.Name} (Package)", rowIndex);
                    table.TableCell($"{data.Currency}{package.Price:N2}", rowIndex, alignRight: true);
                    rowIndex++;
                }

                if (rowIndex == 0)
                {
                    table.TableCell("No items", 0);
                    table.TableCell("—", 0, alignRight: true);
                }
            });
    }

    protected virtual void BuildExtrasSection(IContainer container, ReceiptPdfData data)
    {
        container.PaddingTop(10).Column(col =>
        {
            col.Item().Text("EXTRAS INCLUDED")
                .FontSize(CleansiaPdfTheme.FontSizeLabel)
                .Bold()
                .FontColor(CleansiaPdfTheme.BrandPrimary);

            col.Item().PaddingTop(4).Row(row =>
            {
                foreach (var extra in data.Extras)
                {
                    row.AutoItem()
                        .PaddingRight(8)
                        .PaddingBottom(4)
                        .Background(CleansiaPdfTheme.LightBlue)
                        .Border(1)
                        .BorderColor(CleansiaPdfTheme.BorderLight)
                        .PaddingVertical(4)
                        .PaddingHorizontal(10)
                        .Text(extra)
                        .FontSize(CleansiaPdfTheme.FontSizeBody)
                        .FontColor(CleansiaPdfTheme.TextPrimary);
                }
            });
        });
    }

    protected virtual void BuildSummary(IContainer container, ReceiptPdfData data)
    {
        var lines = new List<(string Label, string Value, bool IsBold)>();

        // If the company is a VAT payer and we have a breakdown, show net + VAT + gross.
        // Otherwise, show only the gross total (and the non-VAT-payer notice below).
        if (data.IsVatPayer && data.VatAmount.HasValue && data.VatAmount.Value > 0)
        {
            var netAmount = data.NetAmount ?? data.Total - data.VatAmount.Value;
            var vatRateDisplay = data.VatRate.HasValue ? $" {data.VatRate.Value:N0}%" : string.Empty;

            lines.Add(("Subtotal (excl. VAT)", $"{data.Currency}{netAmount:N2}", false));
            lines.Add(($"VAT{vatRateDisplay}", $"{data.Currency}{data.VatAmount.Value:N2}", false));
        }

        lines.Add(("Total", $"{data.Currency}{data.Total:N2}", true));

        container.Column(col =>
        {
            col.Item().SummaryBox(lines);

            // Non-VAT-payer notice — legally required when the company is not VAT-registered.
            if (!data.IsVatPayer && !string.IsNullOrWhiteSpace(data.NonVatPayerNotice))
            {
                col.Item().PaddingTop(6)
                    .Text(data.NonVatPayerNotice)
                    .FontSize(CleansiaPdfTheme.FontSizeLabel)
                    .FontColor(CleansiaPdfTheme.TextSecondary)
                    .Italic();
            }
        });
    }

    protected virtual void BuildPaymentInfo(IContainer container, ReceiptPdfData data)
    {
        container.PaddingTop(CleansiaPdfTheme.SectionSpacing)
            .Background(CleansiaPdfTheme.TableHeaderBg)
            .Padding(14)
            .Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Payment Status")
                        .FontSize(CleansiaPdfTheme.FontSizeLabel)
                        .FontColor(CleansiaPdfTheme.TextSecondary)
                        .Bold();
                    col.Item().Text(data.PaymentStatus)
                        .FontSize(CleansiaPdfTheme.FontSizeBody)
                        .FontColor(CleansiaPdfTheme.TextPrimary)
                        .Bold();
                });

                if (!string.IsNullOrWhiteSpace(data.PaymentType))
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Payment Method")
                            .FontSize(CleansiaPdfTheme.FontSizeLabel)
                            .FontColor(CleansiaPdfTheme.TextSecondary)
                            .Bold();
                        col.Item().Text(data.PaymentType)
                            .FontSize(CleansiaPdfTheme.FontSizeBody)
                            .FontColor(CleansiaPdfTheme.TextPrimary)
                            .Bold();
                    });
                }
            });
    }

    protected virtual void BuildFooter(IContainer container, ReceiptPdfData data)
    {
        var contactInfo = data.Company != null
            ? $"Contact: {data.Company.Email} | {data.Company.Phone}"
            : null;

        container.StandardFooter(
            data.Company?.TradingName ?? "CLEANSIA",
            contactInfo,
            DateTime.UtcNow);
    }
}
