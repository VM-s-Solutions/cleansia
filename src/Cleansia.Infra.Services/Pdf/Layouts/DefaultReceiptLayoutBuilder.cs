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
            // Document title
            col.Item().Element(c => c.DocumentTitle("Receipt"));

            // Customer & Company info
            col.Item().Element(c => BuildInfoSection(c, data));

            // Services & Packages table
            col.Item().Element(c => c.SectionTitle("Services & Packages"));
            col.Item().Element(c => BuildItemsTable(c, data));

            // Summary
            col.Item().PaddingTop(CleansiaPdfTheme.SectionSpacing).Element(c => BuildSummary(c, data));

            // Payment status
            col.Item().Element(c => c.PaymentStatusSection(data.PaymentStatus));
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
                    table.TableCell(package.Name, rowIndex);
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

    protected virtual void BuildSummary(IContainer container, ReceiptPdfData data)
    {
        var lines = new List<(string Label, string Value, bool IsBold)>
        {
            ("Subtotal", $"{data.Currency}{data.Subtotal:N2}", false),
            ("Total", $"{data.Currency}{data.Total:N2}", true)
        };

        container.SummaryBox(lines);
    }

    protected virtual void BuildFooter(IContainer container, ReceiptPdfData data)
    {
        var contactInfo = data.Company != null
            ? $"Contact: {data.Company.Email} | {data.Company.Phone}"
            : null;

        container.StandardFooter(
            data.Company?.TradingName ?? "CLEANSIA",
            contactInfo);
    }
}
