using Cleansia.Infra.Services.Pdf.Components;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.Infra.Services.Pdf.Theme;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf.Layouts;

public class DefaultInvoiceLayoutBuilder : IInvoiceLayoutBuilder
{
    public virtual string CountryCode => "default";

    public virtual void Build(IDocumentContainer container, InvoicePdfData data, CountryInvoiceContext? context)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(CleansiaPdfTheme.FontSizeBody));

            page.Header().Element(h => BuildHeader(h, data));
            page.Content().PaddingHorizontal(30).PaddingVertical(10).Element(c => BuildContent(c, data, context));
            page.Footer().PaddingHorizontal(30).PaddingBottom(20).Element(f => BuildFooter(f, data));
        });
    }

    protected virtual void BuildHeader(IContainer container, InvoicePdfData data)
    {
        container.GradientHeader(
            data.Company?.TradingName ?? "CLEANSIA",
            data.Company?.Tagline,
            meta =>
            {
                meta.Column(col =>
                {
                    col.Item().Element(c => c.MetaField("Invoice #", data.InvoiceNumber));

                    if (!string.IsNullOrWhiteSpace(data.VariableSymbol))
                        col.Item().Element(c => c.MetaField("Variable Symbol", data.VariableSymbol));

                    col.Item().Element(c => c.MetaField("Date", data.GeneratedAt.ToString("dd.MM.yyyy")));
                });
            });
    }

    protected virtual void BuildContent(IContainer container, InvoicePdfData data, CountryInvoiceContext? context)
    {
        container.Column(col =>
        {
            col.Item().Element(c => c.DocumentTitle("Invoice"));

            // Billed To & Payment Period
            col.Item().Element(c => BuildInfoSection(c, data));

            // Order details table
            col.Item().Element(c => c.SectionTitle($"Order Details ({data.Orders.Count} orders)"));
            col.Item().Element(c => BuildOrderTable(c, data));

            // Summary
            col.Item().PaddingTop(CleansiaPdfTheme.SectionSpacing).Element(c => BuildSummary(c, data));

            // Legal notice
            if (!string.IsNullOrWhiteSpace(data.LegalDisclaimer))
            {
                col.Item().PaddingTop(CleansiaPdfTheme.SectionSpacing)
                    .Element(c => c.LegalNoticeBox(data.LegalDisclaimer));
            }
        });
    }

    protected virtual void BuildInfoSection(IContainer container, InvoicePdfData data)
    {
        container.TwoColumnInfoSection(
            left =>
            {
                left.Column(col =>
                {
                    col.Item().Text("Billed To")
                        .FontSize(CleansiaPdfTheme.FontSizeSectionTitle)
                        .Bold()
                        .FontColor(CleansiaPdfTheme.TextPrimary);
                    col.Item().PaddingTop(6);
                    col.Item().Element(c => c.LabeledField("Name", data.EmployeeName));
                    col.Item().Element(c => c.LabeledField("Email", data.EmployeeEmail));
                    col.Item().Element(c => c.LabeledField("Address", data.EmployeeAddress));
                });
            },
            right =>
            {
                right.Column(col =>
                {
                    col.Item().Text("Payment Period")
                        .FontSize(CleansiaPdfTheme.FontSizeSectionTitle)
                        .Bold()
                        .FontColor(CleansiaPdfTheme.TextPrimary);
                    col.Item().PaddingTop(6);
                    col.Item().Element(c => c.LabeledField("From", data.PayPeriodStart));
                    col.Item().Element(c => c.LabeledField("To", data.PayPeriodEnd));
                    col.Item().Element(c => c.LabeledField("Total Orders", data.Orders.Count.ToString()));

                    if (!string.IsNullOrWhiteSpace(data.PaymentReference))
                        col.Item().Element(c => c.LabeledField("Payment Ref", data.PaymentReference));
                });
            });
    }

    protected virtual void BuildOrderTable(IContainer container, InvoicePdfData data)
    {
        container.OrderDetailsTable(
            ["Order #", "Date", "Base Pay", "Extras", "Expenses", "Total"],
            [3, 2, 2, 2, 2, 2],
            table =>
            {
                for (var i = 0; i < data.Orders.Count; i++)
                {
                    var order = data.Orders[i];
                    table.TableCell(order.OrderNumber, i);
                    table.TableCell(order.CompletedAt.ToString("dd.MM.yyyy"), i);
                    table.TableCell($"{data.CurrencySymbol}{order.BasePay:N2}", i, alignRight: true);
                    table.TableCell($"{data.CurrencySymbol}{order.ExtrasPay:N2}", i, alignRight: true);
                    table.TableCell($"{data.CurrencySymbol}{order.ExpensesPay:N2}", i, alignRight: true);
                    table.TableCell($"{data.CurrencySymbol}{order.TotalPay:N2}", i, alignRight: true);
                }
            });
    }

    protected virtual void BuildSummary(IContainer container, InvoicePdfData data)
    {
        var lines = new List<(string Label, string Value, bool IsBold)>
        {
            ("Subtotal", $"{data.CurrencySymbol}{data.SubTotal:N2}", false)
        };

        if (data.BonusAmount != 0)
            lines.Add(("Bonus", $"+{data.CurrencySymbol}{data.BonusAmount:N2}", false));

        if (data.DeductionAmount != 0)
            lines.Add(("Deduction", $"-{data.CurrencySymbol}{data.DeductionAmount:N2}", false));

        if (data.VatAmount != 0)
            lines.Add(("VAT", $"{data.CurrencySymbol}{data.VatAmount:N2}", false));

        lines.Add(("Total", $"{data.CurrencySymbol}{data.TotalAmount:N2}", true));

        container.SummaryBox(lines);
    }

    protected virtual void BuildFooter(IContainer container, InvoicePdfData data)
    {
        var contactInfo = data.Company != null
            ? $"Contact: {data.Company.Email} | {data.Company.Phone}"
            : null;

        container.StandardFooter(
            data.Company?.TradingName ?? "CLEANSIA",
            contactInfo,
            data.GeneratedAt);
    }
}
